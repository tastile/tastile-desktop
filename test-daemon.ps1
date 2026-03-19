# Test daemon launch from Desktop app
Write-Host "=== Desktop Daemon Test ==="

# Cleanup first
Get-Process -Name TastileDesktop, tastile-daemon, tastile-mock-server -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item "$env:TEMP\tastile-*.log" -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Check port before
Write-Host "`n1. Port 1080 status before launch:"
$before = Test-NetConnection -ComputerName localhost -Port 1080 -WarningAction SilentlyContinue
Write-Host "   Reachable: $($before.TcpTestSucceeded)"

# Launch desktop app
Write-Host "`n2. Launching Desktop app..."
$proc = Start-Process -FilePath ".\src\TastileDesktop\bin\Release\net10.0-windows10.0.26100.0\win-x64\TastileDesktop.exe" -PassThru
Write-Host "   App PID: $($proc.Id)"

# Wait for startup
Write-Host "`n3. Waiting 5 seconds for startup..."
Start-Sleep -Seconds 5

# Check logs
Write-Host "`n4. Daemon log:"
Get-Content "$env:TEMP\tastile-daemon.log" -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "   $_" }

Write-Host "`n5. App log (last 5 lines):"
Get-Content "$env:TEMP\tastile-desktop.log" -ErrorAction SilentlyContinue | Select-Object -Last 5 | ForEach-Object { Write-Host "   $_" }

# Check processes
Write-Host "`n6. Running processes:"
Get-Process | Where-Object { $_.ProcessName -match "tastile|TastileDesktop" } | Select-Object ProcessName, Id | ForEach-Object { Write-Host "   $($_.ProcessName) (PID: $($_.Id))" }

# Check port
Write-Host "`n7. Testing daemon health..."
try {
    $response = Invoke-RestMethod -Uri "http://localhost:1080/health" -TimeoutSec 3
    Write-Host "   ✅ Daemon responding: $($response | ConvertTo-Json)"
} catch {
    Write-Host "   ❌ Not responding: $_"
}

# Cleanup
Write-Host "`n8. Cleaning up..."
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Get-Process -Name tastile-daemon -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host "   Done"
