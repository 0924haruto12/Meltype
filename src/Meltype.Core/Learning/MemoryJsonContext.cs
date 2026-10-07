// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 0924haruto12

using System.Text.Json;
using System.Text.Json.Serialization;
using Meltype.Composition;

namespace Meltype.Learning;

// NativeAOT でも保存した学習データを再起動後に読み戻せるよう、辞書と値の型を生成する。
// 既存ファイルのキー・プロパティ名を保ち、model.json だけ従来どおりインデントする。
[JsonSerializable(typeof(Dictionary<string, LanguageMemory.Entry>), TypeInfoPropertyName = "Languages")]
[JsonSerializable(typeof(Dictionary<string, ConversionHistory.Entry>), TypeInfoPropertyName = "Conversions")]
[JsonSerializable(typeof(LanguageMemory.Entry), TypeInfoPropertyName = "LanguageEntry")]
[JsonSerializable(typeof(ConversionHistory.Entry), TypeInfoPropertyName = "ConversionEntry")]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, int>>), TypeInfoPropertyName = "Translations")]
[JsonSerializable(typeof(UserModel.ModelFile), TypeInfoPropertyName = "Model")]
internal partial class MemoryJsonContext : JsonSerializerContext
{
    internal static MemoryJsonContext Indented { get; } = new(new JsonSerializerOptions { WriteIndented = true });
}
