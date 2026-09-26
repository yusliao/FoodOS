[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("accepted", "pending", "rejected", "shortage")]
    [string]$Mode
)

$ErrorActionPreference = "Stop"
$response = Invoke-RestMethod `
    -Method Post `
    -Uri "http://127.0.0.1:19083/demo/mode" `
    -ContentType "application/json" `
    -Body (@{ mode = $Mode } | ConvertTo-Json -Compress)
Write-Host "Demo WMS mode: $($response.mode)"
