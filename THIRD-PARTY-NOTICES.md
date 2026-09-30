# サードパーティー通知

Meltype のソースコード (このリポジトリ) には、他者の著作物は含まれていません。

## 配布用パッケージに同梱しているもの

| 部品 | ライセンス | 同梱先 |
| --- | --- | --- |
| .NET ランタイム (Microsoft.NETCore.App, Microsoft.WindowsDesktop.App) | MIT License | `app\dotnet\` (ライセンス: `app\dotnet\LICENSE.txt`、同梱部品の通知: `app\dotnet\ThirdPartyNotices.txt`) |
| [Mozc](https://github.com/google/mozc) の変換エンジンと辞書 (`meltype_mozc_helper.exe`。Meltype 用の小さな入出力部分 `native/mozc/meltype_mozc_helper.cc` を足してビルドしたもの) | Mozc: BSD-3-Clause (Copyright Google Inc.)。辞書: IPAdic (NAIST)・ICOT・沖縄辞書 のライセンス。組み込みのライブラリ: Abseil (Apache-2.0)・Protocol Buffers (BSD-3-Clause)・Japanese Usage Dictionary など | `app\mozc\` (ライセンス: `app\mozc\MOZC-LICENSE.txt`、辞書とライブラリの全文: `app\mozc\MOZC-CREDITS.html`。Qt は使っていない) |

## Mac 版 (mac/) がビルド時に取り込むもの

| 部品 | ライセンス | 使い方 |
| --- | --- | --- |
| [AzooKeyKanaKanjiConverter](https://github.com/azooKey/AzooKeyKanaKanjiConverter) (azooKey の変換エンジンと辞書) | MIT License | Swift Package として取り込み、Meltype.app に組み込む (漢字変換)。Meltype.app を配布するときは、azooKey のライセンス表示も同梱する |
| .NET ランタイム (NativeAOT) | MIT License | libMeltypeNative.dylib に組み込まれる |

## 実行時に使う Windows の機能 (同梱しない)

次は Windows に標準で入っているものを実行時に呼び出すだけで、Meltype には含まれません。

- Microsoft IME の変換エンジン (IFELanguage)
- Windows の変換候補 API (Windows.Data.Text.TextConversionGenerator)
- Windows のスペルチェッカー (ISpellChecker)
- UI Automation、IMM32、Text Services Framework

## 辞書

### 英訳の候補 (dictionaries/translations.txt)

[JMdict](https://www.edrdg.org/wiki/index.php/JMdict-EDICT_Dictionary_Project) (Japanese-Multilingual Dictionary) の英語版から、よく使う語 (ichi1・news1・spec1・spec2・gai1 の印が付いた語) の書き方・品詞・英訳の一部を `tools/make-translations.mjs` で取り出して作りました。

- 著作権: Electronic Dictionary Research and Development Group (EDRDG)
- ライセンス: [Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0)](https://creativecommons.org/licenses/by-sa/4.0/)。EDRDG のライセンスの説明: https://www.edrdg.org/edrdg/licence.html
- この派生データ (dictionaries/translations.txt) も CC BY-SA 4.0 です。Meltype のプログラム本体は GPL-3.0-or-later です。

### 絵文字の辞書 (dictionaries/emoji-cldr.txt)

Unicode CLDR の絵文字の日本語の名前・キーワード (common/annotations/ja.xml, common/annotationsDerived/ja.xml) から、読みを付けて作りました。Unicode License v3 (SPDX: Unicode-3.0) です。

```
UNICODE LICENSE V3

COPYRIGHT AND PERMISSION NOTICE

Copyright © 1991-2026 Unicode, Inc.

NOTICE TO USER: Carefully read the following legal agreement. BY
DOWNLOADING, INSTALLING, COPYING OR OTHERWISE USING DATA FILES, AND/OR
SOFTWARE, YOU UNEQUIVOCALLY ACCEPT, AND AGREE TO BE BOUND BY, ALL OF THE
TERMS AND CONDITIONS OF THIS AGREEMENT. IF YOU DO NOT AGREE, DO NOT
DOWNLOAD, INSTALL, COPY, DISTRIBUTE OR USE THE DATA FILES OR SOFTWARE.

Permission is hereby granted, free of charge, to any person obtaining a
copy of data files and any associated documentation (the "Data Files") or
software and any associated documentation (the "Software") to deal in the
Data Files or Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, and/or sell
copies of the Data Files or Software, and to permit persons to whom the
Data Files or Software are furnished to do so, provided that either (a)
this copyright and permission notice appear with all copies of the Data
Files or Software, or (b) this copyright and permission notice appear in
associated Documentation.

THE DATA FILES AND SOFTWARE ARE PROVIDED "AS IS", WITHOUT WARRANTY OF ANY
KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF
THIRD PARTY RIGHTS.

IN NO EVENT SHALL THE COPYRIGHT HOLDER OR HOLDERS INCLUDED IN THIS NOTICE
BE LIABLE FOR ANY CLAIM, OR ANY SPECIAL INDIRECT OR CONSEQUENTIAL DAMAGES,
OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS,
WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION,
ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THE DATA
FILES OR SOFTWARE.

Except as contained in this notice, the name of a copyright holder shall
not be used in advertising or otherwise to promote the sale, use or other
dealings in these Data Files or Software without prior written
authorization of the copyright holder.

SPDX-License-Identifier: Unicode-3.0
```

### そのほかの辞書

`dictionaries/` の辞書 (日本語・英語の単語、固有名詞、同音異義語の候補、文脈の手がかり) は Meltype のために作成したもので、
Meltype 本体と同じライセンス (GPL-3.0-or-later、商用ライセンス) です。
固有名詞の辞書に含まれる製品名・会社名は各社の商標です。
