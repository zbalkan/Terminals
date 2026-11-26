function Get-MSTestAdapter {
    $packageRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $adapterRoot = Join-Path $packageRoot "mstest.testadapter/$env:MSTEST_VERSION"
    if (-not (Test-Path -LiteralPath $adapterRoot -PathType Container)) {
        throw "MSTest.TestAdapter $env:MSTEST_VERSION was not restored to $adapterRoot"
    }
    $adapter = Get-ChildItem -LiteralPath $adapterRoot -Filter MSTest.TestAdapter.dll -Recurse -File |
        Where-Object { $_.FullName -match '[\\/]net462[\\/]' } | Select-Object -First 1
    if (-not $adapter) { throw "MSTest .NET Framework adapter not found under $adapterRoot" }
    return $adapter
}
