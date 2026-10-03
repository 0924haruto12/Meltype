// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// summary: chjht / jht の結果 (この bot が送った埋め込み) を読み直して、特に問題となるものをまとめる。
// 同じ文の結果が何度も出ていても 1 つにする (いちばん新しい結果を使う)。
// Discord の 1 メッセージ 2000 文字の上限に合わせて、行の切れ目で分けて送る。
import { shorten } from './commands.mjs';

/** 1 メッセージの長さの上限 (Discord は 2000 文字。少し余裕を持たせる)。 */
export const MessageLimit = 1900;
/** 一覧に出す件数の上限 (それ以上は件数だけ)。 */
export const ListLimit = 150;

const WidthOnly = /^記号の全角 \/ 半角だけが違う/;

/** 問題の種類 (集計用)。 */
export const ProblemKinds = [
  ['split', '日本語 / 英語の分かれ方が違う', /^日本語 \/ 英語の分かれ方が違う/],
  ['missing', '候補に無い', /が候補に無い/],
  ['order', '候補にはあるが最初に出ない', /は候補の \d+ 番目/],
  ['clauses', '文節の数が合わない', /文節が(多い|足りない)/],
  ['committed', '空白で確定した部分が違う', /^空白までで確定した部分/],
  ['width', '記号の全角 / 半角だけ', WidthOnly],
];

/**
 * jht の結果の埋め込みを読む。jht の結果でなければ null。
 * 戻り値: { expected, ok, total, marks: { '✅': n, … }, problems: [文], url }
 */
export function parseJhtEmbed(embed, fallbackUrl) {
  const description = embed?.description ?? '';
  const count = description.match(/最初の変換で出た: (\d+) \/ (\d+) 通り/);
  if (!count) return null;
  const expected = (embed.title ?? '').replace(/^「/, '').replace(/」(の変換テスト)?$/, '');
  const marks = { '✅': 0, '🟡': 0, '🟠': 0, '❌': 0 };
  const problems = [];
  let inProblems = false;
  for (const line of description.split('\n')) {
    const mark = line.match(/^(✅|🟡|🟠|❌) `/)?.[1];
    if (mark && !inProblems) marks[mark]++;
    if (/^\*\*問題 \(\d+\)\*\*/.test(line)) inProblems = true;
    else if (inProblems && line.startsWith('・')) problems.push(line.slice(1));
  }
  return { expected, ok: Number(count[1]), total: Number(count[2]), marks, problems, url: embed.url || fallbackUrl };
}

/** 結果 1 つの重さ: split (分かれ方が違う) > none (どの打ち方でも出ない) > some (出ない打ち方がある) > width (全角 / 半角だけ) > ok。 */
export function severity(entry) {
  if (entry.ok === entry.total) return 'ok';
  if (entry.marks['❌'] > 0 || entry.problems.some(p => ProblemKinds[0][2].test(p))) return 'split';
  if (entry.problems.length > 0 && entry.problems.every(p => WidthOnly.test(p))) return 'width';
  return entry.ok === 0 ? 'none' : 'some';
}

/**
 * 結果をまとめる。entries は { ...parseJhtEmbed の結果, at (送られた時刻) }。
 * 同じ文 (expected) は、いちばん新しいものだけを使う。
 */
export function summarize(entries) {
  const latest = new Map();
  for (const entry of entries) {
    const old = latest.get(entry.expected);
    if (!old || (entry.at ?? 0) >= (old.at ?? 0)) latest.set(entry.expected, entry);
  }
  const unique = [...latest.values()];
  const groups = { split: [], none: [], some: [], width: [], ok: [] };
  for (const entry of unique) groups[severity(entry)].push(entry);
  // 失敗した打ち方の割合が大きいものから
  const failRate = e => (e.total - e.ok) / Math.max(1, e.total);
  for (const list of Object.values(groups)) list.sort((a, b) => failRate(b) - failRate(a) || (b.marks['❌'] - a.marks['❌']));
  // 問題の種類ごとの件数 (1 つの文で同じ種類が何度出ても 1 件)
  const kinds = Object.fromEntries(ProblemKinds.map(([key]) => [key, 0]));
  for (const entry of unique) {
    for (const [key, , pattern] of ProblemKinds) if (entry.problems.some(p => pattern.test(p))) kinds[key]++;
  }
  return { total: entries.length, duplicates: entries.length - unique.length, unique: unique.length, groups, kinds };
}

/** 一覧の 1 行。いちばん大事な問題 (全角 / 半角だけのものは後回し) を 1 つ添える。 */
function line(entry, mark) {
  const note = entry.problems.find(p => !WidthOnly.test(p)) ?? entry.problems[0] ?? '';
  const more = entry.problems.length > 1 ? ` ほか ${entry.problems.length - 1}` : '';
  const link = entry.url ? ` [→](<${entry.url}>)` : '';
  return `${mark} 「${shorten(entry.expected, 40)}」 ${entry.ok}/${entry.total}${link}` + (note ? `\n　└ ${shorten(note, 110)}${more}` : '');
}

/** 長い文を、行の切れ目で上限以下のメッセージに分ける。1 行が長すぎるときはその行を切る。 */
export function chunkLines(lines, limit = MessageLimit) {
  const chunks = [];
  let current = '';
  for (const raw of lines) {
    const text = raw.length > limit ? raw.slice(0, limit - 1) + '…' : raw;
    if (current && current.length + 1 + text.length > limit) {
      chunks.push(current);
      current = '';
    }
    current = current ? `${current}\n${text}` : text;
  }
  if (current) chunks.push(current);
  return chunks;
}

/** まとめを、送るメッセージ (2000 文字以下) の並びにする。 */
export function formatSummary(summary, channelIds) {
  const { groups, kinds } = summary;
  const problems = summary.unique - groups.ok.length;
  const lines = [
    `📋 **jht の結果のまとめ** (${channelIds.map(id => `<#${id}>`).join(' ')})`,
    `結果 ${summary.total} 件 → 重複を除いて **${summary.unique} 件** (重複 ${summary.duplicates} 件は無視。同じ文は新しい結果を使用)`,
    `問題あり ${problems} 件: ❌ 分かれ方が違う **${groups.split.length}**・🔴 どの打ち方でも出ない **${groups.none.length}**・🟠 出ない打ち方がある ${groups.some.length}・記号の全角 / 半角だけ ${groups.width.length}`,
    '',
    '**問題の種類** (文の数)',
    ProblemKinds.filter(([key]) => kinds[key] > 0).map(([key, label]) => `${label}: ${kinds[key]}`).join('　') || 'なし',
  ];
  let listed = 0;
  const section = (title, list, mark) => {
    if (list.length === 0) return;
    lines.push('', `**${title} (${list.length})**`);
    for (const entry of list) {
      if (listed >= ListLimit) {
        lines.push(`…ほか ${list.length - list.indexOf(entry)} 件 (多いので省略)`);
        return;
      }
      lines.push(line(entry, mark));
      listed++;
    }
  };
  // 特に問題となるもの: 日本語 / 英語の分かれ方 (Meltype の判定) と、どの打ち方でも出ないもの
  section('❌ 日本語 / 英語の分かれ方が違う', groups.split, '❌');
  section('🔴 どの打ち方でも最初の変換で出ない', groups.none, '🔴');
  if (groups.some.length > 0) lines.push('', `🟠 一部の打ち方だけ出ないもの ${groups.some.length} 件と、記号の全角 / 半角だけのもの ${groups.width.length} 件は、件数だけ出しています。`);
  if (problems === 0) lines.push('', '問題のある結果はありませんでした。');
  return chunkLines(lines);
}
