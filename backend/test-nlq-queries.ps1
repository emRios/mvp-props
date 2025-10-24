param(
    [string]$BaseUrl = "http://localhost:5000"
)

function Invoke-Nlq([string]$q, [int]$limit = 12) {
    $body = @{ query = $q; limit = $limit } | ConvertTo-Json
    try {
        $r = Invoke-RestMethod -Uri "$BaseUrl/api/nlq" -Method POST -ContentType 'application/json' -Body $body -ErrorAction Stop
        $itemsCount = if ($r.toolPayload -and $r.toolPayload.data) { ($r.toolPayload.data | Measure-Object).Count } else { 0 }
        $limitEff = if ($r.toolArgs) { $r.toolArgs.limit } else { $null }
        $preview = @()
        if ($itemsCount -gt 0) {
            $first = $r.toolPayload.data[0]
            $preview = [PSCustomObject]@{
                id = $first.id
                tipo = $first.tipo
                clase_tipo = $first.clase_tipo
                ubicacion = $first.ubicacion
                direccion = $first.proyecto.direccion
                habitaciones = $first.habitaciones
                banos = $first.'baños'
                precio = $first.precio
            }
        }
        return [PSCustomObject]@{
            query = $q
            items = $itemsCount
            limit = $limitEff
            latency_ms = $r.latency_ms
            answer = $r.answer
            preview = $preview
        }
    } catch {
        return [PSCustomObject]@{
            query = $q
            error = $_.Exception.Message
        }
    }
}

$tests = @(
    'casas en zona 1',
    'apartamentos zona 10',
    'terrenos mixco',
    'casa 3 habitaciones',
    'casa 2 baños zona 1'
)

Write-Host "=== NLQ targeted tests ===" -ForegroundColor Cyan
$results = @()
foreach ($t in $tests) {
    $res = Invoke-Nlq -q $t -limit 12
    $results += $res
    $status = if ($res.error) { '✗' } else { '✓' }
    if ($res.error) {
        Write-Host ("$status $($t): ERROR -> $($res.error)") -ForegroundColor Red
    } else {
        Write-Host ("$status $($t): items=$($res.items) limit=$($res.limit) latency_ms=$($res.latency_ms)") -ForegroundColor Green
        if ($res.preview) {
            Write-Host ("  preview: " + ($res.preview | ConvertTo-Json -Compress)) -ForegroundColor Gray
        }
    }
}

Write-Host "\n=== Summary ===" -ForegroundColor Cyan
$results | ConvertTo-Json -Depth 6
