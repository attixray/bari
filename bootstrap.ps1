#requires -Version 7.0
[CmdletBinding()]
param(
    [string] $BariPath,
    [ValidateSet('debug', 'release')][string] $Configuration = 'release',
    [switch] $Test
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$runRoot = Join-Path $repoRoot ('_bootstrap/run-' + [guid]::NewGuid().ToString('N'))
$modules = 'core', 'dotnetplugins', 'scripting', 'addon', 'innosetup', 'vcs', 'tools'

function Invoke-Bari([string] $Executable, [string[]] $BariArguments) {
    if ([IO.Path]::GetExtension($Executable) -eq '.dll') {
        & dotnet $Executable @BariArguments
    } else {
        & $Executable @BariArguments
    }
    if ($LASTEXITCODE -ne 0) { throw "Bari failed with exit code $LASTEXITCODE" }
}

Push-Location $repoRoot
try {
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or $sdk -notmatch '^10\.') { throw '.NET 10 SDK is required.' }
    New-Item -ItemType Directory -Path $runRoot -Force | Out-Null

    if (!$BariPath) {
        & (Join-Path $repoRoot 'scripts/build-legacy-seed.ps1') -Destination (Join-Path $runRoot 'legacy')
        $BariPath = Join-Path $runRoot 'legacy/host/bari.exe'
    } else {
        $BariPath = (Resolve-Path -LiteralPath $BariPath).Path
        # A caller may pass target/full/bari.exe. Copy its distribution before cleaning target.
        $hostSource = Split-Path $BariPath
        if ($runRoot.StartsWith($hostSource.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The bootstrap executable must be in its own distribution directory, not a parent of _bootstrap.'
        }
        $hostCopy = Join-Path $runRoot 'host'
        New-Item -ItemType Directory -Path $hostCopy | Out-Null
        Get-ChildItem -LiteralPath $hostSource -Force | Copy-Item -Destination $hostCopy -Recurse -Force
        $BariPath = Join-Path $hostCopy (Split-Path $BariPath -Leaf)
    }

    Write-Host 'Generating the .NET 10 projects from suite.yaml with the seed Bari.'
    Invoke-Bari $BariPath @('--target', 'bootstrap', 'clean')
    Invoke-Bari $BariPath @('--target', 'bootstrap', 'vs', 'full')
    & dotnet build (Join-Path $repoRoot 'target/full-withtests.sln') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'The .NET 10 bootstrap build failed.' }

    $stage1 = Join-Path $runRoot 'net10'
    New-Item -ItemType Directory -Path $stage1 | Out-Null
    foreach ($module in $modules) {
        Get-ChildItem -LiteralPath (Join-Path $repoRoot "target/$module") -Force |
            Copy-Item -Destination $stage1 -Recurse -Force
    }
    $newBari = Join-Path $stage1 'bari.dll'
    Write-Host 'Rebuilding Bari with the newly compiled .NET 10 Bari.'
    Invoke-Bari $newBari @('--target', $Configuration, 'rebuild', 'full')
    Invoke-Bari (Join-Path $repoRoot 'target/full/bari.dll') @('help')

    if ($Test) {
        Invoke-Bari $newBari @('--target', $Configuration, 'test')
        & (Join-Path $repoRoot 'scripts/test-net10.ps1') -BariPath (Join-Path $repoRoot 'target/full/bari.dll')
    }
    Write-Host "Bari is ready in $repoRoot/target/full. Independent build host: $newBari"
} finally {
    Pop-Location
}
