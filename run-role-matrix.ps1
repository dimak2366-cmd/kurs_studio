$ErrorActionPreference = "SilentlyContinue"
$server = "http://localhost:5155"
$users = @{
    "admin"      = "Admin123!"
    "director"   = "Director123!"
    "economist"  = "Economist123!"
    "head"       = "Head123!"
    "accountant" = "Accountant123!"
}
# Замените id на реальные из вашего демо-сида после reset-db
$endpoints = @(
    "/api/analysis/deviation/1",
    "/api/analysis/materials/1/2",
    "/api/limits/1",
    "/api/limits/1/check",
    "/api/periods",
    "/api/reports",
    "/api/decisions",
    "/api/users",
    "/api/settings",
    "/api/backup"
)

foreach ($role in $users.Keys) {
    $body = @{ login = $role; password = $users[$role] } | ConvertTo-Json
    $login = Invoke-RestMethod -Uri "$server/api/auth/login" -Method Post `
                                -Body $body -ContentType "application/json"
    if (-not $login.token) { Write-Host "LOGIN FAILED: $role"; continue }
    Write-Host "`n=== $role ==="
    foreach ($ep in $endpoints) {
        try {
            $r = Invoke-WebRequest -Uri "$server$ep" -Method Get `
                                   -Headers @{ Authorization = "Bearer $($login.token)" }
            Write-Host ("  {0,-32} {1}" -f $ep, $r.StatusCode)
        } catch {
            $code = $_.Exception.Response.StatusCode.value__
            Write-Host ("  {0,-32} {1}" -f $ep, $code)
        }
    }
}
