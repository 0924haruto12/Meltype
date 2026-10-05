# Meltype MT-001〜MT-005 問題点と修正点のまとめ

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
