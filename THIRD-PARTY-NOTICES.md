# サードパーティー通知

AutoIME のソースコード (このリポジトリ) には、他者の著作物は含まれていません。

## 配布用パッケージに同梱しているもの

| 部品 | ライセンス | 同梱先 |
| --- | --- | --- |
| .NET ランタイム (Microsoft.NETCore.App, Microsoft.WindowsDesktop.App) | MIT License | `app\dotnet\` (ライセンス: `app\dotnet\LICENSE.txt`、同梱部品の通知: `app\dotnet\ThirdPartyNotices.txt`) |

## 実行時に使う Windows の機能 (同梱しない)

次は Windows に標準で入っているものを実行時に呼び出すだけで、AutoIME には含まれません。

- Microsoft IME の変換エンジン (IFELanguage)
- Windows の変換候補 API (Windows.Data.Text.TextConversionGenerator)
- Windows のスペルチェッカー (ISpellChecker)
- UI Automation、IMM32、Text Services Framework

## 辞書

`dictionaries/` の辞書 (日本語・英語の単語、固有名詞、同音異義語の候補、文脈の手がかり) は AutoIME のために作成したもので、
AutoIME 本体と同じライセンス (GPL-3.0-or-later、商用ライセンス) です。
固有名詞の辞書に含まれる製品名・会社名は各社の商標です。
