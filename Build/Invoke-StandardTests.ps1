$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Get-MSTestAdapter.ps1')
$adapter = Get-MSTestAdapter
$result = 'TestResults/test_results.trx'
if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result -Force }
if (-not $env:TEST_FILTER) { throw 'Missing TEST_FILTER' }
vstest.console.exe "Source\Tests\bin\Release\Tests.dll" "/TestAdapterPath:$($adapter.DirectoryName)" "/TestCaseFilter:$env:TEST_FILTER" '/logger:trx;LogFileName=test_results.trx' -- RunConfiguration.TreatNoTestsAsError=true
if ($LASTEXITCODE -ne 0) { throw "Standard test execution failed (exit $LASTEXITCODE)" }
if (-not (Test-Path -LiteralPath 'TestResults/test_results.trx' -PathType Leaf)) { throw 'Missing expected test_results.trx' }
