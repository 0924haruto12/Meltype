// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// jht (japanese-henkan-test) の結果を Discord に出す文にする。
// 打ち方ごとの結果を 1 行ずつ並べ、見つかった問題は最後にまとめて出す (同じ問題は 1 つにして、どの打ち方で起きたかを添える)。
import { code } from './commands.mjs';

/** jht の結果 1 つの判定: ✅ 最初の変換で出る / 🟡 Enter (ライブ変換) なら出る / 🟠 分かれ方は合っているが変換が違う / ❌ 日本語・英語の分かれ方が違う */
export function jhtMark(result) {
  if (result.firstOk) return '✅';
  if (result.liveOk) return '🟡';
  if (result.splitOk) return '🟠';
  return '❌';
}

const Limit = 3900;

export function formatJht(r) {
  const ok = r.results.filter(x => x.firstOk).length;
  const lines = [
    `読み: ${r.reading}　変換エンジン: ${r.engine}`,
    `**最初の変換で出た: ${ok} / ${r.results.length} 通りの打ち方**`,
    '',
  ];
  // 打ち方ごとの結果 (1 行)
  for (const x of r.results) {
    if (x.firstOk && x.liveOk) lines.push(`✅ ${code(x.keys)}`);
    else lines.push(`${jhtMark(x)} ${code(x.keys)} → Space: ${x.firstOk ? 'OK' : x.first || '(なし)'}　Enter: ${x.liveOk ? 'OK' : x.entered || '(なし)'}`);
  }
  // 問題をまとめる (同じ問題は 1 つに)
  const problems = new Map();
  r.results.forEach((x, i) => {
    for (const note of x.notes) {
      if (!problems.has(note)) problems.set(note, []);
      problems.get(note).push(i);
    }
  });
  if (problems.size > 0) {
    lines.push('', `**問題 (${problems.size})**`);
    for (const [note, which] of problems) {
      const where = which.length === r.results.length ? 'すべての打ち方' : which.map(i => code(r.results[i].keys)).join(' ');
      lines.push(`・${note}`, `　└ ${where}`);
    }
  } else if (ok === r.results.length) {
    lines.push('', '問題は見つかりませんでした。');
  }
  lines.push('', '✅ 最初の変換で出る　🟡 Enter なら出る　🟠 日本語/英語の分かれ方は合っているが変換が違う　❌ 分かれ方が違う');

  // 長すぎるときは、打ち方の一覧を縮めてでも問題を最後まで出す
  let text = lines.join('\n');
  if (text.length <= Limit) return text;
  const head = lines.slice(0, 3).join('\n');
  const tail = lines.slice(3 + r.results.length).join('\n');
  const rest = Limit - head.length - tail.length - 40;
  let list = '';
  for (const line of lines.slice(3, 3 + r.results.length)) {
    if (list.length + line.length + 1 > rest) {
      list += '…(打ち方の一覧は省略)\n';
      break;
    }
    list += line + '\n';
  }
  text = `${head}\n${list}${tail}`;
  return text.length <= Limit ? text : text.slice(0, Limit - 20) + '\n…(長いので省略)';
}
