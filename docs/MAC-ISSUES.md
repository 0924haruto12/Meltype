<a name="mac-版の-issue-の修正"></a>
<img src="images/headings/mac-issues/title.svg" alt="Mac 版の Issue の修正" height="80">


`mac` のラベルが付いた未解決の Issue を、2026-10-07 の時点で調べて直した記録です。
修正は手元のもので、元の Issue を閉じたりコメントしたりはしていません。

<br>

<a name="直したもの"></a>
<img src="images/headings/mac-issues/01.svg" alt="直したもの" height="53"><br>


| Issue | 変えたこと | 確かめたこと |
| --- | --- | --- |
| [#114](https://github.com/yksr-melt/Meltype/issues/114) | macOS のスペルチェッカーの結果 (正しい・正しくない の両方) を、4,096 語まで 5 分間覚えておく。変換エンジンの途中の状態はそのまま残す | 報告された長い入力で、2 回とも同じ結果になった。スペルチェッカーを呼んだ回数は 1 回目 237 回、2 回目 0 回 |
| [#101](https://github.com/yksr-melt/Meltype/issues/101) | シェルで配列に足していた入力ソースの登録を、macOS の仕組みでの登録に変える。Meltype の親と入力モードの記録を整理し、ほかの入力ソースは残して、前の一覧を控えておく | 15 個重なった状態から、親 1 つ・入力モード 1 つになった。何度実行しても同じ結果になる。実機の登録もそれぞれ 1 つずつ |
| [#77](https://github.com/yksr-melt/Meltype/issues/77) | 日本語の活用の前の英語の動詞を、ローマ字の区切りをまたいでいても英語として見分ける | `reflectsareta` が `reflectされた` になる。活用と、変わらない英単語は本体のテストで確かめた |
| [#118](https://github.com/yksr-melt/Meltype/issues/118) | ソースの版を 1.0.3 にし、ネイティブのビルドとアプリの plist で同じ版を使う。変換エンジンの版の情報は plist から読む | インストールした plist と本体のビルドの設定が、どちらも 1.0.3。リリースのタグでのビルドでは、どちらもタグの版になる |
| [#40](https://github.com/yksr-melt/Meltype/issues/40) | 書き出した macOS の plist の辞書を、手元のユーザー辞書に取り込んで入力に反映する操作を、入力メニューに足す | XML とバイナリのどちらでも、今ある語を残し、カタカナの読みを整え、読み込み直しても重ならない |

<br>

<a name="確かめた結果"></a>
<img src="images/headings/mac-issues/02.svg" alt="確かめた結果" height="53"><br>


- 本体のテスト: 198/198 通過
- インストールしたアプリの `--self-test`: 通過
- アプリの署名の確認とシェルの文法の確認: 通過

<br>

<a name="確かめられていないこと"></a>
<img src="images/headings/mac-issues/03.svg" alt="確かめられていないこと" height="53"><br>


- 報告された OS の版で、システム設定に 15 個並んでいた画面そのものは再現できていません
- テストで確かめたのは plist の読み込みと取り込みまでで、ファイルを選ぶ画面や、実際に書き出した辞書では試していません
- スペルチェッカーの結果を 5 分覚えておくので、macOS で綴りの登録を変えても、反映まで最大 5 分かかります
- 品質テストの外れた例が 2 つ (`my name is taro` と `apinoerror`) ありますが、もとからあるもので、今回の修正で増えたものではありません
