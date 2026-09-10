#requires -Version 7.0
[CmdletBinding()]
param([string] $BariPath = (Join-Path (Split-Path $PSScriptRoot) 'target/full/bari.dll'))

$ErrorActionPreference = 'Stop'
$BariPath = (Resolve-Path -LiteralPath $BariPath).Path
$repoRoot = Split-Path $PSScriptRoot
$fixture = Join-Path $repoRoot 'systest/net10-runtime'
# Spaces deliberately exercise process argument handling.
$testRoot = Join-Path $repoRoot ('tmp/net10 smoke ' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
foreach ($item in @('suite.yaml', 'src', 'scripts')) {
    Copy-Item -LiteralPath (Join-Path $fixture $item) -Destination $testRoot -Recurse
}
function Invoke-Bari([string[]] $BariArguments) {
    if ([IO.Path]::GetExtension($BariPath) -eq '.dll') { & dotnet $BariPath @BariArguments }
    else { & $BariPath @BariArguments }
    if ($LASTEXITCODE -ne 0) { throw "Bari smoke test failed with exit code $LASTEXITCODE" }
}
Push-Location $testRoot
try {
    Invoke-Bari @('--target', 'release', 'rebuild', 'main')
    $message = & dotnet target/main/Smoke.dll
    if ($LASTEXITCODE -ne 0) { throw 'The .NET 10 executable failed.' }
    $result = $message | ConvertFrom-Json
    if ($result.Runtime -ne 10 -or $result.Message -ne 'Bari on .NET 10') { throw 'Unexpected executable output.' }
    $python = Get-Content -LiteralPath target/main/nested/python.json -Raw | ConvertFrom-Json
    if ($python.python -ne 'ok' -or $python.product -ne 'main') { throw 'The Python postprocessor failed.' }
    Invoke-Bari @('--target', 'release', 'build', 'main')
    Invoke-Bari @('--target', 'release', 'test')

    # Validate every repository Python script with the same IronPython runtime.
    $scriptRoot = (Join-Path $repoRoot 'systest').Replace('\', '/')
    @"
import os
root = r'$scriptRoot'
count = 0
for directory, children, names in os.walk(root):
    children[:] = [name for name in children if name not in ('target', 'cache', 'tmp')]
    for name in names:
        if name.endswith('.py'):
            path = os.path.join(directory, name)
            with open(path, 'r') as source:
                compile(source.read(), path, 'exec')
            count += 1
print('Compiled %d repository Python scripts' % count)
results = []
"@ | Set-Content scripts/postprocessors/verify.py
    Invoke-Bari @('--target', 'release', 'build', 'main')

    if (Get-Command hg -ErrorAction SilentlyContinue) {
        $hgRoot = Join-Path $testRoot 'hg-suite'
        New-Item -ItemType Directory -Path $hgRoot | Out-Null
        Push-Location $hgRoot
        try {
            @'
---
suite: Mercurial smoke test
version: 1.0.$HG_REVNO
csharp:
    target-framework: v10.0
modules:
    - name: hg
      projects:
          - name: HgVersion
'@ | Set-Content suite.yaml
            New-Item -ItemType Directory -Path src/hg/HgVersion/cs -Force | Out-Null
            'public class HgVersion { }' | Set-Content src/hg/HgVersion/cs/Version.cs
            & hg init
            if ($LASTEXITCODE -ne 0) { throw 'hg init failed.' }
            & hg add suite.yaml
            if ($LASTEXITCODE -ne 0) { throw 'hg add failed.' }
            & hg commit --user 'Bari smoke test' --message 'Test fixture'
            if ($LASTEXITCODE -ne 0) { throw 'hg commit failed.' }
            Invoke-Bari @('--target', 'release', 'vs', 'hg')
            $project = Get-Content src/hg/HgVersion/cs/HgVersion.csproj -Raw
            if ($project -notmatch '<FileVersion>1\.0\.0</FileVersion>') { throw 'HG_REVNO was not resolved to revision zero.' }
        } finally { Pop-Location }
    } else {
        Write-Host 'Mercurial smoke test skipped: hg is not installed.'
    }
    Write-Host "The .NET 10 runtime smoke tests passed. Results: $testRoot"
} finally { Pop-Location }
