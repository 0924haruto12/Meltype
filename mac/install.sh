#!/bin/bash
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Yukishiro
#
# テスト版の Meltype.app を ~/Library/Input Methods に入れる。zip を展開したフォルダーで実行する:
#   bash install.sh
set -euo pipefail
cd "$(dirname "$0")"

if [[ ! -d Meltype.app ]]; then
    echo "Meltype.app が見つかりません。zip を展開したフォルダーで実行してください。" >&2
    exit 1
fi
for helper in start-input-method.sh select-input-source.swift; do
    [[ -f "$helper" ]] || { echo "必要なファイルがありません: ${helper}。zip 全体を展開してください。" >&2; exit 1; }
done

# 入力ソースの「+」の一覧に Meltype が出ない Mac がある (macOS 26、#21)。
# アプリ内の登録処理で、有効な入力ソースを追加し、Meltype の重複だけを整理する。
enable_input_source() {
    "$TARGET/Meltype.app/Contents/MacOS/Meltype" --register-input-source
    killall TextInputMenuAgent 2>/dev/null || true
    echo "入力ソースに Meltype を追加しました"
}

TARGET="$HOME/Library/Input Methods"
mkdir -p "$TARGET"
pkill -x Meltype 2>/dev/null || true
rm -rf "$TARGET/Meltype.app"
cp -R Meltype.app "$TARGET/"
# インターネットから取ってきた印 (隔離属性) を外す。署名が自分用なので、外さないと macOS が起動させない。
xattr -dr com.apple.quarantine "$TARGET/Meltype.app" 2>/dev/null || true

echo "インストールしました: $TARGET/Meltype.app"
enable_input_source
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$TARGET/Meltype.app"
bash ./start-input-method.sh "$TARGET/Meltype.app"
swift ./select-input-source.swift
echo
echo "Meltype を起動し、入力ソースとして選択しました。"
