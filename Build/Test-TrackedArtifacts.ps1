# BIN-01: examine only committed paths. Ignore untracked/restored build outputs.
# Do not ban legitimate images, source files, key fixtures, or test archives by default.
$ErrorActionPreference = 'Stop'
$paths = @(git ls-files --cached --full-name)
if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed' }

$violations = @($paths | Where-Object {
    $path = $_ -replace '\\', '/'
    $path -match '(?i)(^|/)(?:bin|obj)/' -or
    $path -match '(?i)\.(?:dll|exe|ocx|msi|pdb|nupkg|vspscc|vssscc)$'
})
if ($violations.Count -gt 0) {
    Write-Host 'Prohibited tracked build artifacts or legacy source-control bindings:'
    $violations | ForEach-Object { Write-Host "  $_" }
    throw "BIN-01 tracked-artifact guard found $($violations.Count) violation(s)."
}
Write-Host "BIN-01: checked $($paths.Count) tracked paths; no prohibited artifacts found."
