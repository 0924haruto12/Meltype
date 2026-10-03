// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// summary: chjht / jht の結果 (この bot が送った埋め込み) を読み直して、特に問題となるものをまとめる。
// 同じ文の結果が何度も出ていても 1 つにする (いちばん新しい結果を使う)。
// 文で送るのは概要と上位のものだけ (1 メッセージに収まる分)。全体は JSON のファイルにして添える。
import fs from 'node:fs';
import path from 'node:path';
import { shorten } from './commands.mjs';

/** 1 メッセージの長さの上限 (Discord は 2000 文字。少し余裕を持たせる)。 */
export const MessageLimit = 1900;
/** 文で見せる、種類ごとの件数の上限 (残りは JSON)。 */
export const SectionLimit = 8;

const WidthOnly = /^記号の全角 \/ 半角だけが違う/;
const SplitNote = /^日本語 \/ 英語の分かれ方が違う \(英字: (.*)、出てほしいのは (.*)\)$/;

/** 問題の種類 (集計用)。 */
export const ProblemKinds = [
  ['split', '日本語 / 英語の分かれ方が違う', /^日本語 \/ 英語の分かれ方が違う/],
  ['missing', '候補に無い', /が候補に無い/],
  ['order', '候補にはあるが最初に出ない', /は候補の \d+ 番目/],
  ['clauses', '文節の数が合わない', /文節が(多い|足りない)/],
  ['committed', '空白で確定した部分が違う', /^空白までで確定した部分/],
  ['width', '記号の全角 / 半角だけ', WidthOnly],
];

/** 英単語の一覧 (Meltype の辞書 english-words.txt・english.txt)。読めなければ空。 */
export function loadEnglishWords(dictionaryDir) {
  const words = new Set();
  for (const name of ['english-words.txt', 'english.txt']) {
    let text = '';
    try { text = fs.readFileSync(path.join(dictionaryDir, name), 'utf8'); } catch { continue; }
    for (const line of text.split('\n')) {
      if (line.startsWith('#')) continue;
      for (const word of line.trim().toLowerCase().split(/\s+/)) if (word) words.add(word);
    }
  }
  return words;
}

/** ローマ字としても読めるか (tomato = と ま と、made = ま で)。 */
export const isRomaji = word =>
  /^(?:[aiueo]|nn|n(?![aiueoy])|(?:[kgsztdnhbpmrfv]y|sh|ch|ts|j|[kgsztdnhbpmyrwfv])[aiueo]|([kgsztdhbpmrfjcv])(?=\1))+$/.test(word);

/**
 * 日本語 / 英語の分かれ方の違いが、英単語でもありローマ字としても読める語 (tomato、made、game) だけか。
 * こうした語は、英語にするか日本語にするかを打った文字だけでは決められないので、まとめの一覧からは除く。
 */
export function isAmbiguousSplit(entry, englishWords) {
  const notes = entry.problems.map(p => p.match(SplitNote)).filter(Boolean);
  if (notes.length === 0) return false;
  return notes.every(([, got, want]) => {
    const words = text => (text === 'なし' ? [] : text.split(' ').filter(Boolean));
    const remaining = [...words(want)];
    const extra = [];
    for (const w of words(got)) {
      const i = remaining.indexOf(w);
      if (i >= 0) remaining.splice(i, 1);
      else extra.push(w);
    }
    const differences = [...extra, ...remaining];
    return differences.length > 0 && differences.every(w => englishWords.has(w) && isRomaji(w));
  });
}

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
  // JSON に入れる中身 (打ち方ごとの結果と、問題ごとの起きた打ち方)
  const results = [];
  const details = [];
  const header = description.match(/^読み: (.*)　変換エンジン: (.*)$/m);
  let inProblems = false;
  for (const line of description.split('\n')) {
    const mark = line.match(/^(✅|🟡|🟠|❌) `/)?.[1];
    if (mark && !inProblems) {
      marks[mark]++;
      const parts = line.match(/^\S+ `([^`]*)`(?: → Space: (.*)　Enter: (.*))?$/);
      if (parts) results.push({ mark, keys: parts[1], space: parts[2] ?? 'OK', enter: parts[3] ?? 'OK' });
    }
    if (/^\*\*問題 \(\d+\)\*\*/.test(line)) inProblems = true;
    else if (inProblems && line.startsWith('・')) {
      problems.push(line.slice(1));
      details.push({ problem: line.slice(1), keys: [] });
    } else if (inProblems && line.startsWith('　└ ') && details.length > 0) {
      const where = line.slice(3);
      details.at(-1).keys = where === 'すべての打ち方' ? ['すべての打ち方'] : [...where.matchAll(/`([^`]*)`/g)].map(m => m[1]);
    }
  }
  return {
    expected, ok: Number(count[1]), total: Number(count[2]), marks, problems, url: embed.url || fallbackUrl,
    reading: header?.[1] ?? '', engine: header?.[2] ?? '', results, details,
    listShortened: description.includes('…(打ち方の一覧は省略)'),
  };
}

/**
 * 結果 1 つの重さ: split (分かれ方が違う) > none (どの打ち方でも出ない) > some (出ない打ち方がある) > width (全角 / 半角だけ) > ok。
 * 分かれ方の違いが決められない語 (tomato) だけなら ambiguous (除外)。
 */
export function severity(entry, englishWords = new Set()) {
  if (entry.ok === entry.total) return 'ok';
  if (entry.marks['❌'] > 0 || entry.problems.some(p => ProblemKinds[0][2].test(p))) return isAmbiguousSplit(entry, englishWords) ? 'ambiguous' : 'split';
  if (entry.problems.length > 0 && entry.problems.every(p => WidthOnly.test(p))) return 'width';
  return entry.ok === 0 ? 'none' : 'some';
}

/**
 * 結果をまとめる。entries は { ...parseJhtEmbed の結果, at (送られた時刻) }。
 * 同じ文 (expected) は、いちばん新しいものだけを使う。
 */
export function summarize(entries, { englishWords = new Set() } = {}) {
  const latest = new Map();
  for (const entry of entries) {
    const old = latest.get(entry.expected);
    if (!old || (entry.at ?? 0) >= (old.at ?? 0)) latest.set(entry.expected, entry);
  }
  const unique = [...latest.values()];
  const groups = { split: [], none: [], some: [], width: [], ambiguous: [], ok: [] };
  for (const entry of unique) groups[severity(entry, englishWords)].push(entry);
  // 失敗した打ち方の割合が大きいものから
  const failRate = e => (e.total - e.ok) / Math.max(1, e.total);
  for (const list of Object.values(groups)) list.sort((a, b) => failRate(b) - failRate(a) || (b.marks['❌'] - a.marks['❌']));
  // 問題の種類ごとの件数 (1 つの文で同じ種類が何度出ても 1 件。除外したものは数えない)
  const kinds = Object.fromEntries(ProblemKinds.map(([key]) => [key, 0]));
  for (const entry of unique.filter(e => !groups.ambiguous.includes(e))) {
    for (const [key, , pattern] of ProblemKinds) if (entry.problems.some(p => pattern.test(p))) kinds[key]++;
  }
  return { total: entries.length, duplicates: entries.length - unique.length, unique: unique.length, groups, kinds };
}

/** 一覧の 1 行。いちばん大事な問題 (全角 / 半角だけのものは後回し) を 1 つ添える。 */
function line(entry, mark) {
  const note = entry.problems.find(p => !WidthOnly.test(p)) ?? entry.problems[0] ?? '';
  const more = entry.problems.length > 1 ? ` ほか ${entry.problems.length - 1}` : '';
  const link = entry.url ? ` [→](<${entry.url}>)` : '';
  return `${mark} 「${shorten(entry.expected, 40)}」 ${entry.ok}/${entry.total}${link}` + (note ? `\n　└ ${shorten(note, 100)}${more}` : '');
}

/**
 * JSON のファイルにする内容 (すべての結果。問題の無いものは文だけ)。
 * Discord へのリンクではなく、結果そのもの (読み・打ち方ごとの Space / Enter の結果・問題と起きた打ち方) を入れる。
 */
export function summaryJson(summary, channelIds) {
  const { groups } = summary;
  const item = e => ({
    expected: e.expected,
    reading: e.reading,
    engine: e.engine,
    firstOk: `${e.ok} / ${e.total}`,
    results: e.results ?? [],
    ...(e.listShortened ? { resultsShortened: true } : {}),
    problems: e.details?.length ? e.details : e.problems.map(problem => ({ problem, keys: [] })),
  });
  return {
    channels: channelIds,
    total: summary.total,
    unique: summary.unique,
    duplicates: summary.duplicates,
    counts: Object.fromEntries(Object.entries(groups).map(([key, list]) => [key, list.length])),
    kinds: summary.kinds,
    split: groups.split.map(item),
    none: groups.none.map(item),
    some: groups.some.map(item),
    width: groups.width.map(item),
    ambiguous: groups.ambiguous.map(item),
    ok: groups.ok.map(e => e.expected),
  };
}

/**
 * まとめを、1 メッセージ (2000 文字以下) の文にする。概要と、特に問題となるものを入るだけ。
 * 入りきらない分は件数だけ書き、全体は JSON のファイルで見てもらう。
 */
export function formatSummary(summary, channelIds, fileName = 'summary.json') {
  const { groups, kinds } = summary;
  const problems = summary.unique - groups.ok.length - groups.ambiguous.length;
  const head = [
    `📋 **jht の結果のまとめ** (${channelIds.map(id => `<#${id}>`).join(' ')})`,
    `結果 ${summary.total} 件 → 重複を除いて **${summary.unique} 件** (重複 ${summary.duplicates} 件は無視。同じ文は新しい結果を使用)`,
    `問題あり ${problems} 件: ❌ 分かれ方が違う **${groups.split.length}**・🔴 どの打ち方でも出ない **${groups.none.length}**・🟠 出ない打ち方がある ${groups.some.length}・記号の全角 / 半角だけ ${groups.width.length}`,
    `除外 ${groups.ambiguous.length} 件 (英単語でもローマ字でも読める語 (tomato など) だけの違いで、どちらにすべきか決められないもの)`,
    `問題の種類: ${ProblemKinds.filter(([key]) => kinds[key] > 0).map(([key, label]) => `${label} ${kinds[key]}`).join('・') || 'なし'}`,
  ];
  const footer = `\n📎 全体 (すべての文の、打ち方ごとの変換結果と問題) は ${fileName} にあります。`;
  let text = head.join('\n');
  let shown = 0;
  const listed = groups.split.length + groups.none.length;
  // 特に問題となるもの: 日本語 / 英語の分かれ方 (Meltype の判定) と、どの打ち方でも出ないもの
  outer: for (const [title, list, mark] of [['❌ 日本語 / 英語の分かれ方が違う', groups.split, '❌'], ['🔴 どの打ち方でも最初の変換で出ない', groups.none, '🔴']]) {
    if (list.length === 0) continue;
    const heading = `\n\n**${title} (${list.length})**`;
    // 見出しと 1 件も入らないなら、ここまで
    if (text.length + heading.length + line(list[0], mark).length + 1 + footer.length + 40 > MessageLimit) break;
    text += heading;
    // 1 つの種類で埋まらないよう、種類ごとに上位 SectionLimit 件まで
    for (const entry of list.slice(0, SectionLimit)) {
      const next = '\n' + line(entry, mark);
      if (text.length + next.length + footer.length + 40 > MessageLimit) break outer;
      text += next;
      shown++;
    }
  }
  if (shown < listed) text += `\n…ほか ${listed - shown} 件 (文に入りきらない分)`;
  if (problems === 0) text += '\n\n問題のある結果はありませんでした。';
  return text + footer;
}
