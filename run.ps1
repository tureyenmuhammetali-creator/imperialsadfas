$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# Eski ImperialVip surecini durdur (port 51000 + exe dosya kilidi sorununu onle)
Write-Host "Eski sunucu kontrol ediliyor..." -ForegroundColor Yellow
$procs = Get-Process -Name "ImperialVip" -ErrorAction SilentlyContinue
if ($procs) {
    $procs | Stop-Process -Force
    Start-Sleep -Seconds 2
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
Write-Host "Sunucu baslatiliyor: http://localhost:51000" -ForegroundColor Green
dotnet run --project ImperialVip.csproj --urls "http://localhost:51000"
