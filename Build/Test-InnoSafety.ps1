# Exercise fail-closed Inno Setup preflight on an isolated Windows runner.
param([string]$InstallerPath = (Join-Path $PSScriptRoot 'Output/TerminalsSetup_4.0.1.exe'),
      [string]$LogDirectory = (Join-Path $PSScriptRoot 'Output/InstallerLogs'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) { throw "Missing installer: $InstallerPath" }
New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
$setup = [IO.Path]::GetFullPath($InstallerPath)
function Assert-SetupRefused([string]$target, [string]$log) {
    if (Test-Path -LiteralPath (Join-Path $target 'Terminals.exe')) {
        throw "Unsafe test target already contains Terminals.exe: $target"
    }
    $process = Start-Process -FilePath $setup -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=$target", ('/LOG="{0}"' -f $log)
    ) -Wait -PassThru
    Write-Host "Expected installer refusal at $target, returned $($process.ExitCode)"
    if ($process.ExitCode -eq 0) { throw "Installer accepted an unsafe installation target: $target" }
    if (Test-Path -LiteralPath (Join-Path $target 'Terminals.exe') -PathType Leaf) {
        throw "Installer copied application files despite refusing the target: $target"
    }
}
$portable = Join-Path $env:RUNNER_TEMP 'Terminals-ExistingPortable'
$marker = Join-Path $portable 'Data/Favorites.xml'
$legacyMarker = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Terminals-CI-Legacy-MSI'
try {
    New-Item -ItemType Directory -Path (Split-Path $marker -Parent) -Force | Out-Null
    Set-Content -LiteralPath $marker -Value '<favorites>keep this data</favorites>'
    Assert-SetupRefused $portable (Join-Path $LogDirectory 'portable-collision.log')
    if ((Get-Content -LiteralPath $marker -Raw) -notmatch 'keep this data') {
        throw 'Installer damaged existing portable data'
    }
    New-Item -Path $legacyMarker -Force | Out-Null
    New-ItemProperty -Path $legacyMarker -Name DisplayName -PropertyType String -Value 'Terminals' -Force | Out-Null
    New-ItemProperty -Path $legacyMarker -Name WindowsInstaller -PropertyType DWord -Value 1 -Force | Out-Null
    $target = Join-Path $env:RUNNER_TEMP 'Terminals-LegacyMsiConflict'
    Assert-SetupRefused $target (Join-Path $LogDirectory 'legacy-msi-collision.log')
}
finally {
    if (Test-Path -LiteralPath $legacyMarker) {
        Remove-Item -LiteralPath $legacyMarker -Recurse -Force
    }
    if (Test-Path -LiteralPath $portable) {
        Remove-Item -LiteralPath $portable -Recurse -Force
    }
}
Write-Host 'Inno Setup legacy MSI and portable-data refusal checks passed.'
