# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Yukishiro

$ErrorActionPreference = 'Stop'

# Meltype テスト版のアンインストール (協力者向け)。設定と学習データ (%LOCALAPPDATA%\Meltype) も消す。

# 旧名 (AutoIME) のときのものも一緒に消す。
Get-Process Meltype, AutoIME -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

foreach ($name in 'Meltype.lnk', 'AutoIME.lnk') {
    $shortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\$name"
    if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
}

foreach ($folder in 'Programs\Meltype', 'Meltype', 'Programs\AutoIME', 'AutoIME' | ForEach-Object { Join-Path $env:LOCALAPPDATA $_ }) {
    if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force }
}
Write-Host 'Meltype をアンインストールしました (設定と学習データも削除しました)。'
