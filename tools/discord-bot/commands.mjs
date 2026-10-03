// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// コマンドの読み取り (Discord に依存しない部分。テストしやすいように分けてある)。
// 「@bot ht kyouha」のように、bot へのメンションで始まるメッセージをコマンドとして読む。

export const Commands = {
  help: { aliases: ['help', 'h', 'ヘルプ'] },
  henkan: { aliases: ['henkan-test', 'ht'] },
  jht: { aliases: ['japanese-henkan-test', 'jht'] },
  chjht: { aliases: ['chjht', 'channel-jht'] },
  chjhtStop: { aliases: ['chjht-stop'] },
  add: { aliases: ['add-list', 'al'] },
  list: { aliases: ['list', 'ls'] },
  close: { aliases: ['close-list', 'cl'] },
  submit: { aliases: ['submit-list', 'sl'] },
};

/**
 * メッセージがこの bot へのコマンドなら { command, name, rest } を返す。違えば null。
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

/** コード (`…`) として見せる。中の ` は似た文字にする。 */
export const code = text => '`' + String(text ?? '').replace(/`/g, 'ˋ') + '`';

/** 使い方。mention はこの bot のメンション (<@id>)。bot の名前はサーバーごとに違うので、実際のメンションで書く。 */
export function helpText(mention) {
  const m = mention;
  return [
    `**${m} の使い方** (先頭に ${m} を付けて送ってください)`,
    '',
    `${m} ${code('help')} … この説明`,
    `${m} ${code('henkan-test <打つキー>')} (${code('ht')}) … 打ったキーがどう変換されるか`,
    `　例: ${m} ${code('ht kyouhagoogledekensaku')}`,
    `${m} ${code('japanese-henkan-test <出てほしい文>')} (${code('jht')}) … その文を、考えられるローマ字の打ち方ですべて打ってみて、ちゃんと出るかを確かめる`,
    `　例: ${m} ${code('jht 私はgoogleが好きです')}　読みが違うときは ${code('jht 私はgoogleが好きです / わたしはgoogleがすきです')}`,
    `${m} ${code('chjht <#チャンネル か ID>')} … そのチャンネルのメッセージを最初からすべて jht して、問題があったものをここに送る (80 文字を超えるものは飛ばす。管理する人だけ。順番待ちあり、${code('chjht-stop')} で止める)`,
    `${m} ${code('list [誤変換|バグ|提案|解決済み|クローズ|すべて]')} … リストの一覧 (省略すると未解決のもの)`,
    '',
    '誤変換・バグ報告・機能提案のチャンネルで:',
    `${m} ${code('add-list [内容]')} (${code('al')}) … リストに追加 (メッセージに返信して送ると、そのメッセージを追加)`,
    `${m} ${code('close-list <番号>')} (${code('cl')}) … クローズ (対応しない・重複など)`,
    `${m} ${code('submit-list <番号>')} (${code('sl')}) … 解決してクローズ`,
    '',
    '番号は 誤変換・バグ・提案 で共通です (#1, #2, …)。',
  ].join('\n');
}
