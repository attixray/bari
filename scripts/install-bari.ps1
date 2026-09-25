<#
.SYNOPSIS
Installs or updates bari from a GitHub release of attixray/bari.

.DESCRIPTION
Downloads bari-<tag>-net10.zip from the release, checks it against the release's
SHA256SUMS and installs it into InstallDir. An existing installation is kept next to
it as <InstallDir>.previous, replacing an older one there. bari needs the .NET 10
runtime; the script warns when it is missing.

`bari selfupdate` runs this script in a new window after checking for a newer
release; it passes the process id of the running bari, so the files are replaced
only after that bari has exited.

.EXAMPLE
& ([scriptblock]::Create((Invoke-RestMethod https://github.com/attixray/bari/releases/latest/download/install-bari.ps1))) -InstallDir C:\Bari

.EXAMPLE
.\install-bari.ps1 -InstallDir C:\Bari -Version 1.1.0
#>
[CmdletBinding()]
param(
    [string] $InstallDir = 'C:\Bari',
    [string] $Version = 'latest',
    [string] $Repository = 'attixray/bari',
    [int] $WaitForProcessId = 0
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Install-Bari {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    # Windows PowerShell takes the user agent only as -UserAgent. A token is optional, against the
    # API's rate limit for anonymous calls; the downloads redirect to storage that rejects it.
    $userAgent = 'bari-installer'
    $apiHeaders = @{}
    if ($env:GITHUB_TOKEN) { $apiHeaders['Authorization'] = "Bearer $env:GITHUB_TOKEN" }
    $api = if ($Version -eq 'latest') {
        "https://api.github.com/repos/$Repository/releases/latest"
    } else {
        "https://api.github.com/repos/$Repository/releases/tags/$Version"
    }
    $release = Invoke-RestMethod $api -Headers $apiHeaders -UserAgent $userAgent
    $zipAsset = $release.assets | Where-Object { $_.name -like 'bari-*-net10.zip' } | Select-Object -First 1
    $sumsAsset = $release.assets | Where-Object { $_.name -eq 'SHA256SUMS' } | Select-Object -First 1
    if (-not $zipAsset -or -not $sumsAsset) {
        throw "The release $($release.tag_name) has no bari-*-net10.zip or SHA256SUMS."
    }
    Write-Host "Installing bari $($release.tag_name) from $($release.html_url) into $InstallDir"

    $runtimes = try { & dotnet --list-runtimes 2>$null } catch { @() }
    if (-not ($runtimes | Select-String '^Microsoft\.NETCore\.App 10\.')) {
        Write-Warning 'The .NET 10 runtime was not found. bari needs it: https://dotnet.microsoft.com/download/dotnet/10.0'
    }

    $work = Join-Path ([IO.Path]::GetTempPath()) ("bari-install-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory $work | Out-Null
    try {
        $zip = Join-Path $work $zipAsset.name
        $sums = Join-Path $work 'SHA256SUMS'
        Invoke-WebRequest $zipAsset.browser_download_url -OutFile $zip -UseBasicParsing -UserAgent $userAgent
        Invoke-WebRequest $sumsAsset.browser_download_url -OutFile $sums -UseBasicParsing -UserAgent $userAgent

        $pattern = '^([0-9a-fA-F]{64})\s+\*?' + [regex]::Escape($zipAsset.name) + '$'
        $expected = Get-Content $sums | ForEach-Object { if ($_ -match $pattern) { $Matches[1] } } | Select-Object -First 1
        if (-not $expected) { throw "SHA256SUMS has no line for $($zipAsset.name)." }
        $actual = (Get-FileHash $zip -Algorithm SHA256).Hash
        if ($actual -ne $expected.ToUpperInvariant()) {
            throw "Checksum mismatch for $($zipAsset.name): expected $expected, got $actual."
        }

        $InstallDir = [IO.Path]::GetFullPath($InstallDir.TrimEnd('\', '/'))
        $staged = "$InstallDir.new"
        $previous = "$InstallDir.previous"
        if (Test-Path $staged) { Remove-Item $staged -Recurse -Force }
        Expand-Archive $zip $staged
        if (-not (Test-Path (Join-Path $staged 'bari.dll'))) { throw "$($zipAsset.name) does not contain bari.dll." }

        if ($WaitForProcessId) {
            $running = Get-Process -Id $WaitForProcessId -ErrorAction SilentlyContinue
            if ($running) {
                Write-Host "Waiting for bari (process $WaitForProcessId) to exit..."
                $running.WaitForExit()
            }
        }

        # The old installation moves aside first; the old .previous is replaced only once the new
        # installation is in place, so a failed update keeps every existing copy.
        $hadInstallation = Test-Path $InstallDir
        $aside = "$InstallDir.replacing"
        if ($hadInstallation) {
            if (Test-Path $aside) { Remove-Item $aside -Recurse -Force }
            try {
                Rename-Item $InstallDir (Split-Path $aside -Leaf)
            } catch {
                Remove-Item $staged -Recurse -Force -ErrorAction SilentlyContinue
                throw "Could not move $InstallDir aside. Is a bari from it still running? $($_.Exception.Message)"
            }
        }
        try {
            Rename-Item $staged (Split-Path $InstallDir -Leaf)
        } catch {
            if ($hadInstallation) { Rename-Item $aside (Split-Path $InstallDir -Leaf) }
            throw
        }
        if ($hadInstallation) {
            if (Test-Path $previous) { Remove-Item $previous -Recurse -Force }
            Rename-Item $aside (Split-Path $previous -Leaf)
        }

        Write-Host "Installed bari $($release.tag_name) in $InstallDir."
        if ($hadInstallation) { Write-Host "The previous installation is in $previous." }
    } finally {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

try {
    Install-Bari
    $failed = $false
} catch {
    Write-Host "bari was not installed: $($_.Exception.Message)" -ForegroundColor Red
    $failed = $true
}
# Started by bari selfupdate in a window of its own: keep it open until the result is read.
if ($WaitForProcessId) { Read-Host 'Press Enter to close' | Out-Null }
if ($failed) { exit 1 }
