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

# Restrict analysis to correctness-oriented rules; do not enforce legacy formatting.
$analyzerVersion = '1.25.0'
Install-Module -Name PSScriptAnalyzer -RequiredVersion $analyzerVersion -Repository PSGallery -Scope CurrentUser -Force -ErrorAction Stop
Import-Module PSScriptAnalyzer -RequiredVersion $analyzerVersion -Force
$rules = @('PSAvoidUsingInvokeExpression', 'PSAvoidDefaultValueForMandatoryParameter', 'PSAvoidReservedWordsAsFunctionNames')
$available = @(Get-ScriptAnalyzerRule | ForEach-Object RuleName)
foreach ($rule in $rules) {
    if ($rule -notin $available) { throw "Missing analyzer rule: $rule" }
}
$findings = @(foreach ($file in $files) {
    Invoke-ScriptAnalyzer -Path (Join-Path $root $file) -IncludeRule $rules
})
if ($findings.Count -gt 0) {
    $findings | Format-Table RuleName, ScriptName, Line, Message -Wrap | Out-Host
    throw "PowerShell analysis failed ($($findings.Count) findings)"
}
# Verify the selected analyzer actually detects a prohibited construct.
$probe = @(Invoke-ScriptAnalyzer -ScriptDefinition 'Invoke-Expression $inputText' -IncludeRule 'PSAvoidUsingInvokeExpression')
if ($probe.Count -eq 0) { throw 'Script analyzer negative control was not detected' }
$probeTokens = $null
$probeErrors = $null
$null = [System.Management.Automation.Language.Parser]::ParseInput('if (', [ref]$probeTokens, [ref]$probeErrors)
if ($probeErrors.Count -eq 0) { throw 'PowerShell parser negative control was not detected' }
Write-Host "PowerShell analysis passed ($($rules.Count) rules); negative controls detected."

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
        $invalid = Join-Path $temporary 'invalid.yml'
        Set-Content -LiteralPath $invalid -Value "on: push`njobs: {}"
        & $executable -color -shellcheck= -pyflakes= $invalid
        if ($LASTEXITCODE -eq 0) { throw 'actionlint negative control was not detected' }
        Write-Host 'actionlint negative control detected.'
        $global:LASTEXITCODE = 0
    }
    finally { Pop-Location }
}
finally { Remove-Item -LiteralPath $temporary -Recurse -Force }
