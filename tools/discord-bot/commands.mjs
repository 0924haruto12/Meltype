// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// コマンドの読み取り (Discord に依存しない部分。テストしやすいように分けてある)。
// 「@Meltype ht kyouha」のように、bot へのメンションで始まるメッセージをコマンドとして読む。

export const Commands = {
  help: { aliases: ['help', 'h', 'ヘルプ'] },
  henkan: { aliases: ['henkan-test', 'ht'] },
  add: { aliases: ['add-list', 'al'] },
  list: { aliases: ['list', 'ls'] },
  close: { aliases: ['close-list', 'cl'] },
  submit: { aliases: ['submit-list', 'sl'] },
};

/**
 * メッセージがこの bot へのコマンドなら { command, args, rest } を返す。違えば null。
 * botId はこの bot のユーザー ID。メンションは <@id> と <@!id> のどちらでもよい。
 */
export function parseCommand(content, botId) {
  const match = (content ?? '').match(new RegExp(`^\\s*<@!?${botId}>\\s*(\\S+)?\\s*([\\s\\S]*)$`));
  if (!match) return null;
  const name = (match[1] ?? 'help').toLowerCase();
  const command = Object.entries(Commands).find(([, c]) => c.aliases.includes(name))?.[0] ?? 'unknown';
  return { command, name, rest: match[2].trim() };
}

/** 番号の引数 (12、#12)。読めなければ null。 */
export function parseNumber(text) {
  const match = (text ?? '').trim().match(/^#?(\d+)$/);
  return match ? Number(match[1]) : null;
}

/** 長い文を切る (Discord の表示用)。 */
export function shorten(text, max) {
  const flat = (text ?? '').replace(/\s+/g, ' ').trim();
  return [...flat].length > max ? [...flat].slice(0, max - 1).join('') + '…' : flat;
}

export const HelpText = [
  '**Meltype bot の使い方** (先頭に bot へのメンションを付けて送ってください)',
  '',
  '`@Meltype help` … この説明',
  '`@Meltype henkan-test <打つキー>` (`ht`) … Meltype でどう変換されるかを試す (例: `@Meltype ht kyouhagoogledekensaku`)',
  '`@Meltype list [誤変換|バグ|提案|解決済み|クローズ|すべて]` … リストの一覧 (省略すると未解決のもの)',
  '',
  '誤変換・バグ報告・機能提案のチャンネルで:',
  '`@Meltype add-list [内容]` (`al`) … リストに追加 (メッセージに返信して送ると、そのメッセージを追加)',
  '`@Meltype close-list <番号>` (`cl`) … クローズ (対応しない・重複など)',
  '`@Meltype submit-list <番号>` (`sl`) … 解決してクローズ',
  '',
  '番号は 誤変換・バグ・提案 で共通です (#1, #2, …)。',
].join('\n');
