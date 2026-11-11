# Build the application, portable ZIP and Inno Setup EXE from a clean output tree.
# Use Visual Studio Developer PowerShell so MSBuild is available on PATH.
$ErrorActionPreference = 'Stop'
$msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
if ($null -eq $msbuild) {
    throw 'MSBuild is not on PATH. Open Visual Studio Developer PowerShell before running Build/Build.ps1.'
}
# Validate the same compiler locations as Build-Inno.ps1 before deleting old outputs.
$compiler = $env:INNO_ISCC_PATH
if (-not $compiler) {
    foreach ($candidate in @(
        (Join-Path $env:ProgramFiles 'Inno Setup 7/ISCC.exe'),
        (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Inno Setup 7/ISCC.exe')
    )) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $compiler = $candidate
            break
        }
    }
}
if (-not $compiler -or -not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'Inno Setup 7 compiler not found. Install Inno Setup 7 or set INNO_ISCC_PATH to ISCC.exe before running Build/Build.ps1.'
}
$output = Join-Path $PSScriptRoot 'Output'
if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}
Push-Location (Join-Path $PSScriptRoot '..')
try {
    & $msbuild.Source Source/Terminals.sln -t:Restore -property:RestorePackagesConfig=true -property:Configuration=Release
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
    & $msbuild.Source Source/Terminals.sln -t:Rebuild '-p:Configuration=Release' '-p:Platform=Any CPU' -m
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed' }
    & (Join-Path $PSScriptRoot 'PackOutput.ps1')
    & (Join-Path $PSScriptRoot 'Build-Inno.ps1') -CompilerPath $compiler
}
finally { Pop-Location }
