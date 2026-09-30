# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Yukishiro
#
# Mozc の変換ヘルパー (meltype_mozc_helper.exe) をビルドして native\mozc\bin に置く。
# Build-Package.ps1 / Install-Meltype.ps1 は、bin にヘルパーがあれば Meltype に同梱する (無ければ Microsoft IME だけで動く)。
#
# 必要なもの (Mozc の docs/build_mozc_in_windows.md と同じ):
#   Visual Studio 2022 (「C++ によるデスクトップ開発」、Windows 11 SDK、C++ ATL)、Python 3.12 以降、Bazelisk、Git
#
#   .\native\mozc\Build-MozcHelper.ps1 -Python C:\tools\python\python.exe -Bazelisk C:\tools\bazelisk.exe
param(
    # Mozc のソースを置く場所 (無ければ clone する)。Windows のパスの長さの制限があるので短い場所がよい。
    [string]$MozcSource = (Join-Path $env:USERPROFILE 'mozc'),
    [string]$Python = 'python',
    [string]$Bazelisk = 'bazelisk',
    # Visual Studio の VC フォルダー (Mozc の自動検出が失敗するとき用)。
    [string]$VcPath = 'C:\Program Files\Microsoft Visual Studio\2022\Community\VC'
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$bin = Join-Path $here 'bin'

if (-not (Test-Path (Join-Path $MozcSource 'src'))) {
    git clone --depth 1 --recurse-submodules --shallow-submodules https://github.com/google/mozc.git $MozcSource
    if ($LASTEXITCODE -ne 0) { throw 'Mozc を取得できませんでした。' }
}
$src = Join-Path $MozcSource 'src'

# ヘルパーのソースと BUILD の定義を Mozc の converter パッケージに入れる (エンジンを使えるのがこのパッケージのため)。
Copy-Item -LiteralPath (Join-Path $here 'meltype_mozc_helper.cc') -Destination (Join-Path $src 'converter') -Force
$build = Join-Path $src 'converter\BUILD.bazel'
if (-not (Select-String -LiteralPath $build -Pattern 'name = "meltype_mozc_helper"' -Quiet)) {
    Add-Content -LiteralPath $build -Value ("`n" + (Get-Content -Raw -LiteralPath (Join-Path $here 'BUILD.fragment'))) -Encoding utf8
}

Push-Location $src
try {
    # LLVM・MSYS2・Ninja (Qt・WiX・Android NDK は使わない)
    & $Python build_tools/update_deps.py --noqt --nowix --nondk
    if ($LASTEXITCODE -ne 0) { throw '依存関係を取得できませんでした。' }
    $env:BAZEL_VC = $VcPath
    # シンボリックリンクは Windows の開発者モードか管理者権限が要るので使わない。
    & $Bazelisk --nowindows_enable_symlinks build //converter:meltype_mozc_helper --config release_build
    if ($LASTEXITCODE -ne 0) { throw 'ビルドに失敗しました。' }
}
finally {
    Pop-Location
}

New-Item -ItemType Directory -Force -Path $bin | Out-Null
Copy-Item -LiteralPath (Join-Path $src 'bazel-bin\converter\meltype_mozc_helper.exe') -Destination $bin -Force
# Mozc と辞書 (IPAdic など)・ライブラリのライセンス
Copy-Item -LiteralPath (Join-Path $src 'data\installer\credits_en.html') -Destination (Join-Path $bin 'MOZC-CREDITS.html') -Force
Copy-Item -LiteralPath (Join-Path $MozcSource 'LICENSE') -Destination (Join-Path $bin 'MOZC-LICENSE.txt') -Force
Write-Host "作成しました: $bin"
