// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// Meltype のテスター用 Discord bot。
//   @Meltype help / henkan-test (ht) / jht / chjht / summary / add-list (al) / list / close-list (cl) / submit-list (sl)
// 設定は .env (.env.example を参照)。起動: npm start
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client, EmbedBuilder, Events, GatewayIntentBits, PermissionFlagsBits } from 'discord.js';
import { code, helpText, parseChannelIds, parseCommand, parseNumber, shorten } from './commands.mjs';
import { formatJht } from './jht-format.mjs';
import { ChannelCheckQueue, readAllMessages } from './channel-check.mjs';
import { formatSummary, parseJhtEmbed, summarize } from './summary.mjs';
import { runHenkan, runJht, sanitizeKeys } from './henkan.mjs';
import { Kinds, ListStore, Status } from './store.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const env = process.env;
const resolve = p => (p ? path.resolve(here, p) : '');
const config = {
  token: env.DISCORD_TOKEN,
  category: env.CATEGORY_ID,
  channels: {
    [env.MISCONVERSION_CHANNEL_ID]: 'misconversion',
    [env.BUG_CHANNEL_ID]: 'bug',
    [env.IDEA_CHANNEL_ID]: 'idea',
  },
  managerRole: env.MANAGER_ROLE_ID || null,
  dll: resolve(env.MELTYPE_DLL),
  mozc: resolve(env.MELTYPE_MOZC),
  dataFile: resolve(env.DATA_FILE || 'data/list.json'),
};
if (!config.token || !config.category) {
  console.error('DISCORD_TOKEN と CATEGORY_ID を .env に書いてください (.env.example を参照)。');
  process.exit(1);
}

const store = await new ListStore(config.dataFile).load();
const client = new Client({
  intents: [GatewayIntentBits.Guilds, GatewayIntentBits.GuildMessages, GatewayIntentBits.MessageContent],
  // bot の返事で、だれにも通知を飛ばさない (リストの内容に @everyone などがあっても)
  allowedMentions: { parse: [], repliedUser: false },
});

/** 指定のカテゴリーの中のチャンネル (とそのスレッド) か。 */
function inCategory(channel) {
  if (!channel) return false;
  if (channel.isThread?.()) return channel.parent?.parentId === config.category;
  return channel.parentId === config.category;
}

/** add-list できるチャンネルなら、その種類 (misconversion / bug / idea)。スレッドなら親のチャンネルで見る。 */
function kindOf(channel) {
  const id = channel.isThread?.() ? channel.parentId : channel.id;
  return config.channels[id] ?? null;
}

const label = item => `${Kinds[item.kind].emoji} #${item.id}`;
/** この bot のメンション (bot の名前はサーバーで変えられるので、名前ではなくメンションで案内する) */
const me = () => `<@${client.user.id}>`;

async function canClose(message, item) {
  return await isManager(message) || (!config.managerRole && item.addedById === message.author.id);
}

/** 管理する人か (MANAGER_ROLE_ID の役職、無ければ「メッセージの管理」の権限)。 */
async function isManager(message) {
  const member = message.member ?? await message.guild.members.fetch(message.author.id);
  if (config.managerRole) return member.roles.cache.has(config.managerRole);
  return member.permissions.has(PermissionFlagsBits.ManageMessages);
}

const channelQueue = new ChannelCheckQueue({
  runJht: text => runJht(text, { dll: config.dll, mozc: fs.existsSync(config.mozc) ? config.mozc : '' }),
  formatJht,
  concurrency: Number(env.CHJHT_CONCURRENCY) || 4,
});

async function handleChannelJht(message, rest) {
  if (!await isManager(message)) return message.reply('`chjht` は重いので、管理する人だけが使えます。');
  if (!fs.existsSync(config.dll)) return message.reply('変換のテストの準備ができていません (MELTYPE_DLL が見つかりません)。');
  const id = rest.match(/^<#(\d+)>$/)?.[1] ?? rest.match(/^(\d{15,})$/)?.[1];
  if (!id) return message.reply(`チャンネルを指定してください (例: ${me()} ${code('chjht #チャンネル')} か ${code('chjht 1234567890')})。`);
  const target = await client.channels.fetch(id).catch(() => null);
  if (!target || target.guildId !== message.guildId || !target.isTextBased?.() || !target.messages) {
    return message.reply('そのチャンネルが見つからないか、読めません (このサーバーのテキストのチャンネルで、bot が読めるもの)。');
  }
  const position = channelQueue.enqueue({ target, report: message.channel, botId: client.user.id });
  await message.reply(position === 0
    ? `<#${target.id}> のチェックを始めます。問題が見つかるたびに、このチャンネルに送ります。`
    : `<#${target.id}> を順番待ちに入れました (${position} 番目)。前のチャンネルが終わったら自動で始めます。`);
}

async function handleChannelJhtStop(message) {
  if (!await isManager(message)) return message.reply('管理する人だけが使えます。');
  const count = channelQueue.stop();
  await message.reply(count === 0 ? '動いているチェックはありません。' : `チェックを止めて、順番待ちを取り消しました (${count} チャンネル)。`);
}

async function handleSummary(message, rest) {
  if (!await isManager(message)) return message.reply('`summary` はチャンネルを全部読むので、管理する人だけが使えます。');
  const ids = parseChannelIds(rest);
  if (ids.length === 0) return message.reply(`chjht の結果が流れたチャンネルを指定してください (例: ${me()} ${code('summary #チャンネル1 #チャンネル2')})。`);
  const channels = [];
  for (const id of ids) {
    const channel = await client.channels.fetch(id).catch(() => null);
    if (!channel || channel.guildId !== message.guildId || !channel.isTextBased?.() || !channel.messages) {
      return message.reply(`<#${id}> が見つからないか、読めません (このサーバーのテキストのチャンネルで、bot が読めるもの)。`);
    }
    channels.push(channel);
  }
  const waiting = await message.reply(`⏳ ${channels.map(c => `<#${c.id}>`).join(' ')} の結果を読んでいます…`);
  const entries = [];
  for (const channel of channels) {
    for (const m of await readAllMessages(channel)) {
      if (m.author.id !== client.user.id) continue;
      for (const embed of m.embeds) {
        const entry = parseJhtEmbed(embed, m.url);
        if (entry) entries.push({ ...entry, at: m.createdTimestamp });
      }
    }
  }
  if (entries.length === 0) return waiting.edit('指定したチャンネルに jht の結果が見つかりませんでした。');
  const [first, ...more] = formatSummary(summarize(entries), channels.map(c => c.id));
  await waiting.edit(first);
  // 続き (2000 文字を超えた分) は順に送る
  for (const text of more) await message.channel.send(text);
}

async function handleHenkan(message, rest) {
  const keys = sanitizeKeys(rest);
  if (!keys) return message.reply(`打つキーを英字で書いてください (例: ${me()} ${code('ht kyouhagoogledekensaku')})。`);
  if (!fs.existsSync(config.dll)) return message.reply('変換のテストの準備ができていません (MELTYPE_DLL が見つかりません)。');
  const waiting = await message.reply('⏳ 打ってみています…');
  try {
    const r = await runHenkan(keys, { dll: config.dll, mozc: fs.existsSync(config.mozc) ? config.mozc : '' });
    const embed = new EmbedBuilder()
      .setColor(0x4ca0ff)
      .setTitle('変換のテスト')
      .addFields(
        { name: '打ったキー', value: '`' + shorten(r.typed, 900).replace(/`/g, 'ˋ') + '`' },
        { name: '打っている途中の表示', value: shorten(r.showing, 900) || '(なし)' },
        { name: 'Enter で確定', value: shorten(r.entered, 900) || '(なし)', inline: true },
        { name: 'Space で変換', value: (r.clauses?.length ? r.clauses.join(' | ') : shorten(r.converted, 900)) || '(なし)', inline: true },
      )
      .setFooter({ text: `変換エンジン: ${r.engine}` });
    if (r.candidates?.length) embed.addFields({ name: '最初の文節の候補', value: shorten(r.candidates.join('、'), 900) });
    await waiting.edit({ content: '', embeds: [embed] });
  } catch (error) {
    console.error(error);
    await waiting.edit('変換のテストに失敗しました。時間をおいてもう一度試してください。');
  }
}

async function handleJht(message, rest) {
  const text = rest.trim();
  if (!text) return message.reply(`出てほしい文を書いてください (例: ${me()} ${code('jht 私はgoogleが好きです')})。`);
  if ([...text].length > 80) return message.reply('文が長すぎます (80 文字まで)。');
  if (!fs.existsSync(config.dll)) return message.reply('変換のテストの準備ができていません (MELTYPE_DLL が見つかりません)。');
  const waiting = await message.reply('⏳ いろいろな打ち方で打ってみています…');
  try {
    const r = await runJht(text, { dll: config.dll, mozc: fs.existsSync(config.mozc) ? config.mozc : '' });
    const ok = r.results.filter(x => x.firstOk).length;
    const embed = new EmbedBuilder()
      .setColor(ok === r.results.length ? 0x2ecc71 : ok > 0 ? 0xf1c40f : 0xe74c3c)
      .setTitle(`「${shorten(r.expected, 60)}」の変換テスト`)
      .setDescription(formatJht(r));
    await waiting.edit({ content: '', embeds: [embed] });
  } catch (error) {
    console.error(error);
    await waiting.edit(`変換のテストに失敗しました。${error.userMessage ?? '時間をおいてもう一度試してください。'}`);
  }
}

async function handleAdd(message, rest) {
  const kind = kindOf(message.channel);
  if (!kind) return message.reply('`add-list` は、誤変換・バグ報告・機能提案のチャンネルで使ってください。');
  let content = rest, author = message.author, link = message.url;
  if (message.reference?.messageId) {
    // 返信先のメッセージをリストに入れる (内容を書き足していれば、それも添える)
    const target = await message.fetchReference();
    const attachments = [...target.attachments.values()].map(a => a.url);
    content = [target.content, rest && `(追記) ${rest}`, ...attachments].filter(Boolean).join('\n');
    author = target.author;
    link = target.url;
  }
  if (!content.trim()) return message.reply('リストに入れる内容を書くか、入れたいメッセージに返信して送ってください。');
  const item = await store.add({
    kind,
    content: content.trim(),
    author: author.displayName ?? author.username,
    authorId: author.id,
    link,
    addedBy: message.author.displayName ?? message.author.username,
    addedById: message.author.id,
  });
  await message.reply(`${label(item)} を **${Kinds[kind].label}** のリストに追加しました。\n> ${shorten(item.content, 150)}`);
}

const ListFilters = {
  誤変換: { kind: 'misconversion' }, バグ: { kind: 'bug' }, 提案: { kind: 'idea' },
  解決済み: { status: 'resolved' }, クローズ: { status: 'closed' }, すべて: { status: 'all' }, all: { status: 'all' },
};

async function handleList(message, rest) {
  const filter = ListFilters[rest.trim()] ?? {};
  const status = filter.status ?? 'open';
  const items = store.list({ status, kind: filter.kind });
  const title = `${filter.kind ? Kinds[filter.kind].label : 'すべての種類'} ・ ${status === 'all' ? 'すべての状態' : Status[status]} (${items.length} 件)`;
  if (items.length === 0) return message.reply(`${title}\nありません。`);
  const lines = items.slice(0, 40).map(i =>
    `${label(i)} ${shorten(i.content, 60)} — ${i.author}${i.status === 'open' ? '' : ` [${Status[i.status]}]`} [→](${i.link})`);
  let description = '';
  for (const line of lines) {
    if (description.length + line.length + 1 > 3900) break;
    description += line + '\n';
  }
  const embed = new EmbedBuilder().setColor(0x4ca0ff).setTitle(title).setDescription(description);
  if (items.length > 40) embed.setFooter({ text: `ほか ${items.length - 40} 件 (種類で絞り込めます: list 誤変換)` });
  await message.reply({ embeds: [embed] });
}

async function handleClose(message, rest, resolved) {
  const command = resolved ? 'submit-list' : 'close-list';
  const id = parseNumber(rest);
  if (id === null) return message.reply(`番号を書いてください (例: ${me()} ${code(`${command} 12`)})。`);
  const item = store.get(id);
  if (!item) return message.reply(`#${id} はリストにありません。`);
  if (!await canClose(message, item)) return message.reply('この操作は、管理する人 (またはリストに入れた本人) だけができます。');
  const result = await store.setStatus(id, resolved ? 'resolved' : 'closed', message.author.displayName ?? message.author.username);
  if (result.already) return message.reply(`${label(item)} は既に「${Status[result.item.status]}」です。`);
  await message.reply(`${resolved ? '✅' : '🔒'} ${label(item)} を「${Status[result.item.status]}」にしました。\n> ${shorten(item.content, 150)}`);
}

client.on(Events.MessageCreate, async message => {
  if (message.author.bot || !message.guild || !inCategory(message.channel)) return;
  const parsed = parseCommand(message.content, client.user.id);
  if (!parsed) return;
  try {
    switch (parsed.command) {
      case 'help': return await message.reply(helpText(me()));
      case 'jht': return await handleJht(message, parsed.rest);
      case 'chjht': return await handleChannelJht(message, parsed.rest);
      case 'chjhtStop': return await handleChannelJhtStop(message);
      case 'summary': return await handleSummary(message, parsed.rest);
      case 'henkan': return await handleHenkan(message, parsed.rest);
      case 'add': return await handleAdd(message, parsed.rest);
      case 'list': return await handleList(message, parsed.rest);
      case 'close': return await handleClose(message, parsed.rest, false);
      case 'submit': return await handleClose(message, parsed.rest, true);
      default: return await message.reply(`${code(parsed.name)} というコマンドはありません。${me()} ${code('help')} で一覧を見られます。`);
    }
  } catch (error) {
    console.error(error);
    await message.reply('エラーが起きました。もう一度試してください。').catch(() => {});
  }
});

client.once(Events.ClientReady, c => console.log(`ログインしました: ${c.user.tag}`));
await client.login(config.token);
