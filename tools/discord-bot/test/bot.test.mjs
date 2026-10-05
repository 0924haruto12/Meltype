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
  const { formatJht, jhtMark } = await import('../jht-format.mjs');
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
  assert.ok(text.includes('Space: eBay出うる'));
  assert.ok(text.indexOf('**問題 (1)**') > text.indexOf('eBaydeuru'), '問題は最後にまとめて出す');
  assert.ok(text.includes('・2 番目の文節'));
});

test('chjht: 試す文の選び方', async () => {
  const { prepareText } = await import('../channel-check.mjs');
  assert.deepEqual(prepareText('私はgoogleが好きです'), { text: '私はgoogleが好きです' });
  assert.deepEqual(prepareText('<@123> 見て https://example.com/a これ <:smile:42>'), { text: '見て これ' });
  assert.equal(prepareText('hello world').skip, 'noJapanese');
  assert.equal(prepareText('あ'.repeat(81)).skip, 'long');
  assert.equal(prepareText('   ').skip, 'empty');
});

test('chjht: 問題があったものを送り、順番待ちのチャンネルを続けて試す', async () => {
  const { ChannelCheckQueue } = await import('../channel-check.mjs');
  const sent = [];
  const report = { send: async m => { sent.push(typeof m === 'string' ? m : m.embeds[0].data.title); } };
  const message = (id, content, bot = false) => ({ id, content, createdTimestamp: Number(id), url: `https://discord.com/channels/1/2/${id}`, author: { bot, username: 'a' } });
  const channel = (id, list) => ({ id, messages: { fetch: async ({ after }) => new Map(list.filter(m => BigInt(m.id) > BigInt(after)).map(m => [m.id, m])) } });
  const order = [];
  const queue = new ChannelCheckQueue({
    concurrency: 2,
    formatJht: () => '詳しく',
    runJht: async text => {
      order.push(text);
      if (text.includes('打てない')) throw new Error('読みが分からない');
      return { expected: text, results: [{ firstOk: !text.includes('問題'), liveOk: true }] };
    },
  });
  const first = channel('1', [message('10', 'ふつうの文'), message('11', '問題のある文'), message('12', 'bot の文', true), message('13', 'english only'), message('14', '打てない文')]);
  const second = channel('2', [message('20', '問題その2')]);
  assert.equal(queue.enqueue({ target: first, report, botId: '99' }), 0);
  assert.equal(queue.enqueue({ target: second, report, botId: '99' }), 1, '2 つ目は順番待ち');
  for (let i = 0; i < 100 && queue.current; i++) await new Promise(r => setTimeout(r, 5));
  assert.ok(sent.includes('「問題のある文」'), '問題のある文を送る');
  assert.ok(sent.includes('「問題その2」'), '順番待ちのチャンネルも自動で試す');
  assert.ok(!order.includes('bot の文') && !order.includes('english only'), 'bot・日本語の無い文は試さない');
  assert.ok(order.indexOf('問題その2') > order.indexOf('問題のある文'), '1 つ目のチャンネルが終わってから 2 つ目');
  assert.ok(sent.some(s => s.includes('<#1> のチェックが終わりました') && s.includes('**1 件**') && s.includes('試せなかったもの 1 件')), '最後にまとめ');
});

test('summary: チャンネルの指定・結果の読み取り・重複の無視・文の上限と JSON・決められない語の除外', async () => {
  const { parseChannelIds } = await import('../commands.mjs');
  const { formatJht } = await import('../jht-format.mjs');
  const { formatSummary, isRomaji, loadEnglishWords, parseJhtEmbed, summarize, summaryJson } = await import('../summary.mjs');
  assert.deepEqual(parseChannelIds('<#111111111111111111> 222222222222222222,<#111111111111111111>'), ['111111111111111111', '222222222222222222'], 'いくつでも・重複は 1 つに');
  assert.deepEqual(parseChannelIds('<@333333333333333333>'), [], 'ユーザーのメンションはチャンネルではない');

  const result = (expected, first, splitOk, notes) => ({
    expected, reading: 'x', engine: 'Mozc',
    results: [{ keys: 'a', entered: first, first, liveOk: false, firstOk: first === expected, splitOk, notes }, { keys: 'b', entered: expected, first: expected, liveOk: true, firstOk: true, splitOk: true, notes: [] }],
  });
  const embed = (r, url) => ({ title: `「${r.expected}」`, url, description: formatJht(r) });
  const split = parseJhtEmbed(embed(result('私はgoogle', '私はごおgle', false, ['日本語 / 英語の分かれ方が違う (英字: なし、出てほしいのは google)']), 'https://x/1'));
  assert.equal(split.expected, '私はgoogle');
  assert.equal(split.ok, 1);
  assert.equal(split.total, 2);
  assert.equal(split.marks['❌'], 1);
  assert.deepEqual(split.problems, ['日本語 / 英語の分かれ方が違う (英字: なし、出てほしいのは google)']);
  assert.equal(parseJhtEmbed({ title: 'リスト', description: '一覧' }), null, 'jht の結果でない埋め込みは読まない');

  const width = parseJhtEmbed(embed(result('A：B', 'A:B', true, ['記号の全角 / 半角だけが違う: 「:」→「：」']), 'https://x/2'));
  const old = { ...split, at: 1, ok: 0 };
  const summary = summarize([old, { ...split, at: 2 }, { ...width, at: 3 }]);
  assert.equal(summary.unique, 2, '同じ文は 1 つ');
  assert.equal(summary.duplicates, 1);
  assert.equal(summary.groups.split[0].ok, 1, '新しい結果を使う');
  assert.equal(summary.groups.width.length, 1, '全角 / 半角だけのものは分ける');

  // たくさんあっても文は 1 メッセージ (2000 文字以下)。全体は JSON
  const many = Array.from({ length: 300 }, (_, i) => ({ ...split, expected: `文${i}`.repeat(10), at: i }));
  const big = summarize(many);
  const text = formatSummary(big, ['111111111111111111']);
  assert.ok(text.length <= 2000);
  assert.match(text, /ほか \d+ 件/, '入りきらない分は件数だけ');
  assert.match(text, /summary\.json/);
  assert.equal(summaryJson(big, ['1']).split.length, 300, 'JSON にはすべて入る');
  // JSON にはリンクではなく結果そのもの
  const one = summaryJson(summarize([split]), ['1']).split[0];
  assert.equal(one.url, undefined);
  assert.equal(one.reading, 'x');
  assert.equal(one.engine, 'Mozc');
  assert.deepEqual(one.results, [{ mark: '❌', keys: 'a', space: '私はごおgle', enter: '私はごおgle' }, { mark: '✅', keys: 'b', space: 'OK', enter: 'OK' }]);
  assert.deepEqual(one.problems, [{ problem: '日本語 / 英語の分かれ方が違う (英字: なし、出てほしいのは google)', keys: ['a'] }]);

  // 英単語でもローマ字でも読める語 (tomato) だけの違いは除外
  const words = loadEnglishWords(path.resolve(import.meta.dirname, '../../../dictionaries'));
  assert.ok(words.has('tomato'));
  assert.ok(isRomaji('tomato') && isRomaji('made') && !isRomaji('good') && isRomaji('kitte') && isRomaji('honda'));
  const tomato = { ...split, expected: 'トマトを買う', problems: ['日本語 / 英語の分かれ方が違う (英字: tomato、出てほしいのは なし)'] };
  const good = { ...split, expected: 'goodかも', problems: ['日本語 / 英語の分かれ方が違う (英字: なし、出てほしいのは good)'] };
  const groups = summarize([tomato, good], { englishWords: words }).groups;
  assert.deepEqual(groups.ambiguous.map(e => e.expected), ['トマトを買う']);
  assert.deepEqual(groups.split.map(e => e.expected), ['goodかも'], 'ローマ字として読めない英単語は除外しない');
});

test('jht のテスト用プログラムを起動したまま続けて使い、止まっても続ける・秘密は渡さない', async () => {
  const { JhtWorkers, childEnv } = await import('../henkan.mjs');
  const { spawn } = await import('node:child_process');
  // テスト用プログラムの代わり: 1 行読むごとに 1 行の JSON を返す。「落ちる」なら止まる・「エラー」なら error を返す
  const fake = `
    const rl = require('node:readline').createInterface({ input: process.stdin });
    rl.on('line', line => {
      if (line === '') process.exit(0);
      if (line === '落ちる') process.exit(1);
      if (line === 'エラー') return console.log(JSON.stringify({ error: '読みが分かりません' }));
      console.log(JSON.stringify({ expected: line, pid: process.pid, token: process.env.DISCORD_TOKEN ?? null, results: [] }));
    });`;
  let started = 0;
  const workers = new JhtWorkers({
    dll: 'x.dll', mozc: '', size: 2, idleMs: 50,
    spawn: (_command, _args, options) => { started++; return spawn(process.execPath, ['-e', fake], options); },
  });
  const texts = ['あ', 'い', 'う', 'え', 'お'];
  const results = await Promise.all(texts.map(t => workers.run(t)));
  assert.deepEqual(results.map(r => r.expected), texts);
  assert.equal(started, 2, '同時に 2 つまで。続きは起動したままのプロセスで');
  assert.equal(new Set(results.map(r => r.pid)).size, 2);
  await assert.rejects(workers.run('エラー'), e => e.userMessage === '読みが分かりません');
  await assert.rejects(workers.run('落ちる'));
  assert.equal((await workers.run('か')).expected, 'か', 'プロセスが止まっても次の文は新しいプロセスで試す');
  await assert.rejects(workers.run('\n'), e => e.userMessage === '文が空です。');
  workers.close();
  assert.equal(workers.size, 0);

  const env = childEnv('mozc.exe', { DISCORD_TOKEN: 'secret', GH_TOKEN: 'x', PATH: '/bin' });
  assert.deepEqual(env, { PATH: '/bin', MELTYPE_MOZC: 'mozc.exe' }, 'トークンはテスト用プログラムに渡さない');
});
