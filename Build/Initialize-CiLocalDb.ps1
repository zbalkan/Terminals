$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$instance = 'MSSQLLocalDB'
$provisioningStarted = Get-Date
$directory = Join-Path $env:RUNNER_TEMP 'terminals-localdb'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$msi = Join-Path $directory 'SqlLocalDB.msi'
$log = Join-Path $directory 'install.log'
$uri = 'https://download.microsoft.com/download/7/c/1/7c14e92e-bdcb-4f89-b7cf-93543e7112d1/SqlLocalDB.msi'
try {
    Invoke-WebRequest -Uri $uri -OutFile $msi
    $signature = Get-AuthenticodeSignature -LiteralPath $msi
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(?:^|,\s*)O=Microsoft Corporation(?:,|$)') {
        throw "LocalDB installer signature validation failed: $($signature.Status)"
    }
    $install = Start-Process -FilePath 'msiexec.exe' -ArgumentList "/i `"$msi`" /qn /norestart /l*v `"$log`" IACCEPTSQLLOCALDBLICENSETERMS=YES" -Wait -PassThru
    if ($install.ExitCode -notin @(0, 3010)) { throw "LocalDB MSI installation failed (exit $($install.ExitCode))" }
    if ($install.ExitCode -eq 3010) { throw 'LocalDB installation requires a restart; CI cannot continue reliably' }
    $localdb = Join-Path $env:ProgramFiles 'Microsoft SQL Server/150/Tools/Binn/SqlLocalDB.exe'
    if (-not (Test-Path -LiteralPath $localdb -PathType Leaf)) { throw "Missing SQL Server 2019 LocalDB executable: $localdb" }
    & $localdb info $instance
    if ($LASTEXITCODE -ne 0) {
        & $localdb create $instance 15.0
        if ($LASTEXITCODE -ne 0) { throw "LocalDB instance creation failed (exit $LASTEXITCODE)" }
    }
    $startOutput = & $localdb start $instance 2>&1
    $startExitCode = $LASTEXITCODE
    $startOutput | Out-Host
    if ($startExitCode -ne 0 -or ($startOutput -join ' ') -match 'failed|cannot create') {
        throw "LocalDB instance startup failed (exit $startExitCode): $($startOutput -join ' ')"
    }
    $lastError = $null
    for ($attempt = 1; $attempt -le 12; $attempt++) {
        $connection = [System.Data.SqlClient.SqlConnection]::new('Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;Connect Timeout=5;Pooling=False;')
        try {
            $connection.Open()
            $command = $connection.CreateCommand()
            try {
                $command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int)"
                $major = [int]$command.ExecuteScalar()
            } finally { $command.Dispose() }
            if ($major -ne 15) { throw "Expected SQL Server 2019 (major 15), found $major" }
            Write-Host "SQL Server LocalDB 2019 ready on attempt $attempt."
            return
        } catch {
            $lastError = $_
            Write-Warning "LocalDB readiness attempt $attempt failed: $($_.Exception.Message)"
        } finally { $connection.Dispose() }
        if ($attempt -lt 12) { Start-Sleep -Seconds 5 }
    }
    throw "LocalDB readiness failed after 12 attempts: $lastError"
} catch {
    # Collect runtime diagnostics without replacing the original provisioning failure.
    try {
        if ($localdb -and (Test-Path -LiteralPath $localdb -PathType Leaf)) {
            & $localdb versions | Out-Host
            & $localdb info $instance | Out-Host
        }
        $instanceDirectory = Join-Path $env:LOCALAPPDATA "Microsoft/SQL Server Local DB/Instances/$instance"
        $errorLog = Join-Path $instanceDirectory 'error.log'
        if (Test-Path -LiteralPath $errorLog -PathType Leaf) {
            Write-Host 'LocalDB engine error log:'
            Get-Content -LiteralPath $errorLog -Tail 100 | Out-Host
        } else {
            Write-Warning "LocalDB engine error log unavailable: $errorLog"
        }
    } catch {
        Write-Warning "LocalDB runtime diagnostic collection failed: $($_.Exception.Message)"
    }
    try {
        $events = @(Get-WinEvent -FilterHashtable @{ LogName = 'Application'; StartTime = $provisioningStarted } -ErrorAction Stop |
            Where-Object { $_.ProviderName -match 'SQL|LocalDB' -or $_.Message -match 'sqlservr|LocalDB' } |
            Select-Object -First 50)
        if ($events.Count -gt 0) {
            $events | Format-List TimeCreated, Id, ProviderName, LevelDisplayName, Message | Out-Host
        } else { Write-Warning 'No matching LocalDB Application events found.' }
    } catch {
        Write-Warning "LocalDB Application event collection failed: $($_.Exception.Message)"
    }
    if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 100 | Out-Host }
    throw
} finally {
    if (Test-Path -LiteralPath $msi) { Remove-Item -LiteralPath $msi -Force }
}
