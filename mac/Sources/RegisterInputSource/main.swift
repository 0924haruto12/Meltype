// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// Meltype.app を「有効な入力ソース」として macOS に登録し直す、小さな道具 (#134)。
// install.sh / build.sh が Meltype.app を入れ替えたあとに呼ぶ。
//   RegisterInputSource [Meltype.app のパス]   (省略したときは、この道具が入っている Meltype.app)
//
// 更新のたびに入力ソースから消える問題の対策。~/Library/Input Methods/Meltype.app を入れ替えるとき、
// バンドルが無い一瞬に入力ソースの走査が走ると、macOS は登録を消す。消えたあとは、バンドルを戻して
// 走査し直しても戻らないことがあり、これまではログアウトするしかなかった。
// TISRegisterInputSource なら、実行中でもその場で登録を戻せる (ログアウトは要らない)。
//
// IME の本体とは別の実行ファイルにしてある。本体を引数で呼び分ける形にすると、引数を知らない古い
// 本体に当てたときに IME として起動したままになり、install.sh がそこで止まってしまう。
import Carbon
import Foundation

let appPath = CommandLine.arguments.count >= 2 ? CommandLine.arguments[1] : Bundle.main.bundlePath

guard FileManager.default.fileExists(atPath: appPath) else {
    FileHandle.standardError.write(Data("Meltype.app が見つかりません: \(appPath)\n".utf8))
    exit(1)
}

// 何度呼んでも増えない (すでに入っていれば、そのまま)。
let status = TISRegisterInputSource(URL(fileURLWithPath: appPath) as CFURL)
guard status == noErr else {
    FileHandle.standardError.write(Data("入力ソースに登録できませんでした (OSStatus \(status)): \(appPath)\n".utf8))
    exit(1)
}

print("入力ソースに Meltype を登録しました: \(appPath)")
