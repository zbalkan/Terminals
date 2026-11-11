# Compile the non-MSI Inno Setup distribution using the manifest-prepared payload.
param(
    [string]$CompilerPath = $env:INNO_ISCC_PATH,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'Output')
)
$ErrorActionPreference = 'Stop'
if (-not $CompilerPath) {
    foreach ($candidate in @(
        (Join-Path $env:ProgramFiles 'Inno Setup 7/ISCC.exe'),
        (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Inno Setup 7/ISCC.exe')
    )) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $CompilerPath = $candidate
            break
        }
    }
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
    throw 'Inno Setup 7 compiler not found; set INNO_ISCC_PATH'
}
$info = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Source/Terminals/Properties/Common.AssemblyInfo.cs') -Raw
if ($info -notmatch 'AssemblyVersion\("([^"]+)"\)') { throw 'AssemblyVersion not found' }
$version = $Matches[1] -replace '\.\*$', ''
$output = [IO.Path]::GetFullPath($OutputDirectory)
$staging = Join-Path $output '.installer-staging'
if (-not (Test-Path -LiteralPath (Join-Path $staging 'Terminals.exe'))) {
    throw "Installer staging not generated: $staging. Run PackOutput.ps1 first."
}
$script = Join-Path $PSScriptRoot '../Source/Setup/Terminals.iss'
& $CompilerPath "/DAppVersion=$version" "/DPayloadDir=$staging" "/O$output" $script
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compiler exited with $LASTEXITCODE" }
$setup = Join-Path $output "TerminalsSetup_$version.exe"
if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw "Inno Setup artifact missing: $setup" }
Write-Host "Created $setup"
