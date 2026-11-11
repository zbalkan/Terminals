# Build a portable ZIP and installer-ready directory from one audited Release payload.
param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'Output'))
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
$release = Join-Path $output 'Release'
& (Join-Path $PSScriptRoot 'Assert-PackagePayload.ps1') -OutputDirectory $release
$info = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Source/Terminals/Properties/Common.AssemblyInfo.cs') -Raw
if ($info -notmatch 'AssemblyVersion\("([^"]+)"\)') { throw 'AssemblyVersion not found' }
$version = $Matches[1] -replace '\.\*$', ''
$zip = Join-Path $output "Terminals_v$version.zip"
$portable = Join-Path $output '.portable-staging'
$installer = Join-Path $output '.installer-staging'
foreach ($dir in @($portable, $installer)) {
    if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}
foreach ($entry in @(& (Join-Path $PSScriptRoot 'PackageFiles.ps1'))) {
    $source = Join-Path $release $entry
    $destination = Join-Path $portable $entry
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
}
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -CompressionLevel Optimal -Force
# The installed application uses user-profile storage; ZIP must remain portable.
Copy-Item -Path (Join-Path $portable '*') -Destination $installer -Recurse -Force
$settingsPath = Join-Path $installer 'Terminals.exe.config'
[xml]$settings = Get-Content -LiteralPath $settingsPath -Raw
$portableSetting = $settings.SelectSingleNode('/configuration/applicationSettings/Terminals.Properties.Settings/setting[@name="Portable"]/value')
if ($null -eq $portableSetting -or $portableSetting.InnerText -ne 'True') { throw 'Unexpected Portable setting in Release configuration' }
$portableSetting.InnerText = 'False'
$settings.Save($settingsPath)
$logPath = Join-Path $installer 'Terminals.log4net.config'
[xml]$logging = Get-Content -LiteralPath $logPath -Raw
$filePath = $logging.SelectSingleNode('/log4net/appender[@name="RollingLogFileAppender"]/file')
if ($null -eq $filePath) { throw 'RollingLogFileAppender file path missing' }
$filePath.SetAttribute('value', '${LOCALAPPDATA}\Robert_Chartier\Terminals\Data\logs\CurrentLog.txt')
$logging.Save($logPath)
Write-Host "Prepared ZIP $zip and installed-mode payload at $installer"
