# Compare logical payloads, not ZIP container metadata or timestamps.
param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'Output'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$output = [IO.Path]::GetFullPath($OutputDirectory)
$info = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Source/Terminals/Properties/Common.AssemblyInfo.cs') -Raw
if ($info -notmatch 'AssemblyVersion\("([^"]+)"\)') { throw 'AssemblyVersion not found' }
$version = $Matches[1] -replace '\.\*$', ''
$zip = Join-Path $output "Terminals_v$version.zip"

function Get-PayloadSnapshot {
    foreach ($stage in @('.portable-staging', '.installer-staging')) {
        $root = Join-Path $output $stage
        $files = @(Get-ChildItem -LiteralPath $root -Recurse -File -Force)
        if ($files.Count -eq 0) { throw "Empty staging directory: $root" }
        foreach ($file in $files) {
            $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            "$stage/$relative $hash"
        }
    }
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            if (-not $seen.Add($entry.FullName)) { throw "Duplicate ZIP entry: $($entry.FullName)" }
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try {
                $hash = [Convert]::ToHexString($sha.ComputeHash($stream))
                "ZIP/$($entry.FullName) $hash"
            }
            finally { $stream.Dispose(); $sha.Dispose() }
        }
        if ($seen.Count -eq 0) { throw 'Empty portable ZIP' }
    }
    finally { $archive.Dispose() }
}

& (Join-Path $PSScriptRoot 'PackOutput.ps1') -OutputDirectory $output
$first = @(Get-PayloadSnapshot)
& (Join-Path $PSScriptRoot 'PackOutput.ps1') -OutputDirectory $output
$second = @(Get-PayloadSnapshot)
$difference = @(Compare-Object -ReferenceObject $first -DifferenceObject $second -CaseSensitive)
if ($difference.Count -gt 0) { throw "Packaging payload changed between runs: $($difference | Out-String)" }
Write-Host "Verified packaging idempotency across $($second.Count) ZIP and staging file hashes."
