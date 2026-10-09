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

日本語の中に英単語を混ぜるたびに 半角/全角 を押すのって、地味に面倒ですよね。
Meltype は、打っている文字が日本語か英語かを見分けて、自動で切り替える日本語入力です。

```
kyouhagoogledekensaku  →  今日はgoogleで検索
```

Windows 10 / 11 で使えます。Mac 版と Linux 版はプレビュー版です。

<br>
<img src="docs/images/headings/features.svg" alt="できること" width="560">

**ローマ字のまま、日本語と英語を混ぜて打てます。**
`google` や `github` のような英単語は英字のまま、それ以外はかなや漢字になります。英文を打てば、そのまま英文が入ります。

**コードを書くときにも邪魔をしません。**
VS Code やターミナルでは英数が基本で、コメントや文字列の中だけ日本語を打てます。AI エージェントの `/command` や `@ファイル名` も、変換されずにそのまま入ります。

**打った内容は外に送りません。**
判定も変換も、すべて PC の中で行います。

ほかにも、絵文字の変換 (えがお → 😊) や、よくある書き間違いの指摘 (ブレスレッド → ブレスレット) ができます。

<br>
<img src="docs/images/headings/install.svg" alt="インストール" width="560">

1. [Releases](https://github.com/yksr-melt/Meltype/releases/latest) から `Meltype-<version>-setup.exe` をダウンロードして開く
2. 画面の案内に沿って進める
3. タスクトレイに「あ」のアイコンが出たら完了

管理者権限はいりません (Meltype IME を入れるときだけ確認が出ますが、断っても使えます)。新しい版が出たら自動で更新されます。

「Windows によって PC が保護されました」と出たときは、「詳細情報」→「実行」で進めてください。コード署名をしていないので、この表示が出ることがあります。

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
<summary>「ウイルスを検出しました」と出たとき</summary>

SmartScreen の警告とは別のものです。誤検知のこともありますが、検出名だけでは見分けられません。

1. Windows セキュリティ →「ウイルスと脅威の防止」→「保護の更新」で定義を新しくする
2. 公式のリリースからダウンロードし直して、もう一度検査する
3. それでも検出されるときは、保護を切ったりフォルダーを除外したりせずに、「保護の履歴」で検出名を確かめて [Issues](https://github.com/yksr-melt/Meltype/issues) で教えてください。個人名やパス、ダウンロード URL の一時トークンは隠してください

ファイルが本物か確かめたいときは、リリースのページにある SHA-256 と比べられます。PowerShell なら `Get-FileHash .\Meltype-<version>-windows.zip` です。

</details>

<details>
<summary>アンインストール</summary>

トレイのアイコンを右クリックして「アンインストール...」を選ぶか、Windows の「設定」→「アプリ」から消せます。
インストーラーで入れた場合は、最後に設定と学習データも消すかを聞かれます。zip 版は `Uninstall.cmd` でも消せて、こちらは設定と学習データもまとめて消えます。

</details>

<br>
<img src="docs/images/headings/start.svg" alt="使い始める" width="560">

メモ帳やブラウザーで、そのままローマ字で打ってみてください。

Meltype IME を入れた場合は、<kbd>Win</kbd> + <kbd>Space</kbd> で「Meltype」を選びます。文字は入力欄に直接入り、候補は入力位置の下に出ます。
入れていない場合は、カーソルの下に小さな変換ボックスが出ます。

| キー | |
|---|---|
| <kbd>Enter</kbd> | 確定 |
| <kbd>Space</kbd> | 漢字に変換 |
| <kbd>F7</kbd> / <kbd>F10</kbd> | カタカナ / 英字 |
| <kbd>半角/全角</kbd> | 英数と日本語の切り替え |
| <kbd>Ctrl</kbd> + <kbd>半角/全角</kbd> | Meltype を一時停止 |

<kbd>F10</kbd> で英字にして確定した語は、次から英字で出るようになります。
よく使う言葉は、トレイのアイコンの右クリックメニューにある「ユーザー辞書...」で登録できます。
細かい使い方は [docs/USAGE.md](docs/USAGE.md) にまとめています。

<br>
<img src="docs/images/headings/faq.svg" alt="よくある質問" width="560">

<details>
<summary>タスクバーの IME の表示がずっと「A」のまま</summary>

故障ではありません。Meltype が Windows の IME を止めて、代わりに入力を受け持っているためです。
今のモードは、入力欄に入ったときにカーソルの近くに出る「あ」「A」や、トレイのアイコンで分かります。

</details>

<details>
<summary>Google 日本語入力など、ほかの IME も使いたい</summary>

<kbd>Ctrl</kbd> + <kbd>半角/全角</kbd> で Meltype を一時停止すれば使えます。

</details>

<details>
<summary>英語のつもりがかなに、かなのつもりが英字になった</summary>

<kbd>F10</kbd> (英字) か <kbd>F6</kbd> (ひらがな) で直してから確定すると、次からはその語を直した方で出します。
トレイの右クリックメニューの「自動判定の強さ」でも調整できます。

</details>

<details>
<summary>おかしな動きを見つけた</summary>

トレイのアイコンの右クリックメニューにある「不具合の報告・提案...」から送ってください。
どのアプリで、何と打って、どうなったかを書いてもらえると助かります。

</details>

<br>
<img src="docs/images/headings/privacy.svg" alt="プライバシー" width="560">

Meltype はキーボードの入力を見て動きますが、打った内容をネットワークに送ることはありません。

通信するのは、自動更新で新しい版があるかを確かめるときと、自分で開いた不具合報告のフォームだけです。
PC に保存するのは、`%LOCALAPPDATA%\Meltype` の設定・学習データ・ユーザー辞書と、ファイルログを ON にしたときのログだけです。

セキュリティの方針と、脆弱性を見つけたときの連絡先は [SECURITY.md](SECURITY.md) にあります。

<br>
<img src="docs/images/headings/license.svg" alt="ライセンス" width="560">

Meltype は [GNU GPL v3.0](LICENSE) で公開しています。
個人でも会社でも無料で使えますし、GPL v3 の条件 (改造したらソースも公開する) を守れば、改造や再配布も自由です。

ソースを公開せずに製品へ組み込みたいなど、GPL v3 の条件では使えない場合は、メールでご相談ください: ibutya0319@gmail.com

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
<img src="docs/images/headings/thanks.svg" alt="協力してくださった方々" width="560">

テスト版を使って、不具合の報告や意見をくださった方々です。本当にありがとうございました (敬称略)。

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

不具合の報告や辞書の追加、Pull Request はいつでも歓迎です。
送り方は [CONTRIBUTING.md](CONTRIBUTING.md)、ビルドや仕組みは [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) にまとめています。
