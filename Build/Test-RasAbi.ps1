$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Get-MSTestAdapter.ps1')
$adapter = Get-MSTestAdapter
. (Join-Path $PSScriptRoot 'Get-VSTestExecutable.ps1')
$vstest = Get-VSTestExecutable
$failures = @()
foreach ($platform in @('x86', 'x64')) {
  $result = "TestResults/ras_abi_$platform.trx"
  if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result -Force }
  $env:TERMINALS_RAS_ABI_PLATFORM = $platform
  & $vstest "Source\Tests\bin\Release\Tests.dll" "/TestAdapterPath:$($adapter.DirectoryName)" "/Platform:$platform" '/TestCaseFilter:TestCategory=RasAbi' "/logger:trx;LogFileName=ras_abi_$platform.trx" -- RunConfiguration.TreatNoTestsAsError=true
  if ($LASTEXITCODE -ne 0) { $failures += "RAS ABI validation failed on $platform (exit $LASTEXITCODE)" }
  if (-not (Test-Path -LiteralPath "TestResults/ras_abi_$platform.trx" -PathType Leaf)) { $failures += "Missing expected RAS ABI TRX for $platform" }
}
Remove-Item Env:TERMINALS_RAS_ABI_PLATFORM -ErrorAction SilentlyContinue
if ($failures.Count -gt 0) { throw ($failures -join "; ") }
