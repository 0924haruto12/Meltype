# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Yukishiro

$ErrorActionPreference = 'Stop'

# Meltype テスト版のアンインストール (協力者向け)。設定と学習データ (%LOCALAPPDATA%\Meltype) も消す。

# 動いている Meltype を止める。管理者として動いている Meltype は Stop-Process では止められないので、
# まず Meltype.exe --exit で終了の合図を送る (新しい版の Meltype なら、権限に関係なく終了する)。
function Stop-Meltype {
    $installed = Join-Path $env:LOCALAPPDATA 'Programs\Meltype\Meltype.exe'
    if ((Get-Process Meltype -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath $installed)) {
        Start-Process -FilePath $installed -ArgumentList '--exit' -Wait -ErrorAction SilentlyContinue
        for ($i = 0; $i -lt 30 -and (Get-Process Meltype -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 100 }
    }
    foreach ($process in Get-Process Meltype, meltype_mozc_helper, AutoIME -ErrorAction SilentlyContinue) {
        try {
            $process | Stop-Process -Force -ErrorAction Stop
        }
        catch {
            Write-Host 'Meltype を終了できませんでした (管理者として動いているのかもしれません)。'
            Write-Host 'タスクトレイの Meltype のアイコンを右クリックして「終了」を選んでから、もう一度実行してください。'
            exit 1
        }
    }
    Start-Sleep -Milliseconds 300
}

# 旧名 (AutoIME) のときのものも一緒に消す。
Stop-Meltype

foreach ($name in 'Meltype.lnk', 'AutoIME.lnk') {
    $shortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\$name"
    if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
}

foreach ($folder in 'Programs\Meltype', 'Meltype', 'Programs\AutoIME', 'AutoIME' | ForEach-Object { Join-Path $env:LOCALAPPDATA $_ }) {
    if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force }
}
Write-Host 'Meltype をアンインストールしました (設定と学習データも削除しました)。'
