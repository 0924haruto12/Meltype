<p align="center">
  <img src="docs/images/logo.png" alt="Meltype" width="420"><br>
  <sub>Logo by <a href="https://github.com/Crysta1221">@Crysta1221</a></sub>
</p>

<p align="center">
  <b>雪解けのように、半角/全角の壁を溶かす日本語入力。</b><br>
  ローマ字で打つだけ。日本語か英語かは Meltype が見分けて、その場で切り替えます。
</p>

<p align="center">
  <a href="https://github.com/yksr-melt/Meltype/releases/latest"><img src="https://img.shields.io/github/v/release/yksr-melt/Meltype?color=5ec4f0&label=download" alt="download"></a>
  <a href="https://github.com/yksr-melt/Meltype/releases"><img src="https://img.shields.io/github/downloads/yksr-melt/Meltype/total?color=ff8ab4" alt="downloads"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0-7a96f0" alt="license"></a>
</p>

<br>

<img src="docs/images/headings/features.svg" alt="できること" width="400">

```
kyouhagoogledekensaku    →  今日はgoogleで検索
ashitanomeetingwotsuika  →  明日のmeetingを追加
I want to go to the park →  I want to go to the park
```

- **混ぜたまま打てる** — 英単語は英字のまま、日本語はかな・漢字に。英文もそのまま
- **コードを書く手も止めない** — VS Code やターミナルでは英数が基本。コメントと文字列の中だけ日本語に
- **ぜんぶ PC の中で** — 判定も変換もローカルで完結。打った文字を外に送りません

絵文字の変換 (えがお → 😊)、予測変換、書き間違いの指摘などもあります。
Windows 10 / 11 に対応。Mac 版・Linux 版はプレビュー版です。

<br>

<img src="docs/images/headings/install.svg" alt="インストール" width="400">

1. [Releases](https://github.com/yksr-melt/Meltype/releases/latest) から `Meltype-<version>-setup.exe` をダウンロード
2. 開いて、案内どおりに進める
3. タスクトレイに「あ」が出たら準備完了

管理者権限は不要。新しい版は自動で入ります。

<details>
<summary>「Windows によって PC が保護されました」と出たとき</summary>

「詳細情報」→「実行」で進められます。コード署名をしていないため表示されます。

</details>

<details>
<summary>「ウイルスを検出しました」と出たとき</summary>

SmartScreen の警告とは別のものです。誤検知のこともありますが、検出名だけでは見分けられません。

1. Windows セキュリティ →「ウイルスと脅威の防止」→「保護の更新」で定義を新しくする
2. 公式のリリースからダウンロードし直して、もう一度検査する
3. それでも検出されるときは、保護を切ったりフォルダーを除外したりせずに、「保護の履歴」で検出名を確かめて [Issues](https://github.com/yksr-melt/Meltype/issues) で教えてください。個人名やパス、ダウンロード URL の一時トークンは隠してください

ファイルが本物か確かめたいときは、リリースのページにある SHA-256 と比べられます。PowerShell なら `Get-FileHash .\Meltype-<version>-windows.zip` です。

</details>

<details>
<summary>zip 版・Mac 版・Linux 版</summary>

| | ファイル |
|---|---|
| Windows (zip) | `Meltype-<version>-windows.zip` を展開して `Install.cmd` をダブルクリック |
| Mac (プレビュー版) | `Meltype-<version>-mac.zip` ([mac/README.md](mac/README.md)) |
| Linux (プレビュー版) | `Meltype-<version>-linux.zip` (IBus / fcitx5) |

インストーラーでも zip でも、入る場所は同じ `%LOCALAPPDATA%\Programs\Meltype` です。
Windows 版に必要なのは Windows 10 / 11 (64bit) と Microsoft IME だけで、.NET は同梱しています。

</details>

<details>
<summary>アンインストール</summary>

トレイのアイコンを右クリックして「アンインストール...」を選ぶか、Windows の「設定」→「アプリ」から消せます。
インストーラーで入れた場合は、最後に設定と学習データも消すかを聞かれます。zip 版は `Uninstall.cmd` でも消せて、こちらは設定と学習データもまとめて消えます。

</details>

<br>

<img src="docs/images/headings/start.svg" alt="使い方" width="400">

いつもどおりローマ字で打つだけ。

| キー | |
|---|---|
| <kbd>Space</kbd> / <kbd>Enter</kbd> | 変換 / 確定 |
| <kbd>F7</kbd> / <kbd>F10</kbd> | カタカナ / 英字 (英字に直した語は次から英字に) |
| <kbd>半角/全角</kbd> | 英数と日本語の切り替え |
| <kbd>Ctrl</kbd> + <kbd>半角/全角</kbd> | Meltype を一時停止 |

Meltype IME を入れた場合は、<kbd>Win</kbd> + <kbd>Space</kbd> で「Meltype」を選ぶと入力欄に直接入力できます。
くわしくは [docs/USAGE.md](docs/USAGE.md) へ。

<br>

<img src="docs/images/headings/faq.svg" alt="困ったときは" width="400">

<details>
<summary>タスクバーの IME の表示がずっと「A」のまま</summary>

故障ではありません。Meltype が Windows の IME に代わって入力を受け持っているためです。
今のモードは、カーソルの近くに出る「あ」「A」か、トレイのアイコンで確認できます。

</details>

<details>
<summary>Google 日本語入力など、ほかの IME も使いたい</summary>

<kbd>Ctrl</kbd> + <kbd>半角/全角</kbd> で Meltype を一時停止すれば使えます。

</details>

<details>
<summary>英語のつもりがかなに、かなのつもりが英字になった</summary>

<kbd>F10</kbd> (英字) か <kbd>F6</kbd> (ひらがな) で直して確定すれば、次からはその語を覚えています。
トレイのメニューの「自動判定の強さ」でも調整できます。

</details>

<details>
<summary>おかしな動きを見つけた</summary>

トレイのメニューの「不具合の報告・提案...」から送れます。
「どのアプリで」「何と打って」「どうなったか」があると、すぐに調べられます。

</details>

<details>
<summary>打った内容はどこかに送られる?</summary>

送りません。通信するのは、自動更新の確認と、自分で開いた不具合報告のフォームだけです。
保存するのは `%LOCALAPPDATA%\Meltype` の設定・学習データ・ユーザー辞書 (と、ON にしたときのログ) だけ。
セキュリティの方針と脆弱性の連絡先は [SECURITY.md](SECURITY.md) にあります。

</details>

<br>

<img src="docs/images/headings/about.svg" alt="Meltype について" width="400">

[GNU GPL v3.0](LICENSE) で公開しています。個人でも会社でも無料。ソースを公開せずに製品へ組み込みたいなど、GPL v3 で使えない場合はご相談ください: ibutya0319@gmail.com

不具合の報告、辞書の追加、Pull Request はいつでも大歓迎です → [CONTRIBUTING.md](CONTRIBUTING.md) ・ [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)

<details>
<summary>協力してくださった方々</summary>

テスト版で不具合の報告や意見をくださった皆さん、ありがとうございました (敬称略)。

くらいど！ ([@Kuraido8888](https://x.com/Kuraido8888)) ・
しぐれ ([@Akisameee0465](https://x.com/Akisameee0465)) ・
琴音Link ・
あげちゃ ・
うな ([@una08142009](https://x.com/una08142009)) ・
かふぇらて ([@cafely_latte](https://x.com/cafely_latte)) ・
ウパー ([@upah_setu](https://x.com/upah_setu)) ・
Ray ・
うぽつです ([@up2ds](https://x.com/up2ds))

</details>

<details>
<summary>著作権表示・同梱物</summary>

```
Meltype
Copyright (C) 2026 雪代 / Yukishiro (@yksr_melt / @yksr-melt)

This program is free software: you can redistribute it and/or modify it under the terms of the
GNU General Public License as published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without
even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
General Public License for more details.
```

ソースファイルには `SPDX-License-Identifier: GPL-3.0-or-later` を付けています。
同梱している .NET ランタイム (MIT) と、使っている Windows の機能については [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を見てください。
ロゴ ([docs/meltype.jpg](docs/meltype.jpg)・[docs/images/logo.png](docs/images/logo.png)) は [@Crysta1221](https://github.com/Crysta1221) さんに描いていただきました。

</details>

<br>

<a href="https://www.star-history.com/?repos=yksr-melt%2FMeltype&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=yksr-melt/Meltype&type=date&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=yksr-melt/Meltype&type=date&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=yksr-melt/Meltype&type=date&legend=top-left" width="600" />
 </picture>
</a>
