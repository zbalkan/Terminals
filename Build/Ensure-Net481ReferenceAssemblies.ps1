$ErrorActionPreference = 'Stop'
$destination = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8.1'
if (-not (Test-Path (Join-Path $destination 'mscorlib.dll'))) {
  $packageDir = Join-Path $env:RUNNER_TEMP 'net481-reference'
  nuget install Microsoft.NETFramework.ReferenceAssemblies.net481 -Version $env:NET481_REFERENCE_VERSION -Source https://api.nuget.org/v3/index.json -OutputDirectory $packageDir -ExcludeVersion -NonInteractive
  if ($LASTEXITCODE -ne 0) { throw 'Failed to download .NET Framework 4.8.1 targeting assemblies' }
  $source = Join-Path $packageDir 'Microsoft.NETFramework.ReferenceAssemblies.net481/build/.NETFramework/v4.8.1'
  if (-not (Test-Path (Join-Path $source 'mscorlib.dll'))) { throw 'Missing 4.8.1 reference assemblies in package' }
  New-Item -ItemType Directory -Path $destination -Force | Out-Null
  Copy-Item (Join-Path $source '*') $destination -Recurse -Force
}
