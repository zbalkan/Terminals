$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
msbuild $env:SLN_FILE_PATH -t:Rebuild '-p:Configuration=Release' '-p:Platform=Any CPU' -m
if ($LASTEXITCODE -ne 0) { throw "Release build failed (exit $LASTEXITCODE)" }
