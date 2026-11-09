# Creates a clean database from the maintained SQL schema on a disposable LocalDB
# instance. No checked-in MDF/LDF file is attached or modified.
$ErrorActionPreference = 'Stop'
$schema = Join-Path $PSScriptRoot '../../../Resources/Database/Terminals_CreateDatabase_v3.0.sql'
if (-not (Test-Path -LiteralPath $schema)) { throw "Missing SQL schema: $schema" }
$connection = [System.Data.SqlClient.SqlConnection]::new('Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;')
try {
    $connection.Open()
    $version = $connection.CreateCommand()
    $version.CommandText = 'SELECT @@VERSION'
    Write-Output ("SQL Server LocalDB: " + $version.ExecuteScalar())
    $batches = [regex]::Split([System.IO.File]::ReadAllText((Resolve-Path $schema)), '(?im)^\s*GO\s*(?:--[^\r\n]*)?$')
    $index = 0
    foreach ($batch in $batches) {
        $index++
        if ([string]::IsNullOrWhiteSpace($batch)) { continue }
        $command = $connection.CreateCommand()
        $command.CommandText = $batch
        $command.CommandTimeout = 120
        try {
            [void]$command.ExecuteNonQuery()
        } catch {
            $preview = $batch.Trim()
            if ($preview.Length -gt 160) { $preview = $preview.Substring(0, 160) }
            throw "Schema batch $index failed ($preview): $($_.Exception.Message)"
        } finally {
            $command.Dispose()
        }
    }
    $verify = $connection.CreateCommand()
    $verify.CommandText = "SELECT COUNT(*) FROM sys.procedures WHERE schema_id = SCHEMA_ID('dbo')"
    $procedureCount = [int]$verify.ExecuteScalar()
    if ($procedureCount -lt 35) { throw "Incomplete SQL schema: expected 35 stored procedures, found $procedureCount" }
    $verify.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo')"
    $tableCount = [int]$verify.ExecuteScalar()
    if ($tableCount -lt 10) { throw "Incomplete SQL schema: expected 10 tables, found $tableCount" }
    Write-Output "SQL schema initialized: $tableCount tables, $procedureCount procedures."
} finally {
    $connection.Dispose()
}
