$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Get-MSTestAdapter.ps1')
$adapter = Get-MSTestAdapter
vstest.console.exe "Source\Tests\bin\Release\Tests.dll" "/TestAdapterPath:$($adapter.DirectoryName)" "/TestCaseFilter:$env:TEST_FILTER" '/logger:trx;LogFileName=test_results.trx' -- RunConfiguration.TreatNoTestsAsError=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path -LiteralPath 'TestResults/test_results.trx' -PathType Leaf)) { throw 'Missing expected test_results.trx' }
