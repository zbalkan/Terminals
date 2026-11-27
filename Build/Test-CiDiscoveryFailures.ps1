$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$fixture = Join-Path $env:RUNNER_TEMP "ci-discovery-$([Guid]::NewGuid().ToString('N'))"
$names = @('TERMINALS_VSTEST_PATH', 'NUGET_PACKAGES', 'MSTEST_VERSION', 'RUNNER_TEMP', 'CI_DISCOVERY_CASE')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
function Assert-Rejected([string]$Case, [string]$Expected) {
    $env:CI_DISCOVERY_CASE = $Case
    $failure = $null
    try { & (Join-Path $PSScriptRoot 'Test-DiscoveryPartition.ps1') | Out-Host } catch { $failure = $_ }
    if (-not $failure -or $failure.Exception.Message -notlike "*$Expected*") {
        throw "Discovery case '$Case' expected '$Expected', received '$failure'"
    }
    $remaining = @(Get-ChildItem -LiteralPath $env:RUNNER_TEMP -Filter 'discovery-*.txt' -File)
    if ($remaining.Count -ne 0) { throw "Discovery temporary files survived case '$Case': $($remaining.Name -join ', ')" }
    $global:LASTEXITCODE = 0
    Write-Host "Discovery negative control passed: $Case"
}
try {
    New-Item -ItemType Directory -Path "$fixture/temp", "$fixture/packages/mstest.testadapter/fixture/net462" -Force | Out-Null
    New-Item -ItemType File -Path "$fixture/packages/mstest.testadapter/fixture/net462/MSTest.TestAdapter.dll" | Out-Null
    Set-Content -LiteralPath "$fixture/Fixture.cs" -Value @'
using System;
using System.IO;
public static class Fixture {
    public static int Main(string[] args) {
        string file = null, filter = null;
        foreach (string arg in args) {
            if (arg.StartsWith("/ListTestsTargetPath:", StringComparison.OrdinalIgnoreCase)) file = arg.Substring(21);
            if (arg.StartsWith("/TestCaseFilter:", StringComparison.OrdinalIgnoreCase)) filter = arg.Substring(16);
        }
        if (file == null) return 31;
        string current = filter == null ? "all" :
            filter == "TestCategory=RasAbi" ? "abi" :
            filter.Contains("SqlPersisted|") ? "database" : "general";
        string testCase = Environment.GetEnvironmentVariable("CI_DISCOVERY_CASE");
        if (testCase == "native-failure" && current == "database") return 17;
        if (testCase == "empty" && current == "general") { File.WriteAllText(file, " "); return 0; }
        string[] tests = current == "all" ? new[] { "A", "B", "C" } :
            current == "general" ? new[] { "A" } :
            current == "database" ? new[] { "B" } : new[] { "C" };
        if (testCase == "duplicate" && current == "database") tests = new[] { "A" };
        if (testCase == "missing" && current == "database") tests = new[] { "D" };
        File.WriteAllLines(file, tests);
        return 0;
    }
}
'@
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $exe = Join-Path $fixture 'vstest.console.exe'
    $sourceFile = Join-Path $fixture 'Fixture.cs'
    & $compiler /nologo /target:exe "/out:$exe" $sourceFile
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Discovery fixture compilation failed' }
    $env:TERMINALS_VSTEST_PATH = $exe
    $env:NUGET_PACKAGES = Join-Path $fixture 'packages'
    $env:MSTEST_VERSION = 'fixture'
    $env:RUNNER_TEMP = Join-Path $fixture 'temp'
    Push-Location $fixture
    try {
        $env:CI_DISCOVERY_CASE = 'valid'
        & (Join-Path $PSScriptRoot 'Test-DiscoveryPartition.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'Valid discovery left a failing native status' }
        if (@(Get-ChildItem -LiteralPath $env:RUNNER_TEMP -Filter 'discovery-*.txt' -File).Count -ne 0) { throw 'Valid discovery leaked temporary files' }
        Assert-Rejected 'duplicate' 'Duplicate discovered test names'
        Assert-Rejected 'missing' 'Test partition mismatch'
        Assert-Rejected 'empty' 'Empty discovery: general'
        Assert-Rejected 'native-failure' 'Discovery failed: database'
    } finally { Pop-Location }
} finally {
    foreach ($name in $names) {
        if ($null -eq $previous[$name]) {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        } else {
            [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
        }
    }
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
