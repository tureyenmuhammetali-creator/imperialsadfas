# ImperialVip sunucusunu durdur (port + dosya kilidi sorununu coz)
Write-Host "ImperialVip durduruluyor..." -ForegroundColor Yellow
$count = 0
Get-Process -Name "ImperialVip" -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-Process -Id $_.Id -Force
    Write-Host "  PID $($_.Id) durduruldu" -ForegroundColor Gray
    $count++
}
if ($count -eq 0) {
    Write-Host "  Calisan ImperialVip sureci bulunamadi." -ForegroundColor Gray
} else {
    Write-Host "Tamam. Simdi projeyi yeniden derleyip calistirabilirsiniz." -ForegroundColor Green
}
