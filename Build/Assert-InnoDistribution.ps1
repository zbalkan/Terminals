# Validate compiled Inno EXE and portable ZIP against the shared package manifest.
param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'Output'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$output = [IO.Path]::GetFullPath($OutputDirectory)
$release = Join-Path $output 'Release'
$portable = Join-Path $output '.portable-staging'
$installed = Join-Path $output '.installer-staging'
$manifest = @(& (Join-Path $PSScriptRoot 'PackageFiles.ps1'))
$assemblyInfo = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Source/Terminals/Properties/Common.AssemblyInfo.cs') -Raw
if ($assemblyInfo -notmatch 'AssemblyVersion\("([^"]+)"\)') { throw 'AssemblyVersion missing' }
$version = $Matches[1] -replace '\.\*$', ''
$zipPath = Join-Path $output "Terminals_v$version.zip"
$setupPath = Join-Path $output "TerminalsSetup_$version.exe"
foreach ($path in @($zipPath, $setupPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing artifact: $path" }
}
$stream = [IO.File]::OpenRead($setupPath)
try {
    if ($stream.Length -lt 1024 -or $stream.ReadByte() -ne 77 -or $stream.ReadByte() -ne 90) {
        throw "Compiled Inno setup is not a valid-sized PE candidate: $setupPath"
    }
}
finally { $stream.Dispose() }

$expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $manifest) {
    $name = $file.Replace('\','/')
    if (-not $expected.Add($name)) { throw "Duplicate file manifest entry: $name" }
}
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $archive.Entries) {
        $name = $entry.FullName
        if ($name.EndsWith('/')) { continue }
        if (-not $seen.Add($name)) { throw "Duplicate ZIP entry: $name" }
        if (-not $expected.Contains($name)) { throw "Unexpected ZIP entry: $name" }
        $relative = $name.Replace('/', '\')
        $original = Join-Path $release $relative
        $staged = Join-Path $portable $relative
        foreach ($path in @($original, $staged)) {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "ZIP file missing on disk: $path" }
        }
        $entryStream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $zipHash = ([BitConverter]::ToString($sha.ComputeHash($entryStream))).Replace('-', '') }
        finally { $entryStream.Dispose(); $sha.Dispose() }
        foreach ($path in @($original, $staged)) {
            $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            if ($hash -ne $zipHash) { throw "ZIP content mismatch: $name in $path" }
        }
    }
    foreach ($name in $expected) {
        if (-not $seen.Contains($name)) { throw "Required ZIP entry missing: $name" }
    }
}
finally { $archive.Dispose() }

foreach ($file in $manifest) {
    $staged = Join-Path $installed $file
    if (-not (Test-Path -LiteralPath $staged -PathType Leaf)) { throw "Missing installer payload file: $file" }
    if ($file -in @('Terminals.exe.config','Terminals.log4net.config')) { continue }
    $sourceHash = (Get-FileHash -LiteralPath (Join-Path $release $file) -Algorithm SHA256).Hash
    $stagedHash = (Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash
    if ($sourceHash -ne $stagedHash) { throw "Installer payload changed unexpectedly: $file" }
}
function Read-Portable([string]$file) {
    [xml]$xml = Get-Content -LiteralPath $file -Raw
    $node = $xml.SelectSingleNode('/configuration/applicationSettings/Terminals.Properties.Settings/setting[@name="Portable"]/value')
    if ($null -eq $node) { throw "Missing Portable configuration: $file" }
    return $node.InnerText
}
$releaseMode = Read-Portable (Join-Path $release 'Terminals.exe.config')
$zipMode = Read-Portable (Join-Path $portable 'Terminals.exe.config')
$setupMode = Read-Portable (Join-Path $installed 'Terminals.exe.config')
if ($releaseMode -ne 'True' -or $zipMode -ne 'True' -or $setupMode -ne 'False') {
    throw "Invalid portable modes: Release=$releaseMode ZIP=$zipMode Setup=$setupMode"
}
[xml]$logging = Get-Content -LiteralPath (Join-Path $installed 'Terminals.log4net.config') -Raw
$logNode = $logging.SelectSingleNode('/log4net/appender[@name="RollingLogFileAppender"]/file')
$expectedLogPath = '$' + '{LOCALAPPDATA}\Robert_Chartier\Terminals\Data\logs\CurrentLog.txt'
if ($null -eq $logNode -or $logNode.GetAttribute('value') -ne $expectedLogPath) {
    throw "Installed logging path is incorrect"
}
Write-Host "Validated Inno EXE, ZIP, $($expected.Count) payload entries, SHA-256 integrity and configuration modes."
