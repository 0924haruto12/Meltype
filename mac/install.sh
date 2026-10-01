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

TARGET="$HOME/Library/Input Methods"
mkdir -p "$TARGET"
pkill -x Meltype 2>/dev/null || true
rm -rf "$TARGET/Meltype.app"
cp -R Meltype.app "$TARGET/"
# インターネットから取ってきた印 (隔離属性) を外す。署名が自分用なので、外さないと macOS が起動させない。
xattr -dr com.apple.quarantine "$TARGET/Meltype.app" 2>/dev/null || true

echo "インストールしました: $TARGET/Meltype.app"
echo
echo "初めてのときは:"
echo "  1. いったんログアウトしてログインし直す"
echo "  2. システム設定 → キーボード → 入力ソース →「編集…」→「+」→ 日本語 → Meltype を追加"
echo "  3. メニューバーの入力メニューで Meltype を選ぶ"
