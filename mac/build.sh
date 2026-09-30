#!/bin/bash
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Yukishiro
#
# Meltype の Mac 版をビルドして ~/Library/Input Methods にインストールする。
#   ./build.sh            ビルドしてインストール
#   ./build.sh --no-install  ビルドだけ (build/Meltype.app)
# 必要なもの: macOS 13 以降、Xcode (またはコマンドライン ツール: xcode-select --install)、.NET 10 SDK
set -euo pipefail
cd "$(dirname "$0")"

INSTALL=1
[[ "${1:-}" == "--no-install" ]] && INSTALL=0

case "$(uname -m)" in
    arm64) RID=osx-arm64 ;;
    x86_64) RID=osx-x64 ;;
    *) echo "対応していない CPU です: $(uname -m)" >&2; exit 1 ;;
esac

BUILD=build
APP="$BUILD/Meltype.app"
rm -rf "$BUILD/native" "$APP"
mkdir -p "$BUILD"

echo "== 1/3 本体 (C#, NativeAOT) をビルド"
# リポジトリの nuget.config は NuGet を使わない設定 (Windows の開発環境用) なので、NativeAOT のコンパイラを取るために nuget.org を指定する。
dotnet publish ../src/Meltype.Mac.Native/Meltype.Mac.Native.csproj -c Release -r "$RID" \
    -p:PublishAot=true -p:NativeLib=Shared -p:StripSymbols=true \
    --source https://api.nuget.org/v3/index.json -o "$BUILD/native"

echo "== 2/3 IME (Swift) をビルド (初回は azooKey の変換エンジンと辞書のダウンロードに時間がかかります)"
swift build -c release

echo "== 3/3 Meltype.app を組み立て"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$APP/Contents/Frameworks"
cp "$(swift build -c release --show-bin-path)/MeltypeIME" "$APP/Contents/MacOS/Meltype"
cp "$BUILD/native/MeltypeNative.dylib" "$APP/Contents/Frameworks/libMeltypeNative.dylib"
cp Resources/Info.plist "$APP/Contents/Info.plist"
cp Resources/icon.tiff "$APP/Contents/Resources/icon.tiff"
# azooKey の辞書などのリソース (Swift Package のリソースバンドル)
for bundle in "$(swift build -c release --show-bin-path)"/*.bundle; do
    [[ -e "$bundle" ]] && cp -R "$bundle" "$APP/Contents/Resources/"
done
# 自分の Mac で使うための署名 (配布用の署名ではない)
codesign --force --deep --sign - "$APP"
echo "作成しました: $APP"

if [[ $INSTALL -eq 1 ]]; then
    TARGET="$HOME/Library/Input Methods"
    mkdir -p "$TARGET"
    pkill -x Meltype 2>/dev/null || true
    rm -rf "$TARGET/Meltype.app"
    cp -R "$APP" "$TARGET/"
    echo "インストールしました: $TARGET/Meltype.app"
    echo "初めてのときは、いったんログアウトしてログインし直してから、"
    echo "システム設定 → キーボード → 入力ソース →「編集…」→「+」→ 日本語 → Meltype を追加してください。"
fi
