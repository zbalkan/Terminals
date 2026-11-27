function Get-VSTestExecutable {
    if (-not $env:TERMINALS_VSTEST_PATH) { throw 'Missing TERMINALS_VSTEST_PATH' }
    if (-not [IO.Path]::IsPathFullyQualified($env:TERMINALS_VSTEST_PATH)) { throw 'VSTest path must be absolute' }
    $file = Get-Item -LiteralPath $env:TERMINALS_VSTEST_PATH -ErrorAction Stop
    if ($file.PSIsContainer -or $file.Name -ne 'vstest.console.exe') { throw "Invalid VSTest executable: $($file.FullName)" }
    return $file.FullName
}
