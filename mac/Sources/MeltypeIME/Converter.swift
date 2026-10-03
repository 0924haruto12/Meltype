// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro

import Foundation
import KanaKanjiConverterModuleWithDefaultDictionary

/// azooKey の変換エンジン (AzooKeyKanaKanjiConverter) で、ひらがなを漢字かな混じりに変換する。
/// azooKey の API が変わったときは、このファイルだけ直せばよいようにしてある。
final class MeltypeConverter {
    static let shared = MeltypeConverter()

    // 同梱の辞書 (KanaKanjiConverterModuleWithDefaultDictionary) を使う変換エンジン
    private let converter = KanaKanjiConverter.withDefaultDictionary()
    private let options: ConvertRequestOptions

    private init() {
        // azooKey の学習データ・ユーザー辞書の置き場所 (Meltype では学習しない設定にしているので、ほぼ使わない)。
        let directory = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("Meltype/azooKey", isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        options = ConvertRequestOptions(
            requireJapanesePrediction: .disabled,
            requireEnglishPrediction: .disabled,
            keyboardLanguage: .ja_JP,
            learningType: .nothing,
            memoryDirectoryURL: directory,
            sharedContainerURL: directory,
            textReplacer: .withDefaultEmojiDictionary(),
            specialCandidateProviders: KanaKanjiConverter.defaultSpecialCandidateProviders,
            metadata: .init(versionString: "Meltype 0.2.0")
        )
    }

    /// ひらがな全体に対する変換結果 (入力をすべて使ったものだけ、よい順)。
    private func results(for hiragana: String) -> [Candidate] {
        var composing = ComposingText()
        composing.insertAtCursorPosition(hiragana, inputStyle: .direct)
        let results = converter.requestCandidates(composing, options: options)
        converter.stopComposition()
        let count = hiragana.count
        // 入力 (ひらがな) をすべて使った候補だけ (.direct で入れたので、入力の文字数 = ひらがなの文字数)
        return results.mainResults.filter { $0.composingCount == .inputCount(count) || $0.composingCount == .surfaceCount(count) }
    }

    /// 文節に区切った変換結果 (読みをつなげると元のひらがなになる)。変換できなければ空。
    func clauses(for hiragana: String, context: String?) -> [(reading: String, text: String)] {
        guard let best = results(for: hiragana).first else { return [] }
        var clauses: [(reading: String, text: String)] = []
        for element in best.data {
            let reading = element.ruby.applyingTransform(.hiraganaToKatakana, reverse: true) ?? element.ruby
            // 助詞・助動詞 (ひらがなだけの短い語) は前の文節にくっつける (今日|は → 今日は)。
            if let last = clauses.last, element.word == reading, element.word.count <= 3 {
                clauses[clauses.count - 1] = (last.reading + reading, last.text + element.word)
            } else {
                clauses.append((reading, element.word))
            }
        }
        // 読みがずれた (長音などの扱いの違い) ときは、文節に分けずに全体で返す。
        if clauses.map(\.reading).joined() != hiragana {
            return [(hiragana, best.text)]
        }
        return clauses
    }

    /// 読みに対する候補の一覧 (重複なし、30 個まで)。
    func candidates(for reading: String) -> [String] {
        var seen = Set<String>()
        var list: [String] = []
        for candidate in results(for: reading) where seen.insert(candidate.text).inserted {
            list.append(candidate.text)
            if list.count >= 30 { break }
        }
        return list
    }
}
