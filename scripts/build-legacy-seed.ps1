#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Destination)

$ErrorActionPreference = 'Stop'
if (!$IsWindows) { throw 'Building the initial .NET Framework seed requires Windows. Pass -BariPath with an existing .NET 10 Bari on other platforms.' }
$repoRoot = Split-Path $PSScriptRoot
$Destination = [IO.Path]::GetFullPath($Destination)
$bootstrapRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot '_bootstrap')) + [IO.Path]::DirectorySeparatorChar
if (!$Destination.StartsWith($bootstrapRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'The seed destination must be inside _bootstrap.' }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null

function Expand-NuGet([string] $Id, [string] $Version, [string] $Name) {
    $zip = Join-Path $Destination "$Name.zip"
    $lowerId = $Id.ToLowerInvariant()
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$lowerId/$Version/$lowerId.$Version.nupkg" -OutFile $zip
    Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $Destination $Name) -Force
}

# The last .NET Framework source revision is a bridge from the published Bari
# (which cannot parse net10.0) to this suite. No generated project is hand-edited.
$legacyRevision = '9386ad006f32f4121428f57e0732f6a5964ea18b'
$archive = Join-Path $Destination 'source.zip'
& git -C $repoRoot archive --format=zip "--output=$archive" $legacyRevision
if ($LASTEXITCODE -ne 0) { throw 'The seed revision is unavailable. Fetch full git history or supply -BariPath.' }
$sourceRoot = Join-Path $Destination 'source'
Expand-Archive -LiteralPath $archive -DestinationPath $sourceRoot
$suitePath = Join-Path $sourceRoot 'suite.yaml'
$suite = [IO.File]::ReadAllText($suitePath)
$suite = $suite.Replace('version:    $GIT_TAG.$GIT_REVNO', 'version:    1.0.3.68').Replace('contracts:  enabled', 'contracts:  disabled')
[IO.File]::WriteAllText($suitePath, $suite)

Write-Host 'Building the pinned legacy seed using the published Bari 1.0.3.25.'
Expand-NuGet 'bari' '1.0.3.25' 'published'
Expand-NuGet 'Microsoft.NETFramework.ReferenceAssemblies.net40' '1.0.3' 'reference'
Push-Location $sourceRoot
try {
    & (Join-Path $Destination 'published/tools/bari.exe') --target release vs full
    if ($LASTEXITCODE -ne 0) { throw 'Legacy project generation failed.' }
    & dotnet msbuild target/full-withtests.sln -t:Build -nologo -v:minimal `
        "-p:TargetFrameworkRootPath=$(Join-Path $Destination 'reference/build')" `
        "-p:FrameworkPathOverride=$(Join-Path $Destination 'reference/build/.NETFramework/v4.0')" `
        -p:CodeContractsEnableRuntimeChecking=false
    if ($LASTEXITCODE -ne 0) { throw 'Legacy seed compilation failed.' }
} finally {
    Pop-Location
}

$hostDirectory = Join-Path $Destination 'host'
New-Item -ItemType Directory -Path $hostDirectory | Out-Null
foreach ($module in @('core', 'dotnetplugins', 'scripting', 'addon', 'innosetup', 'vcs', 'tools')) {
    Get-ChildItem -LiteralPath (Join-Path $sourceRoot "target/$module") -Force |
        Copy-Item -Destination $hostDirectory -Recurse -Force
}
