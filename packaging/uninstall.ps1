# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Yukishiro

$ErrorActionPreference = 'Stop'

# AutoIME テスト版のアンインストール (協力者向け)。設定と学習データ (%LOCALAPPDATA%\AutoIME) も消す。

Get-Process AutoIME -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\AutoIME.lnk'
if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }

foreach ($folder in @((Join-Path $env:LOCALAPPDATA 'Programs\AutoIME'), (Join-Path $env:LOCALAPPDATA 'AutoIME'))) {
    if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force }
}
Write-Host 'AutoIME をアンインストールしました (設定と学習データも削除しました)。'
