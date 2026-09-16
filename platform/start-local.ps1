param(
  [string]$Python = $env:PET_PYTHON,
  [string]$Model = $env:PET_MODEL_PATH,
  [string]$PythonDeps = $env:PET_PYTHON_DEPS
)
$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$runtime = Join-Path $projectDir '.runtime'
New-Item -ItemType Directory -Force -Path $runtime | Out-Null
$node = (Get-Command node -ErrorAction Stop).Source
if (-not (Test-Path -LiteralPath (Join-Path $projectDir 'node_modules/vite/bin/vite.js'))) {
  throw 'Please run npm ci in the platform folder first.'
}
foreach ($port in @(4317,4318)) {
  if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
    throw "Port $port is already in use. Existing services have been left unchanged."
  }
}
if ($Python) { $env:PET_PYTHON = $Python }
if ($Model) { $env:PET_MODEL_PATH = $Model }
if ($PythonDeps) { $env:PET_PYTHON_DEPS = $PythonDeps }
$apiScript = Join-Path $projectDir 'server/local-api.mjs'
$webScript = Join-Path $projectDir 'node_modules/vite/bin/vite.js'
$apiProc = Start-Process -FilePath $node -ArgumentList ('"' + $apiScript + '"') -WorkingDirectory $projectDir -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'api.log') -RedirectStandardError (Join-Path $runtime 'api-error.log')
$webProc = Start-Process -FilePath $node -ArgumentList @(('"' + $webScript + '"'),'--host','127.0.0.1') -WorkingDirectory $projectDir -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'web.log') -RedirectStandardError (Join-Path $runtime 'web-error.log')
@(
  @{ id=$apiProc.Id; start=$apiProc.StartTime.ToUniversalTime().Ticks; script=$apiScript },
  @{ id=$webProc.Id; start=$webProc.StartTime.ToUniversalTime().Ticks; script=$webScript }
) | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runtime 'services.json') -Encoding utf8
Write-Host 'Desktop Pets maker: http://127.0.0.1:4317'
Write-Host 'To stop only these services: .\stop-local.ps1'
