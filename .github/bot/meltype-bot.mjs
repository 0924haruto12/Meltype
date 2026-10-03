// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// Meltype の GitHub bot (.github/workflows/bot.yml から呼ぶ)。外部のパッケージは使わない (Node.js の fetch だけ)。
//   node meltype-bot.mjs parse    … Issue の作成・編集 / コメントのコマンドを読み、次の job に何をするかを渡す
//   node meltype-bot.mjs report   … 再現・テスト・パッケージの結果をコメントする
// Issue の本文・コメントは誰でも書けるので、コマンドとして実行しない (値として次の job に渡すだけ)。
import fs from 'node:fs';
import path from 'node:path';

const api = process.env.GITHUB_API_URL ?? 'https://api.github.com';
const repo = process.env.GITHUB_REPOSITORY;
const token = process.env.GITHUB_TOKEN;
const event = JSON.parse(fs.readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
const Marker = '<!-- meltype-bot -->';
const Maintainers = ['OWNER', 'MEMBER', 'COLLABORATOR'];
const MaxKeys = 300;

async function gh(method, url, body) {
  const response = await fetch(`${api}/repos/${repo}${url}`, {
    method,
    headers: { Authorization: `Bearer ${token}`, Accept: 'application/vnd.github+json', 'User-Agent': 'meltype-bot' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) throw new Error(`${method} ${url}: ${response.status} ${await response.text()}`);
  return response.status === 204 ? null : response.json();
}

function output(values) {
  const lines = Object.entries(values).map(([k, v]) => {
    // 複数行の値も渡せる形 (区切りは推測できない文字列)
    const delimiter = `EOF_${Math.random().toString(36).slice(2)}`;
    return `${k}<<${delimiter}\n${v ?? ''}\n${delimiter}`;
  });
  fs.appendFileSync(process.env.GITHUB_OUTPUT, lines.join('\n') + '\n');
}

/** Issue フォームの本文 (### 見出し + 値) を { 見出し: 値 } にする。 */
function parseForm(body) {
  const fields = {};
  for (const part of (body ?? '').split(/^### /m).slice(1)) {
    const newline = part.indexOf('\n');
    const title = part.slice(0, newline).trim();
    let value = part.slice(newline + 1).trim();
    if (value === '_No response_') value = '';
    fields[title] = value;
  }
  return fields;
}

const field = (form, prefix) => Object.entries(form).find(([k]) => k.startsWith(prefix))?.[1] ?? '';

function osLabels(os) {
  const labels = [];
  if (/Windows/i.test(os)) labels.push('windows');
  if (/Mac/i.test(os)) labels.push('mac');
  if (/Linux/i.test(os)) labels.push('linux');
  return labels;
}

function lastKey(text) {
  if (/^Space/.test(text)) return 'space';
  if (/途中/.test(text)) return 'none';
  return 'enter';
}

/** 打ったものとして打てる文字だけか (かなや漢字で書かれていたら、キーをそのまま書いてもらう)。 */
const isKeys = text => text.length > 0 && /^[\x20-\x7e]+$/.test(text);

async function upsertComment(issue, kind, text) {
  const marker = `${Marker}<!-- ${kind} -->`;
  const comments = await gh('GET', `/issues/${issue}/comments?per_page=100`);
  const mine = comments.find(c => c.user?.type === 'Bot' && c.body?.includes(marker));
  const body = `${marker}\n${text}`;
  if (mine) await gh('PATCH', `/issues/comments/${mine.id}`, { body });
  else await gh('POST', `/issues/${issue}/comments`, { body });
}

async function react(content) {
  if (event.comment) await gh('POST', `/issues/comments/${event.comment.id}/reactions`, { content }).catch(() => {});
}

const Help = [
  'Meltype bot のコマンド (コメントの 1 行目に書いてください):',
  '',
  '| コマンド | すること | 使える人 |',
  '|---|---|---|',
  '| `/repro <打ったキー> [space\\|enter\\|none]` | 最新のコードで打ってみて、結果を返す | だれでも |',
  '| `/explain <打ったキー>` | IME 自動切替での 1 文字ずつの判定理由 | だれでも |',
  '| `/test` | Pull Request のコードでテストを流す | メンテナー |',
  '| `/pack` | Pull Request のコードでテスト版の zip を作る | メンテナー |',
  '| `/help` | この一覧 | だれでも |',
].join('\n');

async function parse() {
  const issue = event.issue.number;
  const isPr = Boolean(event.issue.pull_request);

  // Issue を作った・直したとき: 仕分けと、誤判定の報告なら再現
  if (!event.comment) {
    if (event.action !== 'opened' && event.action !== 'edited') return output({ action: 'none' });
    const form = parseForm(event.issue.body);
    const labels = new Set(event.issue.labels.map(l => l.name));
    const add = new Set(osLabels(field(form, 'OS')));
    if (event.action === 'opened') add.add('未確認');

    const problems = [];
    const version = field(form, 'Meltype の版');
    if ('Meltype の版' in form && !/\d+\.\d+/.test(version)) problems.push('**Meltype の版** を書いてください (Windows: トレイのアイコンを右クリック →「Meltype について...」)。');
    const misdetection = labels.has('誤判定') || '出たもの' in form;
    const keys = field(form, '打ったもの');
    if (misdetection && !isKeys(keys)) problems.push('**打ったもの** は、変換された文字ではなく、押したキーをそのまま英字で書いてください (例: `nihongowohanasu`)。');

    if (problems.length > 0) {
      add.add('情報待ち');
      await upsertComment(issue, 'triage', ['報告ありがとうございます。確認のために、もう少し教えてください。', '', ...problems.map(p => `- ${p}`), '', '本文を編集して直してもらえれば、自動でもう一度確認します。'].join('\n'));
    } else if (labels.has('情報待ち')) {
      await gh('DELETE', `/issues/${issue}/labels/${encodeURIComponent('情報待ち')}`).catch(() => {});
    }
    if (add.size > 0) await gh('POST', `/issues/${issue}/labels`, { labels: [...add] });

    if (misdetection && problems.length === 0) {
      return output({
        action: 'repro', issue, keys: keys.slice(0, MaxKeys), last: lastKey(field(form, '最後に押したキー')),
        reported: field(form, '出たもの'), expected: field(form, '期待した結果'), kind: field(form, '種類'),
      });
    }
    return output({ action: 'none' });
  }

  // コメントのコマンド
  const line = (event.comment.body ?? '').split('\n')[0].trim();
  const match = line.match(/^\/(repro|explain|test|pack|help)\b\s*(.*)$/);
  if (!match || event.comment.user?.type === 'Bot') return output({ action: 'none' });
  const [, command, rest] = match;
  const maintainer = Maintainers.includes(event.comment.author_association);

  if (command === 'help') {
    await upsertComment(issue, `help-${event.comment.id}`, Help);
    return output({ action: 'none' });
  }
  if (command === 'test' || command === 'pack') {
    if (!isPr) {
      await upsertComment(issue, `error-${event.comment.id}`, `\`/${command}\` は Pull Request でだけ使えます。`);
      return output({ action: 'none' });
    }
    if (!maintainer) {
      await upsertComment(issue, `error-${event.comment.id}`, `\`/${command}\` はメンテナーだけが使えます (Pull Request のコードを動かすため)。`);
      return output({ action: 'none' });
    }
    await react('eyes');
    const pr = await gh('GET', `/pulls/${issue}`);
    return output({ action: command, issue, sha: pr.head.sha, comment: event.comment.id });
  }

  // /repro キー [space|enter|none]、/explain キー
  const words = rest.trim().split(/\s+/);
  let last = 'enter';
  if (command === 'repro' && ['space', 'enter', 'none'].includes(words.at(-1)) && words.length > 1) last = words.pop();
  const keys = words.join(' ').replace(/^`|`$/g, '');
  if (!isKeys(keys)) {
    await upsertComment(issue, `error-${event.comment.id}`, `打ったキーを英字で書いてください (例: \`/${command} nihongowohanasu\`)。\n\n${Help}`);
    return output({ action: 'none' });
  }
  await react('eyes');
  return output({ action: command, issue, keys: keys.slice(0, MaxKeys), last, reported: '', expected: '', kind: '', comment: event.comment.id });
}

// --- 結果のコメント ---

const asciiWords = text => (text ?? '').match(/[A-Za-z][A-Za-z'’-]*/g)?.map(w => w.toLowerCase()) ?? [];
const sameWords = (a, b) => asciiWords(a).join(' ') === asciiWords(b).join(' ');
const code = text => '`' + String(text ?? '').replace(/`/g, 'ˋ') + '`';

function readJson(file) {
  try {
    const lines = fs.readFileSync(file, 'utf8').trim().split('\n');
    return JSON.parse(lines.at(-1));
  } catch {
    return null;
  }
}

function readText(file, max = 6000) {
  try {
    const text = fs.readFileSync(file, 'utf8');
    return text.length > max ? '…\n' + text.slice(-max) : text;
  } catch {
    return null;
  }
}

async function report() {
  const action = process.env.BOT_ACTION;
  const issue = process.env.BOT_ISSUE;
  const dir = process.env.BOT_RESULTS ?? 'results';
  const runUrl = `${process.env.GITHUB_SERVER_URL}/${repo}/actions/runs/${process.env.GITHUB_RUN_ID}`;
  const keys = process.env.BOT_KEYS;

  if (action === 'repro') {
    const versions = fs.existsSync(dir) ? fs.readdirSync(dir).filter(f => f.startsWith('repro-')).sort() : [];
    if (versions.length === 0) {
      await upsertComment(issue, 'repro', `再現を試せませんでした ([実行結果](${runUrl}))。`);
      return;
    }
    const reported = process.env.BOT_REPORTED, expected = process.env.BOT_EXPECTED, kind = process.env.BOT_KIND ?? '';
    const rows = [], verdicts = [];
    for (const file of versions) {
      const name = file.replace(/^repro-/, '').replace(/\.json$/, '');
      const r = readJson(path.join(dir, file));
      if (!r) { rows.push(`| ${name} | (失敗) |`); continue; }
      const result = (r.committed ?? '') + (r.composing ?? '');
      rows.push(`| ${name} | ${code(result)} |`);
      if (name === 'main') verdicts.push(
        expected && sameWords(result, expected) ? 'match' :
        reported && sameWords(result, reported) ? 'reproduced' : 'different');
    }
    const verdict = verdicts[0];
    const conversion = /漢字/.test(kind);
    const lines = [
      `${code(keys)} を打ってみました (最後のキー: ${process.env.BOT_LAST})。`,
      '',
      '| 版 | 結果 |',
      '|---|---|',
      ...rows,
      '',
    ];
    if (conversion) lines.push('漢字の変換はここでは再現できません (変換エンジンを使わず、日本語 / 英語の判定だけを見ています)。');
    else if (verdict === 'reproduced') lines.push('🔁 報告と同じく、日本語と英語の分かれ方が期待と違いました。**再現しました。**');
    else if (verdict === 'match') lines.push('✅ 最新のコード (main) では、日本語と英語の分かれ方は期待どおりでした。次の版で直っているかもしれません。');
    else if (expected || reported) lines.push('報告とは違う結果になりました。アプリ・前後の文・学習 (以前に英字 / かなで確定した語) で変わることがあります。');
    lines.push('', `<sub>Windows のスペルチェッカーあり、テスト用の変換エンジン (漢字にはしない)。[実行結果](${runUrl})</sub>`);
    await upsertComment(issue, process.env.BOT_COMMENT_ID ? `repro-${process.env.BOT_COMMENT_ID}` : 'repro', lines.join('\n'));
    if (!conversion && verdict === 'reproduced') await gh('POST', `/issues/${issue}/labels`, { labels: ['再現済み'] });
    return;
  }

  if (action === 'explain') {
    const text = readText(path.join(dir, 'explain.txt'));
    await upsertComment(issue, `explain-${process.env.BOT_COMMENT_ID}`,
      text ? `${code(keys)} の判定理由 (IME 自動切替):\n\n\`\`\`\n${text.trim()}\n\`\`\`` : `判定理由を出せませんでした ([実行結果](${runUrl}))。`);
    return;
  }

  if (action === 'test') {
    const text = readText(path.join(dir, 'test.txt'));
    const summary = text?.match(/(\d+)\/(\d+) passed/);
    const ok = summary && summary[1] === summary[2];
    const failures = text ? text.split('\n').filter(l => /FAIL/.test(l)).slice(0, 20).join('\n') : '';
    await upsertComment(issue, `test-${process.env.BOT_COMMENT_ID}`, [
      `${ok ? '✅' : '❌'} テスト (${process.env.BOT_SHA?.slice(0, 7)}): ${summary ? `${summary[1]}/${summary[2]} 通過` : '結果を読めませんでした'}`,
      failures ? `\n\`\`\`\n${failures}\n\`\`\`` : '',
      `\n[実行結果](${runUrl})`,
    ].join('\n'));
    return;
  }

  if (action === 'pack') {
    const ok = process.env.BOT_JOB_RESULT === 'success';
    await upsertComment(issue, `pack-${process.env.BOT_COMMENT_ID}`, ok
      ? `📦 テスト版の zip を作りました (${process.env.BOT_SHA?.slice(0, 7)})。[実行結果](${runUrl}) の Artifacts の **meltype-pr-${issue}** からダウンロードできます (Windows 用、90 日間)。`
      : `❌ zip を作れませんでした。[実行結果](${runUrl})`);
  }
}

const mode = process.argv[2];
try {
  if (mode === 'parse') await parse();
  else if (mode === 'report') {
    await report();
    await react(process.env.BOT_JOB_RESULT === 'success' ? 'rocket' : 'confused');
  }
} catch (error) {
  console.error(error);
  process.exitCode = 1;
}
