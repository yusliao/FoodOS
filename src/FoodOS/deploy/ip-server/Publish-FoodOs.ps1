param(
    [ValidatePattern('^[A-Za-z0-9._-]+$')]
    [string]$ReleaseId = (Get-Date -Format 'yyyyMMdd-HHmmss')
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$releaseRoot = Join-Path $PSScriptRoot "releases/$ReleaseId"

if (Test-Path -LiteralPath $releaseRoot) {
    throw "Release directory already exists: $releaseRoot"
}

foreach ($name in @('api', 'migrator', 'admin', 'dashboard')) {
    New-Item -ItemType Directory -Path (Join-Path $releaseRoot $name) -Force | Out-Null
}

function Assert-ExitCode([string]$step) {
    if ($LASTEXITCODE -ne 0) {
        throw "$step failed with exit code $LASTEXITCODE. Partial output remains at $releaseRoot."
    }
}

Push-Location $projectRoot
try {
    & dotnet publish 'src/Host/FoodOS.Api/FoodOS.Api.csproj' -c Release -r linux-x64 --self-contained false -o (Join-Path $releaseRoot 'api')
    Assert-ExitCode 'API publish'

    & dotnet publish 'src/Host/FoodOS.DbMigrator/FoodOS.DbMigrator.csproj' -c Release -r linux-x64 --self-contained false -o (Join-Path $releaseRoot 'migrator')
    Assert-ExitCode 'DbMigrator publish'

    foreach ($app in @('admin', 'dashboard')) {
        $appRoot = Join-Path $projectRoot "clients/$app"
        Push-Location $appRoot
        try {
            & npm ci
            Assert-ExitCode "$app npm ci"
            & npm run build
            Assert-ExitCode "$app build"
            Copy-Item -Path (Join-Path $appRoot 'dist/*') -Destination (Join-Path $releaseRoot $app) -Recurse -Force
        }
        finally {
            Pop-Location
        }
    }
}
finally {
    Pop-Location
}

foreach ($required in @('api/FoodOS.Api.dll', 'migrator/FoodOS.DbMigrator.dll', 'admin/index.html', 'dashboard/index.html')) {
    if (-not (Test-Path -LiteralPath (Join-Path $releaseRoot $required))) {
        throw "Missing published file: $required"
    }
}

Write-Host "Release ready: $releaseRoot"
Write-Host "Upload this release directory to /opt/foodos/releases/$ReleaseId and set FOODOS_RELEASE_DIR=./releases/$ReleaseId in foodos.env."
