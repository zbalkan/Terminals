$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$fixture = Join-Path $env:RUNNER_TEMP "ci-guard-$([Guid]::NewGuid().ToString('N'))"
$previousPath = [Environment]::GetEnvironmentVariable('TERMINALS_VSTEST_PATH', 'Process')
function Assert-Rejected([scriptblock]$Action, [string]$Expected) {
    $failure = $null
    try { & $Action | Out-Host } catch { $failure = $_ }
    if (-not $failure) { throw "Negative control unexpectedly passed: $Expected" }
    if ($failure.Exception.Message -notlike "*$Expected*") { throw "Unexpected failure for '$Expected': $failure" }
    Write-Host "Negative control passed: $Expected"
}
try {
    New-Item -ItemType Directory -Path "$fixture/Build/Generated/Mstsc" -Force | Out-Null
    New-Item -ItemType Directory -Path "$fixture/Resources/Libraries/mstsc" -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Test-MstscWrappers.ps1') -Destination "$fixture/Build"
    $wrapper = Join-Path $fixture 'Build/Test-MstscWrappers.ps1'
    Assert-Rejected { & $wrapper } 'Expected generated COM wrapper'
    New-Item -ItemType File -Path "$fixture/Build/Generated/Mstsc/MSTSCLib.dll" | Out-Null
    Assert-Rejected { & $wrapper } 'Empty generated COM wrapper'
    Set-Content -LiteralPath "$fixture/Build/Generated/Mstsc/MSTSCLib.dll" -Value 'fixture'
    Set-Content -LiteralPath "$fixture/Build/Generated/Mstsc/AxInterop.MSTSCLib.dll" -Value 'fixture'
    & $wrapper
    Set-Content -LiteralPath "$fixture/Resources/Libraries/mstsc/MSTSCLib.dll" -Value 'fixture'
    Assert-Rejected { & $wrapper } 'Vendored MSTSC wrapper still present'
    . (Join-Path $PSScriptRoot 'Get-VSTestExecutable.ps1')
    Remove-Item Env:TERMINALS_VSTEST_PATH -ErrorAction SilentlyContinue
    Assert-Rejected { Get-VSTestExecutable } 'Missing TERMINALS_VSTEST_PATH'
    $env:TERMINALS_VSTEST_PATH = 'relative.exe'
    Assert-Rejected { Get-VSTestExecutable } 'VSTest path must be absolute'
    $env:TERMINALS_VSTEST_PATH = Join-Path $fixture 'missing.exe'
    Assert-Rejected { Get-VSTestExecutable } 'missing.exe'
    $env:TERMINALS_VSTEST_PATH = $wrapper
    Assert-Rejected { Get-VSTestExecutable } 'Invalid VSTest executable'
    Push-Location $fixture
    try {
        git init --quiet
        if ($LASTEXITCODE -ne 0) { throw 'Fixture git init failed' }
        Set-Content -LiteralPath 'allowed.txt' -Value 'fixture'
        git add -- allowed.txt
        if ($LASTEXITCODE -ne 0) { throw 'Fixture git add failed' }
        & (Join-Path $PSScriptRoot 'Test-TrackedArtifacts.ps1')
        git add -f -- Build/Generated/Mstsc/MSTSCLib.dll
        if ($LASTEXITCODE -ne 0) { throw 'Fixture binary staging failed' }
        Assert-Rejected { & (Join-Path $PSScriptRoot 'Test-TrackedArtifacts.ps1') } 'tracked-artifact guard found 1 violation'
    } finally { Pop-Location }
} finally {
    [Environment]::SetEnvironmentVariable('TERMINALS_VSTEST_PATH', $previousPath, 'Process')
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
