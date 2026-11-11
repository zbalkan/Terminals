# Validate the shared ZIP/Inno Setup distribution manifest against the Release output.
# Does not claim to resolve arbitrary/dynamically loaded dependencies.
param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'Output/Release'))
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $root 'Terminals.exe') -PathType Leaf)) {
    throw "Release output missing Terminals.exe: $root"
}
$manifest = @(& (Join-Path $PSScriptRoot 'PackageFiles.ps1'))
$files = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $manifest) {
    if ([IO.Path]::IsPathRooted($entry) -or $entry -match '(^|[\\/])\.\.([\\/]|$)') {
        throw "Distribution manifest contains an unsafe path: $entry"
    }
    $relative = $entry.Replace('\','/')
    if (-not $files.Add($relative)) { throw "Duplicate manifest file: $relative" }
    if (-not (Test-Path -LiteralPath (Join-Path $root $entry) -PathType Leaf)) {
        throw "Missing distribution file in Release output: $relative"
    }
}

# These are explicit build artifacts, CLR-independent DLL config, or a seed template
# that is already embedded in the application. Do not silently exclude arbitrary files.
$buildOnly = @('Data/Terminals.config','Plugins/Vnc/zlib.net.dll.config')
$prefix = $root.TrimEnd('\','/').Length + 1
$unlisted = @(Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
    $file = $_
    $relative = $file.FullName.Substring($prefix).Replace('\','/')
    if ($files.Contains($relative) -or $relative -in $buildOnly) { return }
    if ($file.Extension.ToLowerInvariant() -in @('.pdb','.log','.binlog')) { return }
    if ($file.Extension.ToLowerInvariant() -eq '.xml') {
        $stem = $file.FullName.Substring(0, $file.FullName.Length - 4)
        if ((Test-Path -LiteralPath ($stem + '.dll') -PathType Leaf) -or
            (Test-Path -LiteralPath ($stem + '.exe') -PathType Leaf)) { return }
    }
    $relative
})
if ($unlisted.Count) { throw "Unlisted Release output files: $($unlisted -join ', ')" }
Write-Host "Validated $($files.Count) shared ZIP/Inno distribution files and no unlisted non-exempt Release files."
