# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 lnkiai
#
# Meltype IME (Windows の IME として入力欄に直接入力する。native\tip) を入れる / 外す。install.ps1・uninstall.ps1 から読み込んで使う。
#   . .\meltype-ime.ps1
#   Install-MeltypeIme -Source <x64 と x86 と Register-Tip.ps1 があるフォルダー>
#   Uninstall-MeltypeIme
# IME の登録は PC 全体の設定なので、ここだけ管理者権限を求める (UAC の確認が出る)。
# 「言語と地域」のキーボードの一覧に足すのは、ふつうの権限で (今のユーザーの設定なので)。

$MeltypeImeTip = '0411:{417D801B-A9BD-4C26-BD16-356A825A6998}{21F643F4-72D5-4946-BF70-0A126AB52D05}'

# 64 ビットの Program Files (32 ビットの PowerShell から動かしても、64 ビットの方を使う)。
# 管理者として動かす Register-Tip.ps1 は、32 ビットの PowerShell で動いても 64 ビットの場所と regsvr32 を使う
$MeltypeProgramFiles = if ($env:ProgramW6432) { $env:ProgramW6432 } else { $env:ProgramFiles }

# ARM64 の Windows か (x64 のエミュレーションで動いている PowerShell では、環境変数が AMD64 になるので、Windows の設定から読む)
function Test-Arm64Windows {
    if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64' -or $env:PROCESSOR_ARCHITEW6432 -eq 'ARM64') { return $true }
    $native = (Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment' -Name PROCESSOR_ARCHITECTURE -ErrorAction SilentlyContinue).PROCESSOR_ARCHITECTURE
    return $native -eq 'ARM64'
}

function Test-MeltypeImeRegistered {
    return Test-Path -LiteralPath 'HKLM:\SOFTWARE\Microsoft\CTF\TIP\{417D801B-A9BD-4C26-BD16-356A825A6998}'
}

# 管理者として実行する。'ok' / 'failed' / 'cancelled' (UAC で「いいえ」) を返す
function Invoke-Elevated([string]$script, [string[]]$arguments) {
    $log = Join-Path $env:TEMP "meltype-ime-$([Guid]::NewGuid().ToString('N')).log"
    $all = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$script`"") + $arguments + @('-Log', "`"$log`"")
    try {
        $process = Start-Process -FilePath powershell.exe -ArgumentList $all -Verb RunAs -WindowStyle Hidden -Wait -PassThru
    }
    catch {
        # UAC で「いいえ」を選んだ
        return 'cancelled'
    }
    if (Test-Path -LiteralPath $log) {
        Get-Content -LiteralPath $log -Encoding UTF8 | ForEach-Object { Write-Host "  $_" }
        Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue
    }
    if ($process.ExitCode -eq 0) { return 'ok' }
    return 'failed'
}

function Add-MeltypeImeToLanguageList {
    $list = Get-WinUserLanguageList
    $japanese = $list | Where-Object { $_.LanguageTag -like 'ja*' } | Select-Object -First 1
    if (-not $japanese) {
        $list.Add('ja-JP')
        $japanese = $list | Where-Object { $_.LanguageTag -like 'ja*' } | Select-Object -First 1
    }
    if ($japanese.InputMethodTips -notcontains $MeltypeImeTip) {
        $japanese.InputMethodTips.Add($MeltypeImeTip)
        Set-WinUserLanguageList $list -Force
    }
    # 既定の入力方式を自分で決めていなければ、Meltype にする (サインインし直したときも Meltype から始まるように)。
    # 自分で決めているときは変えない
    $override = Get-WinDefaultInputMethodOverride -ErrorAction SilentlyContinue
    if (-not "$($override | Out-String)".Trim()) { Set-WinDefaultInputMethodOverride -InputTip $MeltypeImeTip }
}

function Remove-MeltypeImeFromLanguageList {
    # 既定の入力方式が Meltype のままなら、決めていない状態に戻す
    $override = Get-WinDefaultInputMethodOverride -ErrorAction SilentlyContinue
    if ("$($override | Out-String)" -match '417D801B-A9BD-4C26-BD16-356A825A6998') { Set-WinDefaultInputMethodOverride }
    $list = Get-WinUserLanguageList
    $changed = $false
    foreach ($language in $list) {
        if ($language.InputMethodTips -contains $MeltypeImeTip) {
            $language.InputMethodTips.Remove($MeltypeImeTip) | Out-Null
            $changed = $true
        }
    }
    if ($changed) { Set-WinUserLanguageList $list -Force }
}

$MeltypeImeDeclinedKey = 'HKCU:\Software\Meltype'

# ファイルの SHA-256 (Get-FileHash は、ほかのシェルから起動した PowerShell では読み込めないことがあるので .NET で計算する)
function Get-Sha256([string]$path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($path))).Replace('-', '') }
    finally { $sha.Dispose() }
}

# 登録済みの DLL が、これから入れるものと同じか
function Test-MeltypeImeCurrent([string]$Source) {
    if (-not (Test-MeltypeImeRegistered)) { return $false }
    # 登録を外すときに使うスクリプト (Program Files に置いたもの) も同じか (DLL が同じでも、スクリプトを直したら置き直す)
    $installedScript = Join-Path $MeltypeProgramFiles 'Meltype\tip\Register-Tip.ps1'
    $newScript = Join-Path $Source 'Register-Tip.ps1'
    if (-not (Test-Path -LiteralPath $installedScript) -or -not (Test-Path -LiteralPath $newScript)) { return $false }
    if ((Get-Sha256 $installedScript) -ne (Get-Sha256 $newScript)) { return $false }
    foreach ($arch in 'x64', 'x86') {
        $installed = Join-Path $MeltypeProgramFiles "Meltype\tip\$arch\MeltypeTip.dll"
        $new = Join-Path $Source "$arch\MeltypeTip.dll"
        if (-not (Test-Path -LiteralPath $installed) -or -not (Test-Path -LiteralPath $new)) { return $false }
        # 署名する前の中身のハッシュがあれば、それで比べる (署名した DLL は、中身が同じでも毎回違うファイルになる)
        $installedHash = Read-HashFile "$installed.sha256"
        $newHash = Read-HashFile "$new.sha256"
        # 新しい方にハッシュがあるのに、インストール先に無い: 前の登録が途中で失敗した (登録できてからハッシュを置くため)。登録し直す
        if ($newHash -and -not $installedHash) { return $false }
        if ($installedHash -and $newHash) {
            if ($installedHash -ne $newHash) { return $false }
            continue
        }
        if ((Get-Sha256 $installed) -ne (Get-Sha256 $new)) { return $false }
    }
    return $true
}

# ハッシュのファイル (MeltypeTip.dll.sha256) の中身。無い・空なら $null
function Read-HashFile([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    $text = "$(Get-Content -LiteralPath $path -Raw -ErrorAction SilentlyContinue)".Trim()
    if ($text) { return $text.ToUpperInvariant() }
    return $null
}

# 管理者として動かす前に、これから入れる DLL が、ビルドしたときのハッシュと合っているかを確かめる。
# 壊れた・途中までしかコピーされていない DLL を登録しないためのもの。ハッシュのファイルも同じフォルダー (ユーザーが書き換えられる場所)
# にあるので、同じユーザーの権限で動くプログラムによる書き換えは防げない (SECURITY.md)
function Test-MeltypeImeSource([string]$Source) {
    foreach ($arch in 'x64', 'x86') {
        $dll = Join-Path $Source "$arch\MeltypeTip.dll"
        # 配布用のパッケージなら署名した後のハッシュ、ソースから入れたとき (署名しない) は署名する前のハッシュ
        $expected = Read-HashFile "$dll.package.sha256"
        if (-not $expected) { $expected = Read-HashFile "$dll.sha256" }
        if (-not (Test-Path -LiteralPath $dll)) { return $false }
        if ($expected -and (Get-Sha256 $dll) -ne $expected) { return $false }
    }
    return $true
}

# -Ask: 前に断られていても聞く (Install.cmd を自分で実行したとき)。自動更新では聞かない
function Install-MeltypeIme([string]$Source, [switch]$Ask) {
    $register = Join-Path $Source 'Register-Tip.ps1'
    if (-not (Test-Path -LiteralPath $register)) { return $false }
    # ARM64 の Windows では、ARM64 のアプリ (メモ帳・エクスプローラーなど) に x64 / x86 の DLL が読み込まれず、日本語が打てなくなる
    if (Test-Arm64Windows) {
        Write-Host 'ARM64 の Windows には Meltype IME はまだ対応していません。変換ボックスで入力する方式で使えます。'
        return $false
    }
    if (-not (Test-MeltypeImeSource $Source)) {
        Write-Host 'Meltype IME の DLL が、ビルドしたときのものと違います (壊れているか、書き換えられています)。登録しません。'
        return $false
    }
    if (Test-MeltypeImeCurrent $Source) {
        try { Add-MeltypeImeToLanguageList } catch { }
        return $true
    }
    $declined = (Get-ItemProperty -LiteralPath $MeltypeImeDeclinedKey -Name ImeDeclined -ErrorAction SilentlyContinue).ImeDeclined -eq 1
    if ($declined -and -not $Ask) {
        Write-Host '前に Meltype IME の登録を断ったので、登録しません (Install.cmd か Install-Meltype.ps1 を実行すると、もう一度聞きます)。'
        return $false
    }
    Write-Host 'Meltype IME (入力欄に直接入力) を Windows に登録します。管理者権限の確認が出たら「はい」を選んでください。'
    $result = Invoke-Elevated $register @('-Source', "`"$Source`"")
    if ($result -ne 'ok') {
        Write-Host 'Meltype IME を登録できませんでした。今までどおり、変換ボックスで入力する方式で使えます。'
        # UAC で断られたときだけ覚えて、自動更新では聞かない (失敗したときは、次の更新でまた試す)
        if ($result -eq 'cancelled') {
            New-Item -Path $MeltypeImeDeclinedKey -Force | Out-Null
            Set-ItemProperty -LiteralPath $MeltypeImeDeclinedKey -Name ImeDeclined -Value 1 -Type DWord
        }
        return $false
    }
    Remove-ItemProperty -LiteralPath $MeltypeImeDeclinedKey -Name ImeDeclined -ErrorAction SilentlyContinue
    try {
        Add-MeltypeImeToLanguageList
    }
    catch {
        Write-Host "キーボードの一覧に Meltype を足せませんでした: $($_.Exception.Message)"
        Write-Host '「設定」→「時刻と言語」→「言語と地域」→ 日本語 の「…」→「言語のオプション」→「キーボードの追加」で Meltype を選んでください。'
    }
    Write-Host 'Meltype IME を登録しました。Win + Space で「Meltype」を選ぶと、入力欄に直接入力できます。'
    return $true
}

function Uninstall-MeltypeIme {
    try { Remove-MeltypeImeFromLanguageList } catch { }
    # 登録を断った記録も消す
    Remove-Item -LiteralPath $MeltypeImeDeclinedKey -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-MeltypeImeRegistered)) { return $true }
    $register = Join-Path $MeltypeProgramFiles 'Meltype\tip\Register-Tip.ps1'
    # Program Files に無ければ、同じ場所 (インストール先) の tip か、ソースの native\tip のもの
    if (-not (Test-Path -LiteralPath $register)) { $register = Join-Path $PSScriptRoot 'tip\Register-Tip.ps1' }
    if (-not (Test-Path -LiteralPath $register)) { $register = Join-Path $PSScriptRoot '..\native\tip\Register-Tip.ps1' }
    if (-not (Test-Path -LiteralPath $register)) { return $false }
    Write-Host 'Meltype IME の登録を外します。管理者権限の確認が出たら「はい」を選んでください。'
    return (Invoke-Elevated $register @('-Unregister')) -eq 'ok'
}
