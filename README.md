<p align="center">
  <img src="docs/images/logo.png" alt="Meltype" width="480"><br>
  <sub>Logo by <a href="https://github.com/Crysta1221">@Crysta1221</a></sub>
</p>

<p align="center"><b>雪解けのように、半角/全角の壁を溶かす日本語入力。</b></p>

<p align="center">
  <a href="https://github.com/yksr-melt/Meltype/releases/latest"><img src="https://img.shields.io/github/v/release/yksr-melt/Meltype?color=5ec4f0" alt="release"></a>
  <a href="https://github.com/yksr-melt/Meltype/releases"><img src="https://img.shields.io/github/downloads/yksr-melt/Meltype/total?color=ff8ab4" alt="downloads"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0-7a96f0" alt="license"></a>
</p>

ローマ字で打つだけで、日本語と英語を自動で打ち分ける日本語入力です。
半角/全角 キーを押して切り替える必要はありません。

```
kyouhagoogledekensaku  →  今日はgoogleで検索
```

Windows 10 / 11 用です。Mac 版・Linux 版 (IBus / fcitx5) はプレビュー版です ([mac/README.md](mac/README.md))。

<br>
<img src="docs/images/headings/features.svg" alt="できること" width="560">

| | |
|---|---|
| **日本語と英語を混ぜて打てる** | 英単語 (`google` `github` …) は英字のまま、ほかはかな・漢字に |
| **英文もそのまま** | `I want to go to the park` はそのまま英文で入る |
| **コードを書くときも** | VS Code やターミナルでは基本は英数。コメントと文字列の中だけ日本語 |
| **AI エージェントの入力に** | `/command`・`$skill`・`@ファイル名` は変換せずにそのまま入る |
| **絵文字・書き間違い** | えがお → 😊、ブレスレッド → ブレスレット を指摘 |
| **ネットに送らない** | 判定も変換も、すべて PC の中で行う |

<br>
<img src="docs/images/headings/install.svg" alt="インストール" width="560">

1. [Releases](https://github.com/yksr-melt/Meltype/releases/latest) から `Meltype-<version>-setup.exe` をダウンロードして実行
2. 案内に従ってインストール (管理者権限は不要。Meltype IME を入れるときだけ確認が出ます。断っても変換ボックスで使えます)
3. タスクトレイに「あ」が出たら完了。次からは Windows の起動時に自動で起動します

新しい版が出ると自動で更新します。必要なのは Windows 10 / 11 (64bit) と Microsoft IME (Windows 標準の日本語入力) だけで、.NET は同梱しています。

> 「Windows によって PC が保護されました」と出たら、「詳細情報」→「実行」で入れられます (コード署名をしていないためです)。

<details>
<summary>zip 版・Mac 版・Linux 版</summary>

| OS | ファイル | |
|---|---|---|
| Windows | `Meltype-<version>-windows.zip` | 展開して `Install.cmd` をダブルクリック |
| Mac | `Meltype-<version>-mac.zip` | プレビュー版 |
| Linux | `Meltype-<version>-linux.zip` | プレビュー版 (IBus / fcitx5) |

インストーラーと zip のどちらで入れても、同じ場所 (`%LOCALAPPDATA%\Programs\Meltype`) に入ります。

</details>

<details>
<summary>「ウイルスを検出しました」と出たとき</summary>

上の SmartScreen の警告とは別のものです。誤検知のこともありますが、検出名だけでは判断できません。

1. Windows セキュリティ →「ウイルスと脅威の防止」→「保護の更新」で定義を更新する
2. 公式のリリースからダウンロードし直して、もう一度検査する
3. それでも検出されるときは、保護を無効にしたりフォルダーを除外したりせず、「保護の履歴」で検出名と影響を受けた項目を確かめて、版と検出名を [Issues](https://github.com/yksr-melt/Meltype/issues) に報告してください (個人名・パス・ダウンロード URL の一時トークンは隠してください)

ダウンロードしたファイルが本物か確かめたいときは、リリースのページの SHA-256 と比べてください (PowerShell: `Get-FileHash .\Meltype-<version>-windows.zip`)。

</details>

<details>
<summary>アンインストール</summary>

トレイの Meltype のアイコンを右クリック →「アンインストール...」、または Windows の「設定」→「アプリ」→「インストールされているアプリ」から。
インストーラーで入れたときは、最後に設定と学習データも消すかを聞きます (既定は残す)。zip 版は `Uninstall.cmd` でも消せます (設定と学習データも消えます)。

</details>

<br>
<img src="docs/images/headings/start.svg" alt="使い始める" width="560">

メモ帳やブラウザーで、そのままローマ字で打ってみてください。

- **Meltype IME を入れた場合**: <kbd>Win</kbd> + <kbd>Space</kbd> で「Meltype」を選ぶ。打った文字は入力欄に直接入り、候補は入力位置の下に出ます
- **入れていない場合**: カーソルの下に変換ボックスが出ます

| キー | 動き |
|---|---|
| <kbd>Enter</kbd> | 確定 |
| <kbd>Space</kbd> | 漢字に変換 (英単語なら確定して空白) |
| <kbd>←</kbd> <kbd>→</kbd> / <kbd>↓</kbd> | 変換中に文節を選ぶ / 候補を切り替える |
| <kbd>F7</kbd> / <kbd>F10</kbd> | カタカナ / 英字 (英字にした語は次から英字になる) |
| <kbd>半角/全角</kbd> | 英数 ⇔ 日本語 |
| <kbd>Ctrl</kbd> + <kbd>半角/全角</kbd> | Meltype の一時停止 / 再開 |

よく使う言葉は、トレイのアイコンを右クリック →「ユーザー辞書...」で登録できます。
くわしい使い方は [docs/USAGE.md](docs/USAGE.md) へ。

<br>
<img src="docs/images/headings/faq.svg" alt="よくある質問" width="560">

<details>
<summary>タスクバーの IME の表示がずっと「A」のまま</summary>

故障ではありません。Meltype が Windows の IME を OFF にして、代わりに入力を受け持っているためです。
今のモードは、入力欄に入ったときにカーソルの近くに出る「あ」「A」か、トレイのアイコンで分かります。

</details>

<details>
<summary>Google 日本語入力など、ほかの IME も使いたい</summary>

<kbd>Ctrl</kbd> + <kbd>半角/全角</kbd> で Meltype を一時停止してから使ってください。

</details>

<details>
<summary>英語のつもりがかなに / かなのつもりが英字になった</summary>

<kbd>F10</kbd> (英字) か <kbd>F6</kbd> (ひらがな) で直して確定すると、次からその語は直した方になります。
トレイの右クリック →「自動判定の強さ」でも調整できます。

</details>

<details>
<summary>おかしな動きを見つけた</summary>

トレイのアイコンを右クリック →「不具合の報告・提案...」から送れます。
「どのアプリで」「何と打って」「どうなったか」を書いてもらえると助かります。

</details>

<br>
<img src="docs/images/headings/privacy.svg" alt="プライバシー" width="560">

打った内容をネットワークに送ることはありません。

- 通信するのは、自動更新で新しい版を確かめるとき (送るのは今の版だけ) と、自分で開いた不具合報告のフォームだけ
- 保存するのは `%LOCALAPPDATA%\Meltype` の設定・学習データ・ユーザー辞書と、ファイルログを ON にしたときのログだけ

セキュリティの方針と脆弱性の報告先は [SECURITY.md](SECURITY.md) にあります。

<br>
<img src="docs/images/headings/license.svg" alt="ライセンス" width="560">

**GNU General Public License v3.0** ([LICENSE](LICENSE)) で公開しています。
個人・会社でそのまま使うのも、GPL v3 の条件 (改造版もソースを公開) で改造・再配布するのも自由です。

ソースを公開せずに製品に組み込みたいなど、GPL v3 の条件で使えない場合は、メールでご相談ください: ibutya0319@gmail.com

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

- ソースファイルの先頭には `SPDX-License-Identifier: GPL-3.0-or-later` を付けています
- 同梱の .NET ランタイム (MIT) と、使っている Windows の機能は [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) に
- ロゴ ([docs/meltype.jpg](docs/meltype.jpg)・[docs/images/logo.png](docs/images/logo.png)) は [@Crysta1221](https://github.com/Crysta1221) さんの作品です
- アプリの版・著作権・ライセンスは、トレイの「Meltype について...」でも見られます

</details>

<br>
<img src="docs/images/headings/thanks.svg" alt="協力してくださった方々" width="560">

テスト版で不具合の報告や意見をくださった方々です。ありがとうございました (敬称略)。

くらいど！ ([@Kuraido8888](https://x.com/Kuraido8888)) ・
しぐれ ([@Akisameee0465](https://x.com/Akisameee0465)) ・
琴音Link ・
あげちゃ ・
うな ([@una08142009](https://x.com/una08142009)) ・
かふぇらて ([@cafely_latte](https://x.com/cafely_latte)) ・
ウパー ([@upah_setu](https://x.com/upah_setu)) ・
Ray ・
うぽつです ([@up2ds](https://x.com/up2ds))

<br>
<img src="docs/images/headings/develop.svg" alt="開発に参加する" width="560">

- 不具合の報告・辞書の追加・Pull Request の送り方 (CLA を含む) → [CONTRIBUTING.md](CONTRIBUTING.md)
- ソースからのビルド・テスト・動作の仕組み → [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)
