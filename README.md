# Meltype

**雪解けのように、半角/全角の壁を溶かす日本語入力。**

半角/全角 キーを押さなくても、日本語と英語を打ち分けられるようにする Windows 常駐ツールです。
(開発中は AutoIME という仮の名前でした。以前の設定と学習データは、Meltype の初回起動時に自動で引き継ぎます)

Windows 版のほか、Mac 版の試作があります ([mac/README.md](mac/README.md))。

動作モードは 2 つあり、タスクトレイのメニューで切り替えます。

| モード | 動き |
| --- | --- |
| **Meltype キーボード** (既定) | Meltype 自身の変換ボックスで入力する。ローマ字はかなに、英単語 (`google` `github` …) は自動で英字のまま。Space で漢字に変換、Enter で確定 |
| **IME 自動切替** | 打ち始めの数文字から日本語と判定したときだけ Microsoft IME を ON にする。変換は Microsoft IME が行う |

もとの設計は [docs/AutoIME_technical_design_v2.md](docs/AutoIME_technical_design_v2.md) を参照してください (開発時の仮の名前 AutoIME のときに書いたものです。Meltype キーボードは設計書の後に追加した機能です)。

## インストール

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Meltype.ps1
```

ビルドしてスタートアップに登録し、起動します。タスクトレイに「あ」のアイコンが出れば動いています。
`-ExecutionPolicy Bypass` はこのコマンドにだけ効き、PC 全体の設定は変えません。

アンインストール:

```powershell
powershell -ExecutionPolicy Bypass -File .\Uninstall-Meltype.ps1            # 停止とスタートアップ解除
powershell -ExecutionPolicy Bypass -File .\Uninstall-Meltype.ps1 -RemoveData # 設定と学習データも削除
```

必要なもの: Windows 10 / 11 (x64 / ARM64)、.NET 10 SDK、Microsoft IME (漢字変換に使います)。

### 協力者に渡すテスト版

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-Package.ps1
```

`dist\Meltype-test-<日付>.zip` ができます。中身はビルド済みの `app` フォルダー、`Install.cmd` / `Uninstall.cmd` (ダブルクリックで実行)、協力者向けの `README.txt` です。
.NET ランタイムを同梱しているので、協力者の PC に .NET は不要です。この環境は NuGet が使えないため、自己完結ビルドの代わりに、この PC にインストール済みの .NET ランタイムを `app\dotnet` にコピーし、`Meltype.exe` がそこを使うようにしています (`AppHostDotNetSearch=AppRelative`)。

スマホからも受け取れる大きさ (30MB 未満、現在約 24MB) にするため、ランタイムから Meltype が使わない部品を削っています。

1. Meltype.dll が使う型から参照をたどり、要らないアセンブリを削る (`Meltype.Tests -- --runtime-closure`)
2. 自己診断 (`Meltype.exe --selftest`) を走らせ、実際に読み込まれなかった大きなアセンブリ (XML・ネットワーク・暗号など) を削る
3. もう一度自己診断を走らせ、削りすぎていないことを確かめる (失敗したら zip を作らない)

自己診断は、設定・判定・辞書・変換エンジン・Windows の候補 API・UI Automation・各画面・タスクトレイ・データの保存を、キーボードフックを掛けずに一通り動かします。UI Automation は WPF に頼らず COM で直接使っているので、WPF 一式は同梱していません。
インストール先は `%LOCALAPPDATA%\Programs\Meltype` で、管理者権限は不要です。配布用のファイルの元は [packaging/](packaging/) にあります。

## Meltype キーボード

文字入力欄で英字を打つと、カーソルの下に変換ボックスが出ます。Enter を押すまで、入力欄には何も入りません。

### キー操作

| キー | 動作 |
| --- | --- |
| 英字・記号 | 変換ボックスに追加する (ローマ字はかなで表示) |
| Space | 文節に区切って漢字に変換する。英単語で終わっているときは変換せず、確定して空白を入れる |
| ← → | 文節を選ぶ。Space を押す前でも、矢印キーで文節の選択に入る |
| Space / ↓ ↑ (変換中) | 選んでいる文節の候補を切り替える |
| Shift + ← → (変換中) | 文節の区切りを 1 文字ずらす |
| Enter | 確定して入力欄に入れる |
| BackSpace | かな 1 音 (きょ・っ など) ずつ消す。変換中なら変換をやめてかなに戻す |
| Esc | 変換中なら変換をやめる。そうでなければ入力を取り消す |
| F6 / F7 / F9 / F10 | ひらがな / カタカナ / 全角英数 / 半角英数 にする |
| 半角/全角 | 変換ボックス内: 日本語 ⇔ 英字。ボックスが出ていないとき: 日本語入力 ⇔ 英数 (直接入力) |
| その他のキー・Ctrl+… ・クリック | 確定してから、そのキーやクリックをアプリに通す |

### ローマ字の綴り

基本は Microsoft IME と同じです。

- ん: `n` (子音の前) / `nn` / `xn`。`tanni` → たんい、こんにちは は `konnnichiha`
- 小書き文字: `x` か `l` を付ける (`xtu` `ltsu` → っ、`xa` `la` → ぁ、`xya` → ゃ)
- そのほか: `who` `ulo` → うぉ、`vu` → ゔ、`thi` → てぃ、`dhi` → でぃ、`ye` → いぇ、`wi` → うぃ
- `-` → ー、`,` → 、、`.` → 。、`[` `]` → 「」、`/` → ・ (英文の続きなら半角のまま)。これらの記号から打ち始めても変換ボックスに入る
- 数字: 数字から打ち始めても変換ボックスに入る。数字だけなら半角のまま表示する。Space を押すと、英文の中なら確定して空白を入れ、それ以外は変換して ①・一・Ⅰ・１ などの候補を出す (1 番目は半角の数字のまま)。数字の後にかなが続けば日本語 (`3ji` → 3時)。変換エンジンが全角にした数字は半角に戻す

### 英語の自動判定

変換ボックスの中で、英単語の部分だけを自動で英字にします。

- ローマ字として読めない英単語はすぐ英字になる: `google` `github` `hello` `npm` (途中の「ごおｇぇ」にはならない)
- 日本語の中の英単語だけが英字になる: `kyouhagoogledekensaku` → 今日はgoogleで検索
- 英語の固有名詞は、ローマ字として読めても英字: `amazon` `adobe` `netflix` (辞書: `propernouns.txt`)
- 大文字で打ち始めた語は英字: `Tokyo`、1 文字の `I` も英字
- `sushi` `repo` のように英語とも日本語とも読める語は、**入力欄のカーソルの前後の文字**で決める。前後それぞれ英語なら +1・日本語なら −1 と数え、合計がプラスなら英字、それ以外は日本語
  - 「I love ｜ is great」の間 → sushi、「今日は｜」の後 → すし、前後が食い違う・分からない → すし
  - 前が**英文** (空白で区切った英単語が 2 語以上続いて空白で終わる: `I want `) なら +2。日本語の文の中の英単語 1 つ (`今日は GitHub `) は +1
  - `no` `to` `ga` のように助詞と同じ形の 2 文字の語は、+2 以上のときだけ英字 (`GitHub` + `no` → GitHubの、`I want ` + `to` → to)。`is` `at` `my` のように子音で終わる語は +1 でよい
  - 1 文字の `a` `i` は英文の途中 (+2) なら英字
- 普通の英単語 (`meeting` `tomorrow` …) は同梱の辞書に加えて **Windows の英語スペルチェッカー** (ISpellChecker, en-US) で調べる。スペルチェッカーはローマ字の語 (`kore` `sore`) も単語と見なすほど緩いので、ローマ字として読めない語と、前後の文脈で英語と分かるときだけ使う
- 小書き文字の綴り (`mala` = まぁ、`xtu` = っ) で最後まで読める語は、同梱の辞書の英単語でない限り日本語
- Space を押した時点でかなにならない子音が残る英単語 (`my` `by`) は、変換せずに英字で確定して空白を入れる
- 文脈が分からないまま日本語で確定した語は、後で英語だと分かったら確定し直す。Space で区切って続けた語はまとめて直す (`make sure you` → まけすれよう → `have` で英文と分かり `make sure you have`)。助詞と同じ形の 2 文字の語 1 つだけ (`Google と Apple`) は直さない
- ユーザーが自分で直した語を覚える: 英語とも日本語とも読める語 (api など) を F10 / 半角/全角 で英字にして確定するか、変換の候補から打ったままの英字を選んで確定したら、次からその語は英字にする (日本語の文の中でも: apiのerror)。F6 / F7 でかなにして確定したら、次からかなにする。`%LOCALAPPDATA%\Meltype\languages.json` に「打った英字 → 英語か」だけを保存し、トレイの「学習データをリセット」で消える
- 変換の候補の最後には、打ったままの英字と全角の英字も出る (あぴ → … → api → ａｐｉ)。Space を連打して英字に戻せる
- 知っている英単語 (辞書・固有名詞・覚えた語) の最後の n の後に n + 母音を打ったときは、「nn → ん」とまとめずに英単語の n と次の音に分ける (pythonnobug → pythonのbug)。日本語の語は今までどおり nn → ん (こんにちは は konnnichiha)

### もしかして (書き間違いの指摘)

変換ボックスの読みに、よくある書き間違いがあると「もしかして: ブレスレット (Tab)」と案内し、Tab で正しい読みに直します (変換中なら直して変換し直す)。

- 辞書 `misspellings.txt` に書いた誤り (シュミレーション → シミュレーション、バトミントン → バドミントン …)
- 辞書に書いた正しい形 (5 文字以上) と、濁点・半濁点・促音 (ッ)・長音 (ー)・小書き文字だけが違う綴り (ブレスレッド、ブレースレット → ブレスレット)
- どちらの綴りも使われる語 (バイオリン / ヴァイオリン) や、誤りと同じ綴りの別の語がある組 (バック / バッグ) は入れていない
- `%LOCALAPPDATA%Meltypedictionariesmisspellings.txt` に同じ形式で書くと追加できる

### 自動判定の強さ

トレイの「自動判定の強さ」か、設定の「判定」で選びます。Meltype キーボード (ローマ字入力・かな入力・英数状態の検知) と IME 自動切替のすべてに効きます。

| 強さ | 変換ボックス | IME 自動切替・英数状態の検知 |
| --- | --- | --- |
| 積極的 | 英単語の先頭 (3 文字以上) や、片側が英語の短い語でも英字 | 閾値 −1 |
| 標準 (既定) | 上の規則どおり | 閾値そのまま |
| 慎重 | ローマ字として読めない英単語・大文字始まり・英文の途中の語だけ英字 | 閾値 +3 |
| 手動 | Shift で打った大文字始まりの語だけ英字。ほかは「Tab → google (英字に)」と提案し、Tab で英字にする | 切り替えずに通知だけ (30 秒に 1 回) / 英数状態の検知はしない |

### かな入力 (JIS)

設定の「入力方式」を「かな入力 (JIS)」にすると、変換ボックスに JIS かな配列で入力します (Shift+E = ぃ、Shift+Z = っ、Shift + ね = 、、濁点・半濁点は直前のかなに付く)。
打ったキーの英字 (`google` のキー → きららきりい) が英単語で、かなとしては辞書の日本語の語 (の先頭) にならなければ英字で見せます。かなとしても日本語になる語は、ローマ字入力と同じく前後の文脈で決めます。

### 入力モードの表示

Meltype キーボードの使用中は Windows の IME を OFF にしているので、タスクバーの IME の表示は常に「A」になります。今のモードは次で知らせます。

- 入力欄 (パスワード以外) に入ったときと 半角/全角 を押したとき、カーソルの近くに「あ」(日本語) か「A」(英数) を 1.2 秒だけ出す (`ModeIndicatorWindow`、フォーカスもクリックも奪わない。設定の「入力モードをカーソルの近くに表示」で OFF にできる)
- タスクトレイのアイコン

### アプリの種類 (コードエディター・ターミナル)

アプリ別設定の「種類」を **コード** にしたアプリ (既定で VS Code・Cursor・Visual Studio・JetBrains の IDE・Sublime Text・Notepad++・Windows Terminal・PowerShell・コマンドプロンプトなど) では、基本は英数のまま通し、日本語を書く場所だけ判定します。

- コードの中はキーをそのままアプリへ通す (変換ボックスを出さないので、補完もそのまま効く)
- 今の行のキャレットより前がコメント (`//` `#` `--` `/* …` `<!-- …` `REM`、複数行コメントの ` * ` 行) か、文字列 (`"…` `'…` `` `… ``) の中なら、ふつうどおり日本語を判定する。`#include` `#region` のような指令や `x--` はコメントとみなさない
- 今の行は、打ったキーと確定した文字から追いかける。矢印キー・クリック・Ctrl の操作などでキャレットが動いたら、UI Automation でキャレットより前を読み直す
- コードの行で 半角/全角 を押すと、その行だけ日本語で入力できる (改行で戻る)
- README.md などの文章ファイルを開いているとき (ウィンドウのタイトルが .md .txt など) は一般として扱う
- コメント・文字列に入ったとき / 出たときは、カーソルの近くに「あ」「A」を出して知らせる
- 「コード」のアプリでも、チャット・AI への入力欄 (VS Code の Copilot Chat、Claude Code の画面など。UI Automation の名前に chat / copilot / claude / prompt / message などが入るもの) は一般として扱う。VS Code・Cursor などの Electron 製のエディターは入力欄の種類が多いので、エディター・ターミナル・検索と分かるものだけをコードとする
- ターミナルで動く AI の入力行 (Claude Code・Gemini CLI の「> 」、Codex の「› 」) は日本語を判定する。ターミナルでは改行のたびにプロンプト (出力) を読み直す。ターミナルで 半角/全角 を押して日本語にしたら、別の場所に移るまで日本語のまま
- IME 自動切替モードでも、コードの中では判定しない

### 絵文字・顔文字

変換して Space / ↓ で候補を切り替えると、変換エンジンの候補の後に絵文字・顔文字が出ます (えがお → 😊 😄 (^^)、かおもじ → 顔文字の一覧、ねこ → 🐱 (=^・^=)、かんがえるかお → 🤔、にほん → 🇯🇵 など)。変換ボックスの候補の一覧でも、絵文字は Direct2D + DirectWrite でカラーで表示します。

- `dictionaries/emoji.txt`: 手で書いた絵文字・顔文字 (顔文字には空白や # が入るので区切りはタブ)。先に並ぶ
- `dictionaries/emoji-cldr.txt`: Unicode CLDR の絵文字の日本語の名前・キーワード (約 2,300 個の絵文字、約 5,000 の読み) から自動で作ったもの。読みは Microsoft IME の変換エンジンの逆変換で求めた (`--gen-emoji` で作り直せる)。Unicode License v3
- 読み全体が辞書の語なら (かんがえるかお)、変換エンジンが文節に分けても 1 つの文節として候補を出す
- `%LOCALAPPDATA%\Meltype\dictionaries\emoji.txt` に同じ形式で書くと追加できる

### ほかの IME との併用

Meltype キーボードを使っている間は、Windows の IME (Microsoft IME・Google 日本語入力など) を OFF に保ちます (250ms ごとに確認)。Win+Space などで IME を切り替えると新しい IME が ON で始まることがあり、そのままだと Meltype が通したキーを IME が変換してしまうためです。IME を ON にし続ける場合は 10 秒に 3 回までで奪い合いをやめます。キーボード/IME の切り替えはログに残ります。

### 変換

変換には Windows に入っている Microsoft IME の変換エンジン (IFELanguage) を使います。Meltype 自身は大きな辞書を持ちません。

- **ライブ変換**: 4 文字以上の日本語は、Space を押さなくても打ったそばから漢字で表示します。短い語は Space で変換してください (設定で OFF にできます)。
- **候補の一覧**: Space や ↓ で候補を切り替え始めると、Windows 標準の変換候補 API から候補の一覧を取ります (はし → 橋・端・箸・葉氏 …)。並びは 文の中での変換結果 → Windows の候補一覧 → その文節だけでの変換結果 → 補助辞書 → ひらがな → カタカナ。英語の文節は 打ったまま → 固有名詞の正しい形 (iPhone) → 先頭大文字 → すべて大文字 → 全角。
- **文脈に合わせた変換**: 最初の候補は次の順で決めます。
  1. 文脈の手がかり辞書: 前後に手がかりの語があればその候補 (気温 → 暑い、財布 → 革、ご飯 → 箸)
  2. 学習: 前に選び直した変換
  3. 変換エンジンの結果: カーソルの前の日本語 (同じ文の最大 10 文字) を文脈として渡している (この本は + あつい → 厚い)

### ユーザー辞書

トレイの「ユーザー辞書...」で、読みと単語を登録できます。変換では最優先に使います。

- 読みはローマ字でも入力できます (`kigoutou` → きごうとう)。読みを打つと変換候補がプルダウンに出るので、選ぶか単語欄に直接打って「登録」
- 変換する読みの中に登録した読みが含まれていれば、その部分は変換エンジンの区切りに関係なく登録した単語になります (きごうとう → 記号等 を登録すると、`kigoutoufukume` → 記号等｜含め)
- 読みは 2 文字以上。保存先は `%LOCALAPPDATA%\Meltype\userdict.txt`

### 確定した後の自動修正

英語とも日本語とも読める語 (`i` `sushi` など) を確定した後、次の語で英語か日本語かがはっきりしたら、自動で確定し直します。

- `i` を Space で変換して確定 → 続けて `want` と打つ → 「I want」 (代名詞の i は I に、Space の分の空白も入れる)
- 英文の続きで `sushi ` と確定 → 続けて `gasuki` と打つ → 「すしがすき」
- 候補を自分で選び直したとき、確定の後にカーソルが動いたとき (Meltype を通らないキーやクリック) は直しません
- 設定の「確定後も文脈に合わせて直す」で OFF にできます

### 英数状態 (直接入力)

変換ボックスが出ていないときに 半角/全角 を押すと英数状態になり、打った文字がそのまま入ります (トレイのアイコンが「A」になります)。

英数状態でも、単語の打ち始めの数文字を保留して判定し、ローマ字 (日本語) だと分かったら自動で日本語入力に戻して変換ボックスに入れます。英語ならそのまま入り、同じ単語の続きは保留しません。0.7 秒打たなければ、保留していた文字を英語として出します (設定で OFF にできます)。

### 変換ボックスが出ない場面

- 文字入力欄以外 (Gmail の `j`/`k` のようなショートカット、エクスプローラー、ゲーム)。UI Automation でフォーカスのある要素を調べて判断します
- パスワード欄
- 管理者権限のアプリ、全画面のアプリ、アプリ別設定で OFF にしたアプリ (既定でリモートデスクトップと VM は OFF)

出ない理由はトレイの「ログ / 判定理由」に「フォーカス → 入力欄ではない: …」のように残ります。
Meltype キーボードの使用中は、二重に変換しないよう Microsoft IME を OFF にしておきます。

## IME 自動切替

Microsoft IME を OFF (半角英数) のまま打ち始めると、打ち始めの数文字を保留して判定し、日本語なら IME を ON にしてから保留した文字を IME に渡し直します。

- `konn` `wata` `kyou` などの時点で日本語と判定する
- `hello` `github` `npm` などは、ローマ字として成立しなくなった時点 (多くは 1〜3 文字目) で英語と確定し、そのまま出す
- `kana` `sushi` `radio` `test` のような判断できない語は何もしない
- 誤って日本語に切り替わったときは、そのまま 半角/全角 で戻す。その語は次から切り替えなくなる (学習)。逆に切り替わらなかった語で IME を ON にすると、次から切り替わるようになる

## タスクトレイと設定

- アイコン: 青い「あ」= 日本語入力、青い「A」= 英数、灰色の「A」= 一時停止
- **Ctrl + 半角/全角**、アイコンのダブルクリック、または Ctrl+Alt+F12 で一時停止/再開 (Ctrl+Alt+F12 が他のアプリと重なっていたら Ctrl+Alt+F11 → Ctrl+Shift+Alt+A → Ctrl+Alt+Pause の順に試します)
- 右クリック: 動作モードの切替、設定、ユーザー辞書、ログ / 判定理由、データフォルダを開く、学習データのリセット、終了
- 設定画面: ON/OFF や選択肢はプルダウン、数値は数値入力、アプリ別設定は表 (ON/OFF はプルダウン) で変更します。項目にマウスを乗せると下に説明が出ます
- 設定画面の「判定テスト」欄に英字を打つと、IME 自動切替の判定結果と理由を確認できます

## 動作の仕組み

どちらのモードも、キーボードフック (`WH_KEYBOARD_LL`) を専用スレッドで受けます。Meltype が送り直したキーには印 (`dwExtraInfo = "MELT"`) を付け、自分では判定しません。

Meltype キーボード:

```
物理キー ─▶ KeyboardMonitor ─▶ CaptureGate (変換ボックスが開いている間は、キーとクリックをすべて順番どおりに保留)
                                   │
                                   ▼ (UI スレッド)
                           CompositionController ◀─ CompositionDetector (英語の区間の判定)
                                   │                  MsImeKanjiConverter (Microsoft IME の変換エンジン)
                                   │                  ContextRules / ConversionHistory / CandidateDictionary
                                   │                  FocusInspector (UI Automation: 入力欄か・カーソルの前後の文字)
                                   ▼
                  変換ボックスに表示 ─▶ Enter で確定した文字列を SendInput (Unicode) で入力欄へ
```

IME 自動切替:

```
物理キー ─▶ KeyboardMonitor ─▶ InputSession (Idle → Collecting → Flushing → Committed)
                                   │  Collecting 中は打鍵をすべて保留 (キーアップ・記号も届いた順に)
                                   ▼
                             ScoreEngine ◀─ RomajiDetector / KanaDetector / DictionaryDetector
                                   │         EnglishDetector / TypoDetector / UserModel
                                   ▼
              日本語 ─▶ ImeController で IME を ON ─▶ 保留分を送り直す ─▶ Microsoft IME が処理
              英語 / 不明 ─────────────────────────▶ 保留分をそのまま送り直す
```

- 日本語と判定する条件: `JapaneseScore >= 閾値` かつ `JapaneseScore - EnglishScore >= 閾値` (既定の閾値 4)
- 保留は最大 6 文字、無入力 0.7 秒、最初の打鍵から 2.5 秒で打ち切り、そのまま出す
- IME の切替は IMM32 → TSF → `VK_IME_ON` の順に試し、すべて失敗したら切り替えずにそのまま出す

## データと辞書

保存場所はすべて `%LOCALAPPDATA%\Meltype\` です。ネットワークには何も送りません。

| ファイル | 内容 |
| --- | --- |
| `config.json` | 設定 (トレイ → 設定... から編集) |
| `model.json` | IME 自動切替の学習データ。判定に使った先頭 3〜6 文字ごとの回数だけで、入力内容そのものは保存しない |
| `conversions.json` | 変換で選び直した結果 (文節の読み → 選んだ文字列)。次から最初の候補になる |
| `userdict.txt` | ユーザー辞書 (1 行に「読み[Tab]単語」)。トレイの「ユーザー辞書...」で編集する |
| `languages.json` | ユーザーが F10 / F6 などで英字 / かなに直して確定した語 (英語か日本語か) |
| `meltype.log` | 設定で「ファイルにログを書く」を ON にしたときだけ |

学習データは、トレイの「学習データをリセット」で `model.json` と `conversions.json` の両方を消せます。

組み込みの辞書は [dictionaries/](dictionaries/) にあり、ビルド時に埋め込まれます。
`%LOCALAPPDATA%\Meltype\dictionaries\` に同じ名前・同じ形式のファイルを置くと、組み込みの辞書に追加されます (反映には再起動が必要)。

| ファイル | 形式 | 用途 |
| --- | --- | --- |
| `japanese.txt` | ローマ字の語 (ヘボン式) | 日本語の判定 |
| `english.txt` | 英単語 | 英語の判定 |
| `propernouns.txt` | 大文字小文字どおりの固有名詞 (`GitHub`) | ローマ字として読めても英語にする語と、変換候補の正しい形 |
| `candidates.txt` | `読み 候補 候補 …` | 同音異義語の補助 (Windows の候補一覧に無い語を足す) |
| `contexts.txt` | `読み 候補 : 手がかり …` | 前後に手がかりの語があればその候補を最初にする |

## 開発

```powershell
dotnet build Meltype.sln
dotnet run --project src/Meltype.Tests                                   # テスト (Windows: 共通のテスト + Windows のスペルチェッカー)
dotnet run --project src/Meltype.Tests -- CompositionTests               # 名前に一致するテストだけ
dotnet run --project src/Meltype.Tests -- --explain konnichiwa hello      # 1 文字ずつの判定理由 (IME 自動切替)
dotnet run --project src/Meltype.Tests -- --convert きょうはいいてんき     # 変換エンジンの結果と文節の区切り
dotnet run --project src/Meltype.Tests -- --context この本は:あつい        # 文脈を渡したときの変換結果
dotnet run --project src/Meltype.Tests -- --eval                          # 品質テスト: カテゴリーごとの正解率と外れた例
```

### Mac・Linux でのテスト

OS に依存しない部分 (`src/Meltype.Core`: 英語 / 日本語の判定・ローマ字・変換ボックスの中身・辞書・学習・設定) と、そのテスト (`src/Meltype.Core.Tests`) は Mac・Linux でも動きます (Mac 版・Linux 版の土台)。.NET 10 SDK を入れて次を実行します。GitHub Actions でも Ubuntu と macOS で毎回流しています。

```bash
dotnet run --project src/Meltype.Core.Tests                 # すべてのテスト
dotnet run --project src/Meltype.Core.Tests -- --eval       # 品質テスト (スペルチェッカーは使わない)
```

### 品質テスト (採点テスト)

`src/Meltype.Core.Tests/QualityTests.cs` に、日本語の文・英文・混在・英語とも日本語とも読める語・英語の後の短い語・記号と数字・小書き文字・大文字・かな入力・コードの行 (コメント / 文字列の判定)・文章ファイルの判定・絵文字・もしかして の例をまとめてあります。期待値は「理想の結果」で書いてあり、`--eval` でカテゴリーごとの正解率と外れた例を表示します。通常のテストでは、全体 95% 以上・どのカテゴリーも 80% 以上を基準にして、ある直しで別の場所が壊れたら気づけるようにしています。Windows の英語スペルチェッカーが使える環境では実際と同じくそれも使います (`MELTYPE_NO_SPELLCHECK=1` で使わずに測れます)。新しい不具合の報告を受けたら、まずここに例を足してから直すのがおすすめです。

- `src/Meltype.Core/` — OS に依存しない部分 (Windows・Mac・Linux 共通)
  - `Composition/` 変換ボックスの中身 (英語の区間の判定・変換の流れ・候補・文脈・学習・ユーザー辞書・もしかして)
  - `Detection/` ローマ字・英語・辞書・かな・Typo の判定器
  - `Input/` キーの表し方、IME 自動切替の入力セッション、コードの行の判定
  - `Learning/` `Config/` `Diagnostics/` IME 自動切替の学習・設定・ログ
- `src/Meltype/` — Windows 版 (キーボードフック、変換ボックスの画面、Microsoft IME の変換エンジン・IMM32 / TSF、UI Automation、スペルチェッカー、トレイ)
- `src/Meltype.Mac.Native/` — Mac 版の IME から呼ぶ C の関数 (Meltype.Core を NativeAOT で dylib にする)
- `mac/` — Mac 版の IME (Swift, Input Method Kit。漢字変換は azooKey)。ビルドは `mac/build.sh`
- `src/Meltype.Core.Tests/` — 共通部分のテスト (判定・入力セッション・変換ボックス・学習・品質テスト)。Mac・Linux でも動く
- `src/Meltype.Tests/` — Windows 版のテストと調査用の道具 (共通部分のテストもまとめて流す)
- `dictionaries/` — 組み込み辞書

## 制限

- 変換候補の一覧は Windows 標準の変換候補 API (TextConversionGenerator) から取るため、Microsoft IME の候補ウィンドウとは順番や数が違うことがあります。
- 文脈に合わせた変換は、手がかり辞書 `contexts.txt` と学習にある範囲で効きます。
- カーソルの前後の文字は UI Automation で読むため、読めないアプリでは Meltype が最後に確定した文字列で代用します。Chrome などでは UI Automation を使うことで動作が少し重くなる可能性があります。
- 32bit Windows には対応していません。

## プライバシー

Meltype はキーボードの入力を監視して動くツールですが、打った内容をネットワークに送ることはありません (通信する処理がありません)。
保存するのは `%LOCALAPPDATA%\Meltype` の設定・学習データ・ユーザー辞書と、ファイルログを ON にしたときのログだけです。

## ライセンス

Meltype は **GNU General Public License v3.0** ([LICENSE](LICENSE)) と **商用ライセンス** のデュアルライセンスです。

- 個人・会社でそのまま使う、GPL v3 の条件 (改造版もソースを公開) で改造・再配布する → GPL v3 で無料
- 自社製品に組み込んで、ソースを公開せずに配布したい → 商用ライセンス ([COMMERCIAL.md](COMMERCIAL.md))

貢献の方法と貢献者ライセンス同意 (CLA) は [CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。

ソースファイルの先頭には `SPDX-License-Identifier: GPL-3.0-or-later` を付けています。配布用パッケージに同梱している .NET ランタイム (MIT ライセンス) と、実行時に使う Windows の機能は [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を参照してください。アプリのバージョン・著作権・ライセンスは、トレイの「Meltype について...」で確認できます。

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
