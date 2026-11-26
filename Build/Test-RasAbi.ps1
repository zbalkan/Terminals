$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Get-MSTestAdapter.ps1')
$adapter = Get-MSTestAdapter
$failures = @()
foreach ($platform in @('x86', 'x64')) {
  $env:TERMINALS_RAS_ABI_PLATFORM = $platform
  vstest.console.exe "Source\Tests\bin\Release\Tests.dll" "/TestAdapterPath:$($adapter.DirectoryName)" "/Platform:$platform" '/TestCaseFilter:TestCategory=RasAbi' "/logger:trx;LogFileName=ras_abi_$platform.trx" -- RunConfiguration.TreatNoTestsAsError=true
  if ($LASTEXITCODE -ne 0) { $failures += "RAS ABI validation failed on $platform (exit $LASTEXITCODE)" }
  if (-not (Test-Path -LiteralPath "TestResults/ras_abi_$platform.trx" -PathType Leaf)) { $failures += "Missing expected RAS ABI TRX for $platform" }
}
if ($failures.Count -gt 0) { throw ($failures -join "; ") }
