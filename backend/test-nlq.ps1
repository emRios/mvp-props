# Script de pruebas para /api/nlq
Write-Host "=== Probando Endpoint NLQ ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Consulta simple
Write-Host "1. Consulta simple: '¿Cuántas propiedades hay?'" -ForegroundColor Yellow
try {
    $body = @{
        query = "¿Cuántas propiedades hay?"
        limit = 10
        estado = "disponible"
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/nlq" -Method POST -ContentType "application/json" -Body $body
    Write-Host "   ✓ Success" -ForegroundColor Green
    Write-Host "   Answer: $($response.answer)" -ForegroundColor White
    Write-Host "   Latency: $($response.latency_ms)ms" -ForegroundColor Gray
    Write-Host "   Tool Args: $($response.toolArgs | ConvertTo-Json -Compress)" -ForegroundColor Gray
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 2: Consulta con precios
Write-Host "2. Consulta con precios: 'Muéstrame propiedades con sus precios'" -ForegroundColor Yellow
try {
    $body = @{
        query = "Muéstrame propiedades con sus precios"
        limit = 5
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/nlq" -Method POST -ContentType "application/json" -Body $body
    Write-Host "   ✓ Success" -ForegroundColor Green
    Write-Host "   Answer: $($response.answer)" -ForegroundColor White
    Write-Host "   Latency: $($response.latency_ms)ms" -ForegroundColor Gray
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 3: Consulta con imágenes
Write-Host "3. Consulta con imágenes: 'Quiero ver propiedades con fotos'" -ForegroundColor Yellow
try {
    $body = @{
        query = "Quiero ver propiedades con fotos"
        limit = 3
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/nlq" -Method POST -ContentType "application/json" -Body $body
    Write-Host "   ✓ Success" -ForegroundColor Green
    Write-Host "   Answer: $($response.answer)" -ForegroundColor White
    Write-Host "   Latency: $($response.latency_ms)ms" -ForegroundColor Gray
} catch {
    Write-Host "   ✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 4: Query vacío (debe fallar)
Write-Host "4. Query vacío (validación)" -ForegroundColor Yellow
try {
    $body = @{
        query = ""
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/nlq" -Method POST -ContentType "application/json" -Body $body
    Write-Host "   ✗ No debería haber pasado la validación" -ForegroundColor Red
} catch {
    Write-Host "   ✓ Validación correcta: Query vacío rechazado" -ForegroundColor Green
}
Write-Host ""

# Test 5: Rate limiting (enviar muchas requests)
Write-Host "5. Rate Limiting (enviando 35 requests en 1 minuto)" -ForegroundColor Yellow
$successCount = 0
$rateLimitedCount = 0

for ($i = 1; $i -le 35; $i++) {
    try {
        $body = @{ query = "Test $i" } | ConvertTo-Json
        $response = Invoke-RestMethod -Uri "http://localhost:5000/api/nlq" -Method POST -ContentType "application/json" -Body $body -ErrorAction Stop
        $successCount++
    } catch {
        if ($_.Exception.Response.StatusCode -eq 503) {
            $rateLimitedCount++
        }
    }
}

Write-Host "   ✓ Requests exitosas: $successCount (debe ser ~30)" -ForegroundColor Green
Write-Host "   ✓ Rate limited: $rateLimitedCount (debe ser ~5)" -ForegroundColor Green
Write-Host ""

Write-Host "=== Pruebas completadas ===" -ForegroundColor Cyan
