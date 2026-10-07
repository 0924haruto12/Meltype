// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 0924haruto12

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meltype.Config;

// NativeAOT でも、設定の enum・プロファイル等の JSON メタデータを静的に生成する。
// JSON の形式と、コメント・末尾カンマの受け入れは既存の設定ファイルと同じ。
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(Settings))]
internal partial class SettingsJsonContext : JsonSerializerContext;
