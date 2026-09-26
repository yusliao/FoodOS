[CmdletBinding()]
param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$dockerDir = $PSScriptRoot
$envFile = Join-Path $dockerDir ".env.demo"
$templateFile = Join-Path $dockerDir "demo.env.example"
$composeFiles = @(
    "-f", (Join-Path $dockerDir "docker-compose.yml"),
    "-f", (Join-Path $dockerDir "docker-compose.demo.yml")
)

function New-RandomSecret([int]$byteCount = 32) {
    $bytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes($byteCount)
    return [Convert]::ToHexString($bytes).ToLowerInvariant()
}

function Invoke-DemoCompose([string[]]$Arguments) {
    & docker compose --env-file $envFile -p foodos-demo --profile demo @composeFiles @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $envFile)) {
    $content = Get-Content -Raw -LiteralPath $templateFile
    $values = @{
        JWT_SIGNING_KEY = New-RandomSecret 48
        SEED_ADMIN_PASSWORD = New-RandomSecret 24
        HANGFIRE_PASSWORD = New-RandomSecret 24
        POSTGRES_PASSWORD = New-RandomSecret 24
        REDIS_PASSWORD = New-RandomSecret 24
        MINIO_ROOT_PASSWORD = New-RandomSecret 24
        WMS_SIGNING_SECRET = New-RandomSecret 48
    }
    foreach ($entry in $values.GetEnumerator()) {
        $content = $content -replace "(?m)^$([Regex]::Escape($entry.Key))=.*$", "$($entry.Key)=$($entry.Value)"
    }
    Set-Content -LiteralPath $envFile -Value $content -Encoding utf8NoBOM
    Write-Host "Created ignored local secrets file: $envFile"
}

if (-not $NoBuild) {
    Invoke-DemoCompose @("build", "migrator", "api", "admin", "dashboard", "wms-adapter")
}

Invoke-DemoCompose @("up", "-d", "postgres", "redis", "minio", "minio-init", "migrator")
Invoke-DemoCompose @("run", "--rm", "demo-seeder")
Invoke-DemoCompose @("up", "-d", "wms-adapter", "api", "admin", "dashboard")

& (Join-Path $dockerDir "Test-FoodOsDemo.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "FoodOS demo verification failed."
}

Write-Host ""
Write-Host "FoodOS customer demo is ready:"
Write-Host "  Operator admin : http://localhost:19081"
Write-Host "  Restaurant app : http://localhost:19082"
Write-Host "  API health     : http://localhost:19080/health/ready"
Write-Host "  Demo WMS       : http://localhost:19083/demo/status"
Write-Host "Use the on-screen demo account selector. Shared demo password: Password123!"
