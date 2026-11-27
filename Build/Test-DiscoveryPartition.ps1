$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Get-MSTestAdapter.ps1')
$adapter = Get-MSTestAdapter
. (Join-Path $PSScriptRoot 'Get-VSTestExecutable.ps1')
$vstest = Get-VSTestExecutable
function Get-DiscoveredTests([string]$name, [string]$filter) {
  $file = Join-Path $env:RUNNER_TEMP "discovery-$name-$([Guid]::NewGuid().ToString('N')).txt"
  try {
    $arguments = @('Source\Tests\bin\Release\Tests.dll', "/TestAdapterPath:$($adapter.DirectoryName)", '/ListFullyQualifiedTests', "/ListTestsTargetPath:$file")
    if ($filter) { $arguments += "/TestCaseFilter:$filter" }
    & $vstest @arguments | Out-Host
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Discovery failed: $name" }
    $tests = @(Get-Content -LiteralPath $file | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() })
    if ($tests.Count -eq 0) { throw "Empty discovery: $name" }
    return $tests
  } finally {
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
  }
}
$all = @(Get-DiscoveredTests 'all' '')
$general = @(Get-DiscoveredTests 'general' 'FullyQualifiedName!~Tests.SqlPersisted&TestCategory!=Database&TestCategory!=RasAbi')
$database = @(Get-DiscoveredTests 'database' 'FullyQualifiedName~Tests.SqlPersisted|TestCategory=Database')
$abi = @(Get-DiscoveredTests 'abi' 'TestCategory=RasAbi')
$combined = @($general + $database + $abi)
$duplicates = @($combined | Group-Object -CaseSensitive | Where-Object Count -gt 1)
if ($duplicates.Count -gt 0) { throw "Duplicate discovered test names: $($duplicates.Name -join ', ')" }
$difference = @(Compare-Object -ReferenceObject $all -DifferenceObject $combined -CaseSensitive)
if ($difference.Count -gt 0) { throw "Test partition mismatch: $($difference | Out-String)" }
Write-Host "Discovery partition: $($all.Count) total; $($general.Count) general; $($database.Count) database; $($abi.Count) ABI"
