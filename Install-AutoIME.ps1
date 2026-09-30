# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Yukishiro

$ErrorActionPreference = 'Stop'

# AutoIME はタスクトレイに常駐する独立プロセスとして動く。TSF/COM コンポーネントを他アプリに読み込ませることはない。
$project = Join-Path $PSScriptRoot 'src\AutoIME\AutoIME.csproj'
$output = Join-Path $PSScriptRoot 'app-build'

Get-Process AutoIME -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet build $project -c Release -o $output
if ($LASTEXITCODE -ne 0) { throw "AutoIME のビルドに失敗しました (exit code $LASTEXITCODE)。" }

$exe = Join-Path $output 'AutoIME.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "AutoIME.exe が作成されませんでした: $exe" }

# 自動起動 (現在のユーザーのスタートアップ フォルダー)
$startup = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startup 'AutoIME.lnk'))
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $output
$shortcut.Description = 'AutoIME: 入力開始時に日本語入力を自動判定する'
$shortcut.Save()

Start-Process -FilePath $exe -WorkingDirectory $output
Write-Host 'AutoIME をインストールして起動しました。タスクトレイのアイコンから設定・ログ・一時停止 (Ctrl+半角/全角) ができます。'
