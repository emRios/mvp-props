# Script de pruebas para el backend
# Ejecutar desde una terminal DIFERENTE a donde corre dotnet run

Write-Host "=== Probando Backend API ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: GET /properties
Write-Host "1. GET /properties" -ForegroundColor Yellow
try {
    $props = Invoke-RestMethod -Uri "http://localhost:5000/properties" -Method GET
    Write-Host "   ✓ Success - Propiedades recibidas: $($props.data.Count)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 2: POST /interactions sin propiedadId
Write-Host "2. POST /interactions (sin propiedadId)" -ForegroundColor Yellow
try {
    $body = '{"userId":"u-demo","pregunta":"Hola, como estas?"}'
    $headers = @{ 
        Authorization = "Bearer demo-key"
        "Content-Type" = "application/json"
    }
    $response = Invoke-RestMethod -Uri "http://localhost:5000/interactions" -Method POST -Headers $headers -Body $body
    Write-Host "   ✓ Success - Respuesta: $($response.Respuesta)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 3: POST /interactions con propiedadId
Write-Host "3. POST /interactions (con propiedadId=942)" -ForegroundColor Yellow
try {
    $body = '{"userId":"u-demo","propiedadId":942,"pregunta":"Tiene parqueos?"}'
    $headers = @{ 
        Authorization = "Bearer demo-key"
        "Content-Type" = "application/json"
    }
    $response = Invoke-RestMethod -Uri "http://localhost:5000/interactions" -Method POST -Headers $headers -Body $body
    Write-Host "   ✓ Success - Respuesta: $($response.Respuesta)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 4: GET /interactions
Write-Host "4. GET /interactions?userId=u-demo" -ForegroundColor Yellow
try {
    $headers = @{ Authorization = "Bearer demo-key" }
    $interactions = Invoke-RestMethod -Uri "http://localhost:5000/interactions?userId=u-demo" -Method GET -Headers $headers
    Write-Host "   ✓ Success - Interacciones encontradas: $($interactions.Count)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 5: GET /metrics/interactions
Write-Host "5. GET /metrics/interactions" -ForegroundColor Yellow
try {
    $headers = @{ Authorization = "Bearer demo-key" }
    $metrics = Invoke-RestMethod -Uri "http://localhost:5000/metrics/interactions" -Method GET -Headers $headers
    Write-Host "   ✓ Success - Total: $($metrics.total), Counts: $($metrics.counts | ConvertTo-Json -Compress)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== Pruebas completadas ===" -ForegroundColor Cyan
