$ErrorActionPreference = 'Stop'
$fixture = Join-Path $env:RUNNER_TEMP "ci-native-$([Guid]::NewGuid().ToString('N'))"
$names = @('TERMINALS_VSTEST_PATH', 'NUGET_PACKAGES', 'MSTEST_VERSION', 'TEST_FILTER', 'CI_FIXTURE_EXIT')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
function Assert-Rejected([scriptblock]$Action, [string]$Expected) {
    $failure = $null
    try { & $Action | Out-Host } catch { $failure = $_ }
    if (-not $failure -or $failure.Exception.Message -notlike "*$Expected*") {
        throw "Expected '$Expected', received '$failure'"
    }
    Write-Host "Negative control passed: $Expected"
}
try {
    New-Item -ItemType Directory -Path "$fixture/TestResults", "$fixture/packages/mstest.testadapter/fixture/net462" -Force | Out-Null
    New-Item -ItemType File -Path "$fixture/packages/mstest.testadapter/fixture/net462/MSTest.TestAdapter.dll" | Out-Null
    $source = Join-Path $fixture 'Fixture.cs'
    Set-Content -LiteralPath $source -Value @'
using System;
public static class Fixture {
    public static int Main(string[] args) {
        return int.Parse(Environment.GetEnvironmentVariable("CI_FIXTURE_EXIT"));
    }
}
'@
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $exe = Join-Path $fixture 'vstest.console.exe'
    & $compiler /nologo /target:exe "/out:$exe" $source
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Native fixture compilation failed' }
    $env:TERMINALS_VSTEST_PATH = $exe
    $env:NUGET_PACKAGES = Join-Path $fixture 'packages'
    $env:MSTEST_VERSION = 'fixture'
    $env:TEST_FILTER = 'TestCategory=Fixture'
    $script = Join-Path $PSScriptRoot 'Invoke-StandardTests.ps1'
    Push-Location $fixture
    try {
        $env:CI_FIXTURE_EXIT = '0'
        Set-Content -LiteralPath 'TestResults/test_results.trx' -Value 'stale'
        Assert-Rejected { & $script } 'Missing expected test_results.trx'
        if (Test-Path -LiteralPath 'TestResults/test_results.trx') { throw 'Stale TRX survived execution' }
        $env:CI_FIXTURE_EXIT = '17'
        Assert-Rejected { & $script } 'Standard test execution failed (exit 17)'
        # The nonzero native exit is expected and has been asserted.
        $global:LASTEXITCODE = 0
        Remove-Item Env:TEST_FILTER
        Assert-Rejected { & $script } 'Missing TEST_FILTER'
    } finally { Pop-Location }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
