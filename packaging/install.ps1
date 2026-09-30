# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Yukishiro

$ErrorActionPreference = 'Stop'

# Meltype テスト版のインストール (協力者向け)。ビルド済みの app フォルダーを %LOCALAPPDATA%\Programs\Meltype にコピーし、
# スタートアップに登録して起動する。管理者権限は不要。.NET は app の dotnet フォルダーに同梱しているので、インストール不要。

$source = Join-Path $PSScriptRoot 'app'
$target = Join-Path $env:LOCALAPPDATA 'Programs\Meltype'
$exe = Join-Path $target 'Meltype.exe'


# 旧名 (AutoIME) のときのものがあれば片付ける (設定と学習データは Meltype の初回起動時に引き継ぐ)。
Get-Process AutoIME -ErrorAction SilentlyContinue | Stop-Process -Force
$oldShortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\AutoIME.lnk'
if (Test-Path -LiteralPath $oldShortcut) { Remove-Item -LiteralPath $oldShortcut -Force }
$oldProgram = Join-Path $env:LOCALAPPDATA 'Programs\AutoIME'
Get-Process Meltype, meltype_mozc_helper -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300
if (Test-Path -LiteralPath $oldProgram) { Remove-Item -LiteralPath $oldProgram -Recurse -Force }
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force

$startup = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startup 'Meltype.lnk'))
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $target
$shortcut.Description = 'Meltype: 日本語と英語を自動で打ち分ける'
$shortcut.Save()

Start-Process -FilePath $exe -WorkingDirectory $target
Write-Host 'Meltype をインストールして起動しました。画面右下のタスクトレイに「あ」のアイコンが出ます。'
