param(
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$SdkToolsDirectory
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core' -and $env:OS -ne 'Windows_NT') {
    throw 'MSTSC ActiveX interop generation requires Windows.'
}
$control = Join-Path $env:WINDIR 'System32\mstscax.dll'
if (-not (Test-Path -LiteralPath $control)) {
    throw "Windows RDP ActiveX type library is missing: $control"
}

$toolDirs = @()
if ($SdkToolsDirectory) { $toolDirs += $SdkToolsDirectory }
if ($env:TargetFrameworkSDKToolsDirectory) { $toolDirs += $env:TargetFrameworkSDKToolsDirectory }
$programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
if ($programFilesX86) {
    $toolDirs += (Join-Path $programFilesX86 'Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8.1 Tools')
    $toolDirs += (Join-Path $programFilesX86 'Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools')
}
$toolDirs = @($toolDirs | Select-Object -Unique)
$tlbimp = $null
$aximp = $null
foreach ($directory in $toolDirs) {
    $candidateTlbimp = Join-Path $directory 'tlbimp.exe'
    $candidateAximp = Join-Path $directory 'aximp.exe'
    if ((Test-Path -LiteralPath $candidateTlbimp) -and (Test-Path -LiteralPath $candidateAximp)) {
        $tlbimp = $candidateTlbimp
        $aximp = $candidateAximp
        break
    }
}
if (-not $tlbimp) {
    throw "The .NET Framework SDK's tlbimp.exe and aximp.exe are required. Install the Visual Studio .NET Framework SDK tools; searched: $($toolDirs -join ', ')."
}

function Get-FileSha256([string]$path) {
    $hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($path)
        try {
            return [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '')
        }
        finally { $stream.Dispose() }
    }
    finally { $hasher.Dispose() }
}

$target = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($target) | Out-Null
$rcw = Join-Path $target 'MSTSCLib.dll'
$ax = Join-Path $target 'AxInterop.MSTSCLib.dll'
$stamp = Join-Path $target 'mstscax.sha256'
# A named mutex serializes generation when the app, RDP plugin and tests build in parallel.
$mutex = New-Object System.Threading.Mutex($false, 'Local\TerminalsMstscInteropGeneration')
$locked = $false
try {
    try {
        $locked = $mutex.WaitOne([TimeSpan]::FromMinutes(3))
    }
    catch [System.Threading.AbandonedMutexException] {
        $locked = $true
    }
    if (-not $locked) { throw 'Timed out waiting for MSTSC interop generation lock.' }

    $fingerprint = (Get-FileSha256 $control) + ':' +
        (Get-FileSha256 $tlbimp) + ':' +
        (Get-FileSha256 $aximp)
    if ((Test-Path -LiteralPath $rcw) -and (Test-Path -LiteralPath $ax) -and
        (Test-Path -LiteralPath $stamp) -and
        ((Get-Content -LiteralPath $stamp -Raw).Trim() -eq $fingerprint)) {
        return
    }

    $working = Join-Path $target ('temp-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($working) | Out-Null
    try {
        $tempRcw = Join-Path $working 'MSTSCLib.dll'
        $tempAx = Join-Path $working 'AxInterop.MSTSCLib.dll'
        & $tlbimp $control "/out:$tempRcw" /nologo
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $tempRcw)) {
            throw "tlbimp failed to generate MSTSCLib.dll (exit code $LASTEXITCODE)."
        }
        & $aximp $control "/rcw:$tempRcw" "/out:$tempAx" /nologo
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $tempAx)) {
            throw "aximp failed to generate AxInterop.MSTSCLib.dll (exit code $LASTEXITCODE)."
        }
        # Refresh both assemblies together while holding the generation mutex.
        Remove-Item -LiteralPath $stamp -Force -ErrorAction SilentlyContinue
        Copy-Item -LiteralPath $tempRcw -Destination $rcw -Force
        Copy-Item -LiteralPath $tempAx -Destination $ax -Force
        Set-Content -LiteralPath $stamp -Value $fingerprint -Encoding Ascii
    }
    finally {
        Remove-Item -LiteralPath $working -Recurse -Force -ErrorAction SilentlyContinue
    }
}
finally {
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
