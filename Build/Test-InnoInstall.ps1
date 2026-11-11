# Smoke-test Inno Setup EXE with verbose logs, installed files and uninstall.
param([string]$InstallerPath=(Join-Path $PSScriptRoot 'Output/TerminalsSetup_4.0.1.exe'),
      [string]$LogDirectory=(Join-Path $PSScriptRoot 'Output/InstallerLogs'))
$ErrorActionPreference='Stop'
if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) { throw "Missing installer: $InstallerPath" }
New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
$installLog=Join-Path $LogDirectory 'install.log'
$uninstallLog=Join-Path $LogDirectory 'uninstall.log'
$destination=Join-Path $env:RUNNER_TEMP 'Terminals-InnoSmoke'
if (Test-Path -LiteralPath $destination) { throw "Install target not clean: $destination" }
function Invoke-Setup([string]$exe,[string[]]$arguments,[string]$log,[string]$phase) {
    $process=Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -Wait
    Write-Host "$phase exit code: $($process.ExitCode)"
    if (-not (Test-Path -LiteralPath $log -PathType Leaf)) { throw "Missing $phase log: $log" }
    if ($process.ExitCode -ne 0) {
        Select-String -LiteralPath $log -Pattern 'error|exception|failed|abort|not found' -Context 8,2 |
            Select-Object -Last 5 | ForEach-Object {
                $_.Context.PreContext | ForEach-Object { Write-Host $_ }
                Write-Host $_.Line
                $_.Context.PostContext | ForEach-Object { Write-Host $_ }
            }
        throw "$phase failed, exit $($process.ExitCode); see $log"
    }
}
$installed=$false
$failure=$null
try {
    Invoke-Setup $InstallerPath @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=$destination",('/LOG="{0}"' -f $installLog)) $installLog 'Install'
    $installed=$true
    foreach ($entry in @('Terminals.exe','DnsClient.dll','EntityFramework.dll','EntityFramework.SqlServer.dll',
       'Plugins\Rdp\Terminals.Plugins.Rdp.dll','Plugins\Vnc\Terminals.Plugins.Vnc.dll')) {
        $file=Join-Path $destination $entry
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing installed file: $file" }
    }
    # Compare the entire installed payload with the exact installer staging contents.
    foreach ($entry in @(& (Join-Path $PSScriptRoot 'PackageFiles.ps1'))) {
        $actualFile = Join-Path $destination $entry
        $expectedFile = Join-Path $PSScriptRoot ('Output/.installer-staging/' + $entry)
        if (-not (Test-Path -LiteralPath $actualFile -PathType Leaf)) { throw "Installed payload file missing: $entry" }
        $actualHash = (Get-FileHash -LiteralPath $actualFile -Algorithm SHA256).Hash
        $expectedHash = (Get-FileHash -LiteralPath $expectedFile -Algorithm SHA256).Hash
        if ($actualHash -ne $expectedHash) { throw "Installed payload differs from staged installer file: $entry" }
    }
    [xml]$config=Get-Content -LiteralPath (Join-Path $destination 'Terminals.exe.config') -Raw
    $portable=$config.SelectSingleNode('/configuration/applicationSettings/Terminals.Properties.Settings/setting[@name="Portable"]/value')
    if ($null -eq $portable -or $portable.InnerText -ne 'False') { throw 'Installed configuration must set Portable=False' }
}
catch { $failure=$_ }
finally {
    if ($installed) {
        try {
            $uninstall=@(Get-ChildItem -LiteralPath $destination -Filter 'unins*.exe' -File)
            if ($uninstall.Count -ne 1) { throw "Expected one uninstaller, found $($uninstall.Count)" }
            Invoke-Setup $uninstall[0].FullName @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="{0}"' -f $uninstallLog)) $uninstallLog 'Uninstall'
            foreach ($entry in @(& (Join-Path $PSScriptRoot 'PackageFiles.ps1'))) {
                if (Test-Path -LiteralPath (Join-Path $destination $entry) -PathType Leaf) {
                    throw "Packaged file remains after uninstall: $entry"
                }
            }
        }
        catch {
            if ($null -eq $failure) { $failure=$_ }
            else { Write-Warning "Uninstall failed too: $($_.Exception.Message)" }
        }
    }
}
if ($null -ne $failure) { throw $failure }
Write-Host 'Inno installer smoke-test passed.'
