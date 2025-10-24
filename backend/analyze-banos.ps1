param(
  [string]$BaseUrl = 'http://localhost:5000',
  [int]$PageLimit = 100
)

$afterId = $null
$all = @()

while ($true) {
  $qs = "fields=id,clase_tipo,baños&limit=$PageLimit"
  if ($afterId) { $qs += "&afterId=$afterId" }
  try {
    $resp = Invoke-RestMethod -Uri ("$BaseUrl/api/propiedades/miraiz-lite?" + $qs) -Method GET -ErrorAction Stop
  } catch {
    Write-Error $_.Exception.Message
    break
  }
  if (-not $resp -or -not $resp.data) { break }
  $all += $resp.data
  if (-not $resp.cursor) { break }
  $afterId = $resp.cursor
}

# Normalize baños key (string to number) and count
$casas = 0
$casas_ge3 = 0
$banos_null = 0
foreach ($item in $all) {
  $isCasa = ($item.clase_tipo -and ($item.clase_tipo.ToString().ToLower() -like '*casa*'))
  if ($isCasa) { $casas++ }
  $b = $null
  if ($item.'baños' -ne $null) { $b = [decimal]$item.'baños' } elseif ($item.'banos' -ne $null) { $b = [decimal]$item.'banos' }
  if ($isCasa) {
    if ($b -eq $null) { $banos_null++ }
    if ($b -ne $null -and $b -ge 3) { $casas_ge3++ }
  }
}

[PSCustomObject]@{
  total_items = ($all | Measure-Object).Count
  casas_total = $casas
  casas_banos_ge3 = $casas_ge3
  casas_banos_null = $banos_null
} | ConvertTo-Json -Depth 4
