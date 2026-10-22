# Reports Kitty settings state without ever printing the token value.
# Usage: powershell -NoProfile -File settings-probe.ps1
$path = Join-Path $env:APPDATA 'Kitty/settings.json'
if (-not (Test-Path $path)) { Write-Output 'settings.json: missing'; exit 0 }
$json = Get-Content $path -Raw | ConvertFrom-Json
$token = $json.Token
$present = -not [string]::IsNullOrWhiteSpace([string]$token)
$len = 0
if ($present) { $len = ([string]$token).Length }
Write-Output ("settings.json: TokenPresent={0} TokenLength={1} SourceMode={2} LastLanguage={3}" -f $present, $len, $json.SourceMode, $json.LastLanguage)
