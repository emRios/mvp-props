param(
  [Parameter(Mandatory=$true)][string]$Query,
  [int]$Limit = 12,
  [string]$BaseUrl = 'http://localhost:5000'
)

$bodyStr = @{ query = $Query; limit = $Limit } | ConvertTo-Json
$body = [System.Text.Encoding]::UTF8.GetBytes($bodyStr)
Write-Output ("body=" + $bodyStr)
try {
  $r = Invoke-RestMethod -Uri "$BaseUrl/api/nlq" -Method POST -ContentType 'application/json; charset=utf-8' -Body $body -ErrorAction Stop
  $items = if ($r.toolPayload -and $r.toolPayload.data) { ($r.toolPayload.data | Measure-Object).Count } else { 0 }
  $lim = if ($r.toolArgs) { $r.toolArgs.limit } else { $null }
  $preview = @()
  if ($items -gt 0) {
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
  Write-Output ("query='" + $Query + "'")
  Write-Output ("items=" + $items)
  Write-Output ("limit=" + $lim)
  Write-Output ("latency_ms=" + $r.latency_ms)
  if ($preview) { Write-Output ("preview=" + ($preview | ConvertTo-Json -Compress)) }
} catch {
  $msg = $_.Exception.Message
  $respBody = $null
  $status = $null
  if ($_.Exception.Response) {
    try {
      $status = $_.Exception.Response.StatusCode.value__
      $stream = $_.Exception.Response.GetResponseStream()
      $reader = New-Object System.IO.StreamReader($stream)
      $respBody = $reader.ReadToEnd()
      $reader.Close()
    } catch {}
  }
  Write-Error ("HTTP Error: " + $status + " - " + $msg)
  if ($respBody) { Write-Error ("Body: " + $respBody) }
  exit 1
}
