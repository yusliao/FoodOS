[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Wait-Http([string]$Uri, [int]$Seconds = 120) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($Seconds)
    do {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Uri -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                return $response
            }
        } catch {
            Start-Sleep -Seconds 2
        }
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Timed out waiting for $Uri"
}

$api = Wait-Http "http://127.0.0.1:19080/health/ready"
$admin = Wait-Http "http://127.0.0.1:19081/"
$dashboard = Wait-Http "http://127.0.0.1:19082/"
$wms = Wait-Http "http://127.0.0.1:19083/api/v1/health"
$adminConfig = Invoke-RestMethod "http://127.0.0.1:19081/config.json"
$dashboardConfig = Invoke-RestMethod "http://127.0.0.1:19082/config.json"

if ($adminConfig.demoMode -ne $true -or $dashboardConfig.demoMode -ne $true) {
    throw "Demo account selection is not enabled in both frontends."
}
if ($adminConfig.apiBase -ne "http://localhost:19080" -or $dashboardConfig.apiBase -ne "http://localhost:19080") {
    throw "Frontend API URLs do not point at the isolated demo API."
}

$expectedEmails = @(
    "superadmin@root.com", "manager@root.com", "support@root.com", "purchaser@root.com",
    "qc@root.com", "whlead@root.com", "picker@root.com", "dispatch@root.com",
    "driver@root.com", "finance@root.com", "admin@acme.com", "admin@globex.com"
)
$quoted = ($expectedEmails | ForEach-Object { "'$($_.Replace("'", "''"))'" }) -join ","
$sql = 'select lower("Email") from identity."Users" where lower("Email") in (' + $quoted + ') order by 1;'
$actual = $sql | & docker exec -i foodos-demo-postgres sh -lc 'PGPASSWORD="$POSTGRES_PASSWORD" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At'
if ($LASTEXITCODE -ne 0) {
    throw "Could not verify seeded demo users."
}
$missing = $expectedEmails | Where-Object { $_ -notin $actual }
if ($missing.Count -gt 0) {
    throw "Missing demo users: $($missing -join ', ')"
}

$mappingSql = 'select count(*) from wms."Mappings" where lower("Provider")=''demo-wms'' and lower("ConnectionId")=''customer-demo'' and "IsActive"=true;'
$mappingCount = $mappingSql | & docker exec -i foodos-demo-postgres sh -lc 'PGPASSWORD="$POSTGRES_PASSWORD" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At'
if ($LASTEXITCODE -ne 0 -or [int]$mappingCount -lt 4) {
    throw "Demo WMS mappings were not seeded."
}

$projectionSql = 'select count(*) from wms."InventoryBalances" where lower("Provider")=''demo-wms'' and lower("ConnectionId")=''customer-demo'' and "AvailableQuantity">0 and "OccurredAt">now()-interval ''1 day'';'
$projectionCount = $projectionSql | & docker exec -i foodos-demo-postgres sh -lc 'PGPASSWORD="$POSTGRES_PASSWORD" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At'
if ($LASTEXITCODE -ne 0 -or [int]$projectionCount -lt 1) {
    throw "Demo WMS availability projections were not seeded or are stale."
}

Write-Host "FoodOS demo verification passed: API/admin/dashboard/WMS healthy, demoMode enabled, $($expectedEmails.Count) role accounts, $mappingCount WMS mappings and $projectionCount availability projections present."
