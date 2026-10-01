Meltype for Mac テスト版

※ テスト版です。内容は公開しないでください。
※ 試作のため、動かないところがあります。気づいたことはなんでも教えてください。

■ 動く環境
  macOS 13 以降、Apple シリコン (M1 / M2 / M3 / M4 …) の Mac

■ インストール
  1. この zip を展開する (ダウンロードフォルダーに Meltype-mac フォルダーができます)
  2. 「ターミナル」を開いて、次の 1 行を貼り付けて Enter
       bash ~/Downloads/Meltype-mac/install.sh
  3. 初めてのときは、いったんログアウトしてログインし直す
  4. システム設定 → キーボード → 入力ソース →「編集…」→「+」→ 日本語 → Meltype を追加
  5. メニューバーの入力メニューで Meltype を選ぶ

■ 使い方
  ・ふつうにローマ字で打つと日本語、英単語 (google、github …) は英字のまま
  ・Space で変換、Enter で確定、← → で文節を選ぶ、Esc で取り消し
  ・F6 ひらがな / F7 カタカナ / F9 全角英数 / F10 半角英数
  ・JIS キーボードの「英数」キーで英数、「かな」キーで日本語

■ 不具合を報告するとき
  ・どのアプリで、何と打って、どうなったか (できればスクリーンショットも)
  ・動きがおかしいときのログ: ターミナルで次を実行してから操作すると、ログが流れます
       log stream --predicate 'process == "Meltype"' --level debug

■ 止まってしまったら
  ターミナルで  pkill -x Meltype  (次にキーを打つと自動で起動し直します)

■ アンインストール
  入力ソースから Meltype を外してから、ターミナルで
       rm -rf ~/Library/Input\ Methods/Meltype.app
  設定と学習データも消すなら
       rm -rf ~/Library/Application\ Support/Meltype
