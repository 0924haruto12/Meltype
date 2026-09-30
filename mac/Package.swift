// swift-tools-version:5.10
// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// Meltype の Mac 版 (Input Method Kit の IME)。ビルドとインストールは build.sh で行う。
// 漢字変換には azooKey の変換エンジン (AzooKeyKanaKanjiConverter, MIT License) を使う。
// 英語 / 日本語の判定などの本体は、C# の Meltype.Core を NativeAOT で libMeltypeNative.dylib にしたものを dlopen で読み込む。

import PackageDescription

let package = Package(
    name: "MeltypeIME",
    platforms: [.macOS(.v13)],
    dependencies: [
        .package(url: "https://github.com/azooKey/AzooKeyKanaKanjiConverter", branch: "main"),
    ],
    targets: [
        .executableTarget(
            name: "MeltypeIME",
            dependencies: [
                .product(name: "KanaKanjiConverterModuleWithDefaultDictionary", package: "AzooKeyKanaKanjiConverter"),
            ],
            path: "Sources/MeltypeIME",
            linkerSettings: [
                .linkedFramework("InputMethodKit"),
                .linkedFramework("Carbon"),
            ]
        ),
    ],
    swiftLanguageVersions: [.v5]
)
