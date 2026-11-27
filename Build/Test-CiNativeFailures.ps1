$ErrorActionPreference = 'Stop'
$fixture = Join-Path $env:RUNNER_TEMP "ci-native-$([Guid]::NewGuid().ToString('N'))"
$names = @('TERMINALS_VSTEST_PATH', 'NUGET_PACKAGES', 'MSTEST_VERSION', 'TEST_FILTER', 'CI_FIXTURE_EXIT', 'CI_FIXTURE_MODE', 'TERMINALS_RAS_ABI_PLATFORM')
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
using System.IO;
public static class Fixture {
    public static int Main(string[] args) {
        if (Environment.GetEnvironmentVariable("CI_FIXTURE_MODE") == "abi") {
            string platform = Environment.GetEnvironmentVariable("TERMINALS_RAS_ABI_PLATFORM");
            File.AppendAllText("abi-executions.txt", platform + Environment.NewLine);
            if (platform == "x86") return 17;
            if (platform != "x64") return 19;
            File.WriteAllText("TestResults/ras_abi_x64.trx", "fresh");
            return 0;
        }
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
        $env:CI_FIXTURE_MODE = 'standard'
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
        $env:CI_FIXTURE_MODE = 'abi'
        $env:TERMINALS_RAS_ABI_PLATFORM = 'original'
        Set-Content -LiteralPath 'TestResults/ras_abi_x86.trx' -Value 'stale'
        Set-Content -LiteralPath 'TestResults/ras_abi_x64.trx' -Value 'stale'
        $abiScript = Join-Path $PSScriptRoot 'Test-RasAbi.ps1'
        Assert-Rejected { & $abiScript } 'RAS ABI validation failed on x86 (exit 17); Missing expected RAS ABI TRX for x86'
        if ((Get-Content -LiteralPath 'abi-executions.txt') -join ',' -cne 'x86,x64') { throw 'ABI execution sequence mismatch' }
        if (Test-Path -LiteralPath 'TestResults/ras_abi_x86.trx') { throw 'Stale x86 TRX survived execution' }
        if ((Get-Content -LiteralPath 'TestResults/ras_abi_x64.trx' -Raw) -cne 'fresh') { throw 'Fresh x64 TRX missing' }
        if ($env:TERMINALS_RAS_ABI_PLATFORM -cne 'original') { throw 'Original ABI environment value not restored' }
        Remove-Item Env:TERMINALS_RAS_ABI_PLATFORM
        Remove-Item -LiteralPath 'abi-executions.txt'
        Assert-Rejected { & $abiScript } 'RAS ABI validation failed on x86 (exit 17); Missing expected RAS ABI TRX for x86'
        if (Test-Path Env:TERMINALS_RAS_ABI_PLATFORM) { throw 'Previously absent ABI environment variable was not removed' }
        if ((Get-Content -LiteralPath 'abi-executions.txt') -join ',' -cne 'x86,x64') { throw 'ABI execution sequence mismatch' }
        $global:LASTEXITCODE = 0
        Write-Host 'Verified independent ABI execution, fresh output and environment restoration.'
    } finally { Pop-Location }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
