$ErrorActionPreference = 'Stop'
$record = Join-Path $PSScriptRoot '.runtime/services.json'
if (-not (Test-Path -LiteralPath $record)) { Write-Host 'No recorded services.'; exit }
$items = Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
foreach ($item in $items) {
  $proc = Get-Process -Id $item.id -ErrorAction SilentlyContinue
  $info = Get-CimInstance Win32_Process -Filter ("ProcessId = " + [int]$item.id)
  if ($proc -and $info -and $proc.StartTime.ToUniversalTime().Ticks -eq $item.start -and $info.CommandLine.Contains($item.script)) {
    Stop-Process -Id $item.id
    Write-Host ("Stopped project service " + $item.id)
  }
}
Remove-Item -LiteralPath $record
