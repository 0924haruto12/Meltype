$ErrorActionPreference = 'Stop'

# AutoIME テスト版のインストール (協力者向け)。ビルド済みの app フォルダーを %LOCALAPPDATA%\Programs\AutoIME にコピーし、
# スタートアップに登録して起動する。管理者権限は不要。.NET は app の dotnet フォルダーに同梱しているので、インストール不要。

$source = Join-Path $PSScriptRoot 'app'
$target = Join-Path $env:LOCALAPPDATA 'Programs\AutoIME'
$exe = Join-Path $target 'AutoIME.exe'


Get-Process AutoIME -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force

$startup = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startup 'AutoIME.lnk'))
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $target
$shortcut.Description = 'AutoIME: 日本語と英語を自動で打ち分ける'
$shortcut.Save()

Start-Process -FilePath $exe -WorkingDirectory $target
Write-Host 'AutoIME をインストールして起動しました。画面右下のタスクトレイに「あ」のアイコンが出ます。'
