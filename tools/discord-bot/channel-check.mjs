// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// chjht: チャンネルのメッセージを最初からすべて読み、1 件ずつ jht をして、問題があればそのたびに報告する。
// 何件かを同時に試す (並列)。頼まれたチャンネルは順番待ち (キュー) に入れ、1 つ終わったら次を自動で始める。
import { EmbedBuilder } from 'discord.js';
import { shorten } from './commands.mjs';

/** jht に渡せる文の長さ (Discord の bot の jht と同じ上限)。 */
export const MaxLength = 80;

/**
 * メッセージの本文を jht に渡す文にする。試さないなら { skip: 理由 }。
 * メンション・絵文字・URL は取り除く。日本語を含まない文 (英語だけ) は試しても意味が無いので飛ばす。
 */
export function prepareText(content) {
  const text = (content ?? '')
    .replace(/<a?:\w+:\d+>/g, '')          // カスタム絵文字
    .replace(/<[@#][!&]?\d+>/g, '')        // メンション・チャンネル
    .replace(/https?:\/\/\S+/g, '')         // URL
    .replace(/\s+/g, ' ')
    .trim();
  if (!text) return { skip: 'empty' };
  if ([...text].length > MaxLength) return { skip: 'long' };
  if (!/[぀-ヿ㐀-鿿]/.test(text)) return { skip: 'noJapanese' };
  return { text };
}

/** 決まった数ずつ同時に動かす。終わった順に onResult を呼ぶ。 */
async function runPool(items, concurrency, worker, isCancelled) {
  let next = 0;
  const runners = Array.from({ length: Math.max(1, concurrency) }, async () => {
    while (next < items.length && !isCancelled()) {
      const item = items[next++];
      await worker(item);
    }
  });
  await Promise.all(runners);
}

/** チャンネルのメッセージを古い順にすべて読む。 */
export async function readAllMessages(channel, isCancelled = () => false) {
  const messages = [];
  let after = '0';
  while (!isCancelled()) {
    const page = await channel.messages.fetch({ after, limit: 100 });
    if (page.size === 0) break;
    const sorted = [...page.values()].sort((a, b) => a.createdTimestamp - b.createdTimestamp);
    messages.push(...sorted);
    after = sorted.at(-1).id;
    if (page.size < 100) break;
  }
  return messages;
}

export class ChannelCheckQueue {
  #jobs = [];
  #current = null;
  #options;

  /**
   * options: { runJht(text) → Promise<結果>, formatJht(結果) → 文, concurrency }
   */
  constructor(options) {
    this.#options = options;
  }

  get current() { return this.#current; }
  get waiting() { return this.#jobs.length; }

  /** 順番待ちに入れる。戻り値は待ちの位置 (0 ならすぐ始まる)。 */
  enqueue(job) {
    this.#jobs.push(job);
    const position = this.#current ? this.#jobs.length : this.#jobs.length - 1;
    if (!this.#current) void this.#runNext();
    return position;
  }

  /** 今のチェックを止めて、待ちもすべて取り消す。止めたチャンネルの数を返す。 */
  stop(reportGuildId = null) {
    const belongsToGuild = job => reportGuildId === null || job.report.guildId === reportGuildId;
    const queued = this.#jobs.filter(belongsToGuild).length;
    this.#jobs = this.#jobs.filter(job => !belongsToGuild(job));
    const stopCurrent = this.#current && belongsToGuild(this.#current);
    const count = queued + (stopCurrent ? 1 : 0);
    if (stopCurrent) this.#current.cancelled = true;
    return count;
  }

  async #runNext() {
    const job = this.#jobs.shift();
    if (!job) {
      this.#current = null;
      return;
    }
    this.#current = { ...job, cancelled: false };
    try {
      await this.#check(this.#current);
    } catch (error) {
      console.error(error);
      const targetLabel = job.targetLabel ?? `<#${job.target.id}>`;
      await job.report.send(`❌ ${targetLabel} のチェック中にエラーが起きました: ${shorten(error.message, 200)}`).catch(() => {});
    }
    this.#current = null;
    void this.#runNext();
  }

  async #check(job) {
    const { target, targetLabel = `<#${job.target.id}>`, report, botId } = job;
    const isCancelled = () => job.cancelled;
    const started = Date.now();
    await report.send(`🔍 ${targetLabel} のメッセージを読んでいます…`);
    const messages = await readAllMessages(target, isCancelled);

    const stats = { total: messages.length, checked: 0, problems: 0, long: 0, other: 0, failed: 0 };
    const items = [];
    for (const message of messages) {
      // bot のメッセージと、bot へのコマンドは試さない
      if (message.author.bot || new RegExp(`^\\s*<@!?${botId}>`).test(message.content)) {
        stats.other++;
        continue;
      }
      const prepared = prepareText(message.content);
      if (prepared.skip === 'long') stats.long++;
      else if (prepared.skip) stats.other++;
      else items.push({ message, text: prepared.text });
    }
    await report.send(`▶ ${targetLabel}: ${messages.length} 件のうち ${items.length} 件を試します (同時に ${this.#options.concurrency} 件ずつ。80 文字を超える ${stats.long} 件と、日本語の無いもの・bot のもの ${stats.other} 件は飛ばします)。`);

    await runPool(items, this.#options.concurrency, async ({ message, text }) => {
      let result;
      try {
        result = await this.#options.runJht(text);
      } catch (error) {
        // ローマ字で打てない文字がある (読みが分からない) などは飛ばす
        stats.failed++;
        return;
      }
      stats.checked++;
      if (result.results.every(x => x.firstOk)) return;
      stats.problems++;
      if (isCancelled()) return;
      const embed = new EmbedBuilder()
        .setColor(result.results.some(x => x.firstOk) ? 0xf1c40f : 0xe74c3c)
        .setTitle(`「${shorten(result.expected, 60)}」`)
        .setURL(message.url)
        .setDescription(this.#options.formatJht(result))
        .setFooter({ text: `${message.author.displayName ?? message.author.username} のメッセージ` });
      // 1 件の送信に失敗しても、ほかのメッセージのチェックは続ける
      await report.send({ embeds: [embed] }).catch(error => console.error('chjht: 送れませんでした', error));
    }, isCancelled);

    const minutes = ((Date.now() - started) / 60000).toFixed(1);
    await report.send(job.cancelled
      ? `⏹ ${targetLabel} のチェックを止めました (試した ${stats.checked} 件のうち、問題があったもの ${stats.problems} 件)。`
      : `✅ ${targetLabel} のチェックが終わりました (${minutes} 分): 試した ${stats.checked} 件のうち、問題があったもの **${stats.problems} 件**` +
        `${stats.failed ? `、試せなかったもの ${stats.failed} 件` : ''}。80 文字を超えて飛ばしたもの ${stats.long} 件。`);
  }
}
