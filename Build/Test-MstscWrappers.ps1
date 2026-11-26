$ErrorActionPreference = 'Stop'
$dir = Join-Path $PWD 'Build/Generated/Mstsc'
foreach ($name in @('MSTSCLib.dll', 'AxInterop.MSTSCLib.dll')) {
  $path = Join-Path $dir $name
  if (-not (Test-Path -LiteralPath $path)) { throw "Expected generated COM wrapper: $path" }
  Write-Host "Generated: $path"
}
