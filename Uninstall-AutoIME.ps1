param(
    # 設定と学習データ (%LOCALAPPDATA%\AutoIME) も削除する
    [switch]$RemoveData
)
$ErrorActionPreference = 'Stop'

$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\AutoIME.lnk'
if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
Get-Process AutoIME -ErrorAction SilentlyContinue | Stop-Process -Force

if ($RemoveData) {
    $data = Join-Path $env:LOCALAPPDATA 'AutoIME'
    if (Test-Path -LiteralPath $data) { Remove-Item -LiteralPath $data -Recurse -Force }
    Write-Host 'AutoIME を停止し、自動起動と設定・学習データを削除しました。'
} else {
    Write-Host 'AutoIME を停止し、自動起動から外しました。設定と学習データは %LOCALAPPDATA%\AutoIME に残っています (-RemoveData で削除)。'
}
