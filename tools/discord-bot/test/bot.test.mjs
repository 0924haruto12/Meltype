// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { parseCommand, parseNumber, shorten } from '../commands.mjs';
import { sanitizeKeys } from '../henkan.mjs';
import { ListStore } from '../store.mjs';

test('メンションで始まるメッセージだけをコマンドとして読む', () => {
  assert.deepEqual(parseCommand('<@123> ht kyouha', '123'), { command: 'henkan', name: 'ht', rest: 'kyouha' });
  assert.equal(parseCommand('<@!123> henkan-test  a b ', '123').rest, 'a b');
  assert.equal(parseCommand('<@123>', '123').command, 'help');
  assert.equal(parseCommand('<@123> AL 内容', '123').command, 'add');
  assert.equal(parseCommand('<@123> sl 12', '123').command, 'submit');
  assert.equal(parseCommand('<@123> cl #3', '123').command, 'close');
  assert.equal(parseCommand('<@123> foo', '123').command, 'unknown');
  assert.equal(parseCommand('こんにちは <@123> ht a', '123'), null, '先頭がメンションでなければ無視');
  assert.equal(parseCommand('<@999> ht a', '123'), null, 'ほかの人へのメンションは無視');
});

test('番号・文の短縮・打つキー', () => {
  assert.equal(parseNumber('12'), 12);
  assert.equal(parseNumber('#7'), 7);
  assert.equal(parseNumber('abc'), null);
  assert.equal(shorten('あいうえお', 3), 'あい…');
  assert.equal(sanitizeKeys('  kyouha日本語"; rm -rf / '), 'kyouha"; rm -rf /', '英数字・記号だけ (コマンドとしては実行しない)');
});

test('誤変換・バグ・提案で共通の番号を振り、閉じられる', async () => {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), 'meltype-bot-'));
  const file = path.join(dir, 'list.json');
  const store = await new ListStore(file).load();
  const a = await store.add({ kind: 'misconversion', content: 'koreareka', author: 'A', link: 'x', addedById: '1' });
  const b = await store.add({ kind: 'bug', content: '止まる', author: 'B', link: 'y', addedById: '2' });
  const [c, d] = await Promise.all([
    store.add({ kind: 'idea', content: '提案1', author: 'C', link: 'z', addedById: '3' }),
    store.add({ kind: 'idea', content: '提案2', author: 'C', link: 'z', addedById: '3' }),
  ]);
  assert.deepEqual([a.id, b.id, c.id, d.id], [1, 2, 3, 4], '種類に関係なく通し番号 (同時に追加しても重ならない)');
  assert.equal((await store.setStatus(2, 'resolved', 'me')).item.status, 'resolved');
  assert.equal((await store.setStatus(2, 'closed', 'me')).already, true, '閉じたものはもう一度閉じない');
  assert.equal(await store.setStatus(99, 'closed', 'me'), null);
  assert.deepEqual(store.list().map(i => i.id), [4, 3, 1], '未解決だけ、新しい順');
  assert.deepEqual(store.list({ kind: 'idea' }).map(i => i.id), [4, 3]);
  const reloaded = await new ListStore(file).load();
  assert.equal(reloaded.get(2).status, 'resolved', 'ファイルに保存される');
  const next = await reloaded.add({ kind: 'bug', content: 'x', author: 'D', link: 'w', addedById: '4' });
  assert.equal(next.id, 5, '読み直しても番号は続きから');
});

test('使い方は実際の bot のメンションで書く', async () => {
  const { helpText } = await import('../commands.mjs');
  const text = helpText('<@42>');
  assert.ok(text.includes('<@42> `ht kyouhagoogledekensaku`'));
  assert.ok(!text.includes('@Meltype'), 'bot の名前を決め打ちしない');
  assert.equal(parseCommand('<@42> jht 私はgoogleが好きです', '42').command, 'jht');
  assert.equal(parseCommand('<@42> japanese-henkan-test 文', '42').command, 'jht');
});

test('jht の結果の表示', async () => {
  const { formatJht, jhtMark } = await import('../commands.mjs');
  const r = {
    expected: 'eBayで売る', reading: 'でうる', engine: 'Mozc',
    results: [
      { keys: 'eBaydeuru', entered: 'eBayでうる', first: 'eBay出うる', liveOk: false, firstOk: false, splitOk: true, notes: ['2 番目の文節「出うる」: …'] },
      { keys: 'ok', entered: 'eBayで売る', first: 'eBayで売る', liveOk: true, firstOk: true, splitOk: true, notes: [] },
    ],
  };
  assert.equal(jhtMark(r.results[0]), '🟠');
  const text = formatJht(r);
  assert.ok(text.includes('最初の変換で出た: 1 / 2'));
  assert.ok(text.includes('Space で変換: eBay出うる'));
  assert.ok(text.includes('・2 番目の文節'));
});
