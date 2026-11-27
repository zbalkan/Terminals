$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
foreach ($name in @('MSTSCLib.dll', 'AxInterop.MSTSCLib.dll')) {
    $generated = Join-Path $root "Build/Generated/Mstsc/$name"
    $vendored = Join-Path $root "Resources/Libraries/mstsc/$name"
    if (-not (Test-Path -LiteralPath $generated -PathType Leaf)) { throw "Expected generated COM wrapper: $generated" }
    if ((Get-Item -LiteralPath $generated).Length -eq 0) { throw "Empty generated COM wrapper: $generated" }
    if (Test-Path -LiteralPath $vendored) { throw "Vendored MSTSC wrapper still present: $vendored" }
    Write-Host "Generated: $generated"
}
