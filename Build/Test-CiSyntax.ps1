$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = Split-Path -Parent $PSScriptRoot
$files = @(git -C $root ls-files -- '*.ps1')
if ($LASTEXITCODE -ne 0 -or $files.Count -eq 0) { throw 'Unable to enumerate tracked PowerShell scripts' }
$problems = @()
foreach ($file in $files) {
    $tokens = $null
    $errors = $null
    $null = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $root $file), [ref]$tokens, [ref]$errors)
    foreach ($problem in $errors) {
        $problems += "${file}:$($problem.Extent.StartLineNumber): $($problem.Message)"
    }
}
if ($problems.Count -gt 0) { throw ($problems -join [Environment]::NewLine) }
Write-Host "Parsed $($files.Count) tracked PowerShell scripts."

$version = '1.7.12'
$expected = '6e7241b51e6817ea6a047693d8e6fed13b31819c9a0dd6c5a726e1592d22f6e9'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ("terminals-actionlint-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    $archive = Join-Path $temporary 'actionlint.zip'
    Invoke-WebRequest -Uri "https://github.com/rhysd/actionlint/releases/download/v$version/actionlint_${version}_windows_amd64.zip" -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) { throw 'actionlint archive SHA-256 mismatch' }
    Expand-Archive -LiteralPath $archive -DestinationPath $temporary
    $executable = Join-Path $temporary 'actionlint.exe'
    Push-Location $root
    try {
        & $executable -color -shellcheck= -pyflakes=
        if ($LASTEXITCODE -ne 0) { throw "actionlint failed (exit $LASTEXITCODE)" }
    }
    finally { Pop-Location }
}
finally { Remove-Item -LiteralPath $temporary -Recurse -Force }
