[CmdletBinding()]
param(
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"
$dockerDir = $PSScriptRoot
$envFile = Join-Path $dockerDir ".env.demo"
if (-not (Test-Path -LiteralPath $envFile)) {
    throw "Demo environment file does not exist: $envFile"
}

$arguments = @(
    "compose", "--env-file", $envFile, "-p", "foodos-demo", "--profile", "demo",
    "-f", (Join-Path $dockerDir "docker-compose.yml"),
    "-f", (Join-Path $dockerDir "docker-compose.demo.yml"),
    "down"
)
if ($RemoveData) {
    $arguments += "--volumes"
}

& docker @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Failed to stop the FoodOS demo stack."
}

if ($RemoveData) {
    Write-Host "FoodOS demo containers and demo-only volumes were removed. The ignored .env.demo file was retained."
} else {
    Write-Host "FoodOS demo containers were stopped. Demo data volumes were retained."
}
