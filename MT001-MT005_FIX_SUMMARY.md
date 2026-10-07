# Meltype MT-001〜MT-005 問題点と修正点のまとめ

## 2026-10-07 最終確認（22:37 JST、ローカル）

この節が最新の検証結果で、以降の節は過去の記録。確認で見つけた再現可能な不具合を修正した。
通常の入力環境へのインストールと Windows 実機確認は行っていないため、製品全体に問題がないとは断定しない。

- **混在文の一部分の保護**: 日本語部分を変換しながら、メンション・URL・メール・パスの原文を保持する。
  `kyouha「@kuraido」ashita` の Enter / Space → Enter / Tab / focus を検査した。
  保護区間に誤字補正を適用せず、原文の境界をまたぐローマ字入力単位も正しく区切る。
- **境界での Backspace**: `./z]` / `@z[` では `z]` / `z[` が1入力単位になり、削除すると保護文字 `z` まで消えていた。
  保護区間に重なる末尾単位は原文を1文字ずつ削除し、再解析後も `./z` / `@z` を維持する。
- **品質コーパスの残り2件**: `my name is taro` は名前を表す英語の文脈で未知の名前を保持する。
  `apinoerror` は `apiのerror` と分割する。単独のローマ字、手動かな指定、日本語の学習選択は維持する。
- **入力処理の性能**: 範囲ごとの指標を前計算し、英語候補になれない範囲を早く除外する。
  辞書と学習データの不一致検索は文字列を作らず行う。外部スペルチェッカー・固有名詞の途中入力を維持する。
  固定乱数の8,000区間分け結果は最適化前の参照実装と一致し、別辞書の固有名詞接頭辞も回帰テストを追加した。
- **Macの実連携**: 製品の `InputController.handle` に `NSEvent` を渡し、実際のazooKey辞書で変換する。
  変換エンジンの保存先も `MELTYPE_DATA_DIR` に合わせ、一時設定・学習ディレクトリの外に書き込まない。
- **Linuxの実連携**: x86_64で配布物をビルドし、実GI・NativeAOT・Mozcで入力処理を検査した。
  私有IBusデーモンでは製品スクリプトのFactory生成、キー配送、候補、確定信号まで検査する。
  通常のデスクトップへの登録は行わない。常用環境の設定・サービスを変更せず、一時環境で検証した。

| 検証 | 結果 | 範囲 |
|---|---|---|
| Core | 236/236 PASS | MacとLinux、回帰テストを含む |
| 品質コーパス | 1243/1243 一致 | テストの閾値通過だけではなく全件一致 |
| NativeAOT build | PASS、警告0件 | macOS arm64 / Linux x86_64 |
| NativeAOT C ABI | 各23/23 PASS | 製品ライブラリ、変換コールバックはスタブ |
| SwiftとNativeAOTの接続 | 8/8 PASS | 製品NativeCore、変換はスタブ |
| Mac入力イベント・実azooKey | 22/22 PASS | 製品InputController、合成IMKTextInputクライアント |
| Linuxキー写像 | 12/12 PASS | 関数本体を読み込む単体検証 |
| Linux実GI・NativeAOT・Mozc | 10/10 PASS | 多数のトークン・操作・Unicodeの組み合わせを含む |
| Linux私有IBusデーモン | 10/10 PASS | 実Factory・入力配送・Mozc、合成入力コンテキスト |
| Windows向けクロスビルド | PASS | 既存のnullable警告5件。Windows実行はNOT_RUN |
| 読み取り専用の追加レビュー | 追加のmust-fixなし | 実測は担当が実施、同じworktreeの同時編集なし |

MT-005の比較基準はv1.0.0 `467255bfe3e36b803a3fd3f5a1480fe35d5058c9`。
Mac NativeAOTでは同じ合成ハーネスで64/128/256キー・4ケース・各15標本（ウォームアップ3回）を測定した。
全12ケースで処理時間が基準の50%以下となり、URL以外の確定出力も一致した。
現在の辞書を基準版にも埋め込む対照測定でも全12ケースが通過した。
これは変換をスタブにした測定で、実変換の測定とは区別する。

Mac合成測定の256キー中央処理時間（入力・JSON・Enter、起動と辞書読み込みを除外）:

| ケース | 基準版ms | 修正版ms | 修正版/基準版 |
|---|---:|---:|---:|
| URL | 3190.1885 | 6.5381 | 0.0020 |
| 自然な日英混在 | 1128.8458 | 158.4938 | 0.1404 |
| 日英混在の反復 | 1101.2167 | 171.1665 | 0.1554 |
| 日本語 | 1171.0802 | 186.4349 | 0.1592 |

Linuxでは同じ実GIアダプター・同じ固定Mozcヘルパーで、両版に `今日` の候補があることを確認してから測定した。
既定のライブ変換を有効にし、キー処理・C ABI・JSON・GIの表示オブジェクト・SpaceとEnterを計時した。
URL以外の確定出力は一致し（URLは原文保持の意図した差）、256キーの4ケースすべてで50%以下を達成した。
IBusデーモンの配送・起動・セッション生成・辞書読み込み時間は計時に含めない。

| ケース | 基準版ms | 修正版ms | 修正版/基準版 |
|---|---:|---:|---:|
| URL | 10510.7784 | 29.8505 | 0.0028 |
| 自然な日英混在 | 2361.5812 | 398.3391 | 0.1687 |
| 日英混在の反復 | 2106.9293 | 401.9205 | 0.1908 |
| 日本語 | 3411.5774 | 1300.9853 | 0.3813 |

実変換込みの64/128キーも測定した。全ケースで基準版より短縮したが、50%以下を満たすのは全12ケース中9ケース。
64キーの日英混在（0.5640倍）・日本語（0.7156倍）、128キーの日本語（0.5914倍）は50%以下に届かなかった。
MT-005の256キー基準の達成と、短い入力の追加測定結果を区別する。
先行したLinux性能測定は、基準版のヘルパーへの参照が無効だったため採用しない。上表は修正後の測定だけを使う。

追加検証の再実行（既に生成した配布物を使う）:

```sh
python3 tools/test-mac-events.py path/to/MeltypeNative.dylib --real-converter
python3 linux/tests/test_integration.py linux/build/Meltype-linux
xvfb-run -a dbus-run-session -- python3 linux/tests/test_daemon.py linux/build/Meltype-linux
python3 tools/benchmark-composition.py /path/to/baseline-checkout --output /path/to/results
```

mac/linuxワークフローにも追加検証を組み込んだ。GitHub Actionsの実行結果は未取得。
Windows実行、通常のIMK/IBus登録とデスクトップアプリの入力欄はNOT_RUN。
Push・GitHubコメント・レビュー投稿・マージ・常用IMEのインストールは未実施。
PRのheadは `ac8b504`、最新mainは `55c8a29`。競合解消と修正はローカルだけに存在する。
日時・SHA・入力およびログのハッシュは作業フォルダの `../validation/verification-complete.json` に記録する。

## 2026-10-07 再確認（20:22 JST、履歴）

前回の正常系テストでは取りこぼした不具合を再現し、追加で修正した。「問題がない」とは断定しない。

- **明示的な候補選択の確定**: `@kuraido` → Shift+Space → `＠くらいど` を選択しても、
  Enter / Tab / focus で原文 `@kuraido` が確定されていた。変換中は選択した候補を優先するよう修正した。
- **Tab の誤字補正**: `./shumire-shon` などが保護対象でも日本語の誤字補正で書き換わっていた。
  保護区間に重なる入力単位を補正対象から外し、通常の日本語の補正は維持した。
- **macOS の複数スカラー入力**: 結合文字から始まるイベントも、1 スカラーの場合と同じ確定経路を使う。
  `e` + U+0301 + U+0300 の原文、かな + 濁点・異体字セレクター、F6 の明示指定を検査した。
- **NativeAOT の学習データ読み込み**: 有効な `languages.json` / `conversions.json` /
  `translations.json` / `model.json` が型情報不足で読み込めなかった。
  学習データにも JSON source generation を適用し、既存のキー・プロパティ名と旧形式を維持した。
- **保護区間の検出時間**: 長い英字列で URL スキームの末尾を何度も走査していた。
  末尾を再利用し、候補関数も必要な順に呼ぶよう変更した。
  変更前のスキャナーと固定乱数 12,000 件の結果が一致した。英字 256 文字の単独 Scan は
  中央値 0.036000 ms → 0.003600 ms（11 標本、各 20 回、TieredCompilation 無効）。
  これはスキャナー単体のローカル測定で、IME 全体の MT-005 性能ゲートを達成した証拠ではない。
- 配布用 mac / linux ワークフローにも、生成した NativeAOT ライブラリを使うテストを追加した。
  ワークフローはローカル編集のみで、GitHub Actions の実行結果は未取得。

| 再確認 | 結果 | 範囲 |
|---|---|---|
| Core | 228/228 PASS | 候補選択、Tab、旧学習ファイルの読み込み・保存後の再読み込みを含む |
| 品質コーパス | 1241/1243 一致 | 既存の 2 不一致は残る。テストランナーの PASS と全件一致は別 |
| NativeAOT build | PASS、警告 0 件 | macOS arm64 |
| 製品の NativeAOT C ABI | 18/18 PASS | 学習ファイルを新しいセッションで読むケースを追加。変換はスタブ |
| Swift と NativeAOT の接続 | 8/8 PASS | 実際の NativeCore、InputController / KeyMapping はコンパイル。変換はスタブ |
| 学習 JSON の NativeAOT 読み込み・保存・再読み込み | 4/4 PASS | 製品 Core を参照する診断用 AOT ライブラリ、合成ファイル |
| 設定プロファイルの NativeAOT 往復 | PASS | 書き出し・取り込み・保存・再読み込み、合成データ |
| スキャナー比較 | 12,000/12,000 一致 | 変更前のローカル版との比較 |
| Linux キー写像 | 12/12 PASS | IBus スタブ |
| 読み取り専用の追加レビュー | 追加の具体的な不具合なし | 修正・テストを別担当が静的確認、同時編集なし |

追加テストの再実行:

```sh
python3 tools/test-mac-boundaries.py build-check/native/MeltypeNative.dylib
```

残る制約: 混在文の一部分だけを保護する処理、MT-005 の性能ゲート、品質コーパスの 2 不一致。
実際の `InputController.handle` に OS イベントを渡した検証、IMK / IBus GUI、実変換エンジン、Windows 実機は NOT_RUN。
GitHub の PR は head `ac8b504` のままで競合表示が残る。競合解消と追加修正はローカルのみ。
Push・コメント・レビュー投稿・インストールは実施していない。
検証ログと日時・SHA・ハッシュは、この作業フォルダの `../validation/verification-recheck.json` に記録する。

## 2026-10-07 初回ローカル検証（20:00 JST、履歴）

- 対象 PR: https://github.com/yksr-melt/Meltype/pull/10
- 最新 main の確認点: `55c8a29`。元の PR の head は `ac8b504`。
- `main` を merge し、メール保護の期待値を維持しながら Issue #12 の新しい英単語回帰ケースも残して競合を解消した。
- `@a@b` / `a@b@c` では先に見つけた区間を優先し、後のメール判定が前の保護区間を再利用しないようにした。
  Property Test は開始・終了のサロゲート境界に加え、区間の順序と非重複も直接検査する。
- 新しい `SpaceAroundEnglish` 設定が ON でも、保護文字列に自動で空白を追加しない。
  メンション・メール・URL・POSIX/Windows パスの Enter / Space / Tab / focus を検査した。
- BMP 結合アクセントの既知 FAIL を修正した。U+0300..U+036F が続くとき、未確定入力が ASCII 英字のみで、
  自動表示（または半角英数表示）の場合は原文を先に確定し、結合文字の元イベントを 1 回だけ通す。
  例: `e` + U+0301 は `é` のままで、`え́` にしない。正規化で合成済み文字へ置き換えることもしない。
  明示的なかな・全角表示、選択中の変換候補、かな入力は尊重する。濁点・異体字セレクターや通常の非BMP文字は現在の表示を確定する。
  結合文字の前に確定するときは自動空白を抑え、直後に英語があっても元の文字に結合できるようにする。通常の Enter 確定では空白設定を維持する。
  混在文の末尾だけを英字へ戻す処理や完全な grapheme 編集は対象外。
- 新規 C# ファイル 3 件の著作権表記を `0924haruto12` に修正した。既存ファイルの作者表記は維持した。
- 回帰テストは Core の実行入口に組み込み、C ABI の実際の exports もテストする。Linux のキー写像 12 件は IBus 不要のテストとして CI に追加した。
- NativeAOT 実行時の設定読み込みも修正した。設定の enum 用 JSON メタデータが不足し、有効な `config.json` が
  `.broken` として扱われて既定値へ戻っていた。設定とプロファイルの JSON メタデータを source generator で生成し、
  既存の文字列・数値 enum、コメント、末尾カンマ、プロファイル操作との互換性を検査した。

ローカルでの検証結果（macOS arm64、.NET SDK 10.0.401）:

| 検証 | 結果 | 条件 |
|---|---|---|
| 最新 main との競合解消後、追加修正前の Core | 213/213 PASS | 既存テスト |
| 追加修正後の Core | 225/225 PASS | 保護・C ABI・設定 JSON 回帰を含む |
| NativeAOT 共有ライブラリ | build PASS | JSON trimming / AOT の警告あり |
| 実際の NativeAOT C ABI | 17/17 PASS | macOS arm64、変換はスタブ、設定・学習は一時ディレクトリ |
| Linux `virtual_key` | 12/12 PASS | IBus はスタブ、関数本体は製品コードから読み込む |

再実行:

```sh
dotnet run --project src/Meltype.Core.Tests -c Release
python3 linux/tests/test_virtual_key.py
dotnet publish src/Meltype.Mac.Native -c Release -r osx-arm64 -p:PublishAot=true -p:NativeLib=Shared --source https://api.nuget.org/v3/index.json -o build-check/native
python3 tools/test-native-boundaries.py build-check/native/MeltypeNative.dylib
```

MT-005 の性能ゲートは未達のままで、今回の修正では性能の再測定をしていない。
Core の品質コーパスには既存の 2 不一致（`my name is taro`、`apinoerror`）が残る。
macOS IMK の実際の入力欄、Linux IBus GUI / Mozc、Windows 実機は NOT_RUN。
Push・PR コメント・レビュー投稿・再レビュー依頼・マージ・常用環境へのインストールは未実施。
担当は Codex（既存 ChatGPT 契約）、修正と検証は 1 担当。読み取り専用レビューは交代で実施し、同時編集は行っていない。
追加支払いなし。消費量は unknown。

## 初回PR作成時点の記録（履歴）

以下は初回 PR 作成時点の結果で、上の再検証結果とは区別する。

- 比較基準: `yksr-melt/Meltype` v1.0.0 (`467255bfe3e36b803a3fd3f5a1480fe35d5058c9`)
- 修正ブランチ: `t3code/meltype-ime-improvements-mt001-mt005`
- 前段実測: Linux native 53件で 39一致 / 14不一致。これは製品全体の精度ではない。

## MT-001 メンションの Space 確定で半角 @ が全角 ＠ になる (P1)

**問題**: 日本語入力で `@kuraido` を打ち、Space で確定すると `＠kuraido `（U+FF20）になる。
Enter/Tab/フォーカス確定では半角が保たれる。`@google` / `@user_name` / `@foo-bar` でも再現。

**原因**: 確定経路が原文の `@`（U+0040）を、変換候補・幅変換・誤字補正の対象として扱っていた。

**修正**: メンションを「自動変換してはいけない保護区間」として扱い、Space/Enter/Tab/フォーカス確定の
すべてで原文のまま確定する。Space は IME が半角空白を 1 つ足し、アプリへは Space を送らない。
Enter は改行を足さず IME が消費。Tab は原文確定後にアプリへ 1 回だけ通す。

## MT-002 URL・メール・ファイルパスの一部が日本語化する (P1)

**問題**:
| 入力 | 実測 | 期待 |
|---|---|---|
| `https://example.com?q=konnichiwa` | `https://example.com?q＝こんにちわ` | 原文のまま |
| `https://example.com/nihongo` | `https://example.com/日本語` | 原文のまま |
| `taro.yamada@example.com` | `たろ。山田@example.com` | 原文のまま |
| `./src/main.ts` | `。/src/マイン。ts` | 原文のまま |
| `/home/taro/project` | `/褒め/たろ/project` | 原文のまま |
| `C:\Users\taro\project` | `C:￥Users￥たろ￥project` | 原文のまま |

**原因**: URL・メール・パスを構造として保護せず、ローマ字変換・記号全角化・誤字補正に通していた。

**修正**: `ProtectedSpanScanner` を追加し、構造の強い開始条件（`scheme://`、`@`、`./ ../ ~/ /`、
`C:\`、UNC）で保護区間を求める（ネットワーク検証はしない）。原文全体が保護区間のとき、
表示・候補・Space/Enter/Tab/focus 確定・確定後補正・学習の全経路で原文を保持する。
`a/b`・`1/2`・`///`・単独の記号は保護しない。保護範囲内の Backspace は原文 1 文字ずつ削除。
F9/F10 などの明示指定は保護より優先する。

## MT-003 未確定入力に続く非BMP文字が別の文字になる (P1)

**問題**: 未確定英字の後に絵文字/補助漢字をキーで渡すと `hello😊 → hello⾊`（U+F60A）のように化ける。

**原因**: `meltype_handle_key` が `int ch` を `(char)ch` にキャストするため、非BMPが UTF-16 1 コード
ユニットへ切り詰められていた。

**修正**: C ABI のシンボル・引数レイアウトは変えず、`0x10000..0x10FFFF` の有効なスカラーは char へ
変換しない。未確定内容を確定したうえで `Consumed=false` を返し、元の OS イベントを 1 回だけ
アプリへ通す（二重挿入しない）。不正値（負数・単独サロゲート・上限超過）は不正な文字列を作らず、
状態も変えずに素通しする。Mac 側も UTF-16 要素数ではなく Unicode スカラー数で扱い、複数スカラーは
確定＋素通し。UTF-8 v2 API の全面導入は行っていない。

## MT-004 Linux の virtual_key がトルコ語 İ で例外になる (P2)

**問題**: `İ.lower()` が `i` + 結合ドットの 2 コードポイントになり、`ord()` が
`TypeError: ord() expected a character, but string of length 2 found` を出す。

**修正**: `virtual_key` の英字仮想キー写像を ASCII の A-Z/a-z だけに限定し、その他の有効な
スカラーは元のコードポイントのまま他文字キーとして返す。空・複数スカラーは `ord()` の前に弾く。

## MT-005 長い未確定入力でキー処理が遅くなる (P2)

**問題**: 未確定 256 ローマ字キーで 1 キー p95 約 45–52ms、入力＋確定 約 3.9–4.4s（前段・Linux/Mozc）。

**修正（今回）**: 区間分けで `units[i..j]` の連結文字列を毎回 LINQ で作っていた箇所を、
`Segment` 呼び出しごとの prefix offset に置き換えた（内容・順序は同一、出力不変）。
同一マシン・同一エンジン（スタブ）での 256 キー比較（修正版/基準版）:
`long_url` 0.21 倍、`natural_mixed` 0.84 倍、`mixed_repeated` 0.88 倍。

**未完了**: 設計の性能ゲート（256 キーで基準の 50% 以下）は未達。支配的なホットスポットは
`CompositionDetector.FindSpans` の二重ループで、正しさを保った差分更新が必要。
MT-005 は未完了として報告する。

## 検証結果（要約）

- 公式 Core テスト: 173/173（基準版）→ **188/188**（修正版、exit 0）。
- Linux ラッパー `virtual_key` 単体: 10/12（基準版）→ **12/12**。
- native C ABI（macOS arm64 AOT, 変換はスタブ）baseline 53件: 38→**47 PASS**、残2件は
  実 Mozc/漢字変換が必要（この環境では NOT_RUN）。
- 追加ケース 120件: 61 PASS / 1 FAIL（BMP の結合アクセント）/ 58 NOT_RUN。
- macOS 実機 IMK、Linux IBus GUI、Linux x86_64+Mozc、Windows は **NOT_RUN**。

## 既存契約との衝突（要点）

- 既存テストの `taro@gmail.com → たろ@gmail.com` は MT-002 と矛盾するため
  `taro@gmail.com` に更新した（黙って書き換えず、理由を明記）。
- 保護範囲の適用は現状「原文全体が 1 つの保護区間」のときのみ。通常日本語との混在文の部分保護は
  未実装（通常の日本語の変換を壊さないことを優先）。
- 保護判定はローマ字入力のときだけ（かな入力の `@` は濁点キーで誤保護するため）。

## 制約の順守

公開 Issue/PR、タグ/リリース、権限変更、常用環境へのインストールは未実施。追加課金なし。
外部AI（OpenCode/Gemini/Antigravity/Jev）は未使用。`reset --hard`・`clean -fd`・force push 不使用。
