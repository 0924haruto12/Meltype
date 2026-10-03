// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// 誤変換・バグ・提案のリスト。GitHub の Issue と同じく、種類に関係なく 1 から続く共通の番号で管理する。
// JSON ファイル 1 つに保存する (書き込みは一時ファイルに書いてから置き換えるので、途中で落ちても壊れない)。
import fs from 'node:fs/promises';
import path from 'node:path';

export const Kinds = {
  misconversion: { label: '誤変換', emoji: '🔤' },
  bug: { label: 'バグ', emoji: '🐛' },
  idea: { label: '提案', emoji: '💡' },
};

export const Status = {
  open: '未解決',
  resolved: '解決済み',
  closed: 'クローズ',
};

export class ListStore {
  #file;
  #data = { next: 1, items: [] };
  #queue = Promise.resolve();

  constructor(file) {
    this.#file = file;
  }

  async load() {
    try {
      this.#data = JSON.parse(await fs.readFile(this.#file, 'utf8'));
    } catch (error) {
      if (error.code !== 'ENOENT') throw error;
    }
    return this;
  }

  /** 1 件追加して、その項目を返す。 */
  add({ kind, content, author, authorId, link, addedBy, addedById }) {
    return this.#write(data => {
      const item = {
        id: data.next++,
        kind,
        content,
        author,
        authorId,
        link,
        addedBy,
        addedById,
        status: 'open',
        createdAt: new Date().toISOString(),
      };
      data.items.push(item);
      return item;
    });
  }

  /** 状態を変える (resolved / closed)。項目が無ければ null、既に閉じていれば { item, already: true }。 */
  setStatus(id, status, by) {
    return this.#write(data => {
      const item = data.items.find(i => i.id === id);
      if (!item) return null;
      if (item.status !== 'open') return { item, already: true };
      item.status = status;
      item.closedAt = new Date().toISOString();
      item.closedBy = by;
      return { item, already: false };
    });
  }

  get(id) {
    return this.#data.items.find(i => i.id === id) ?? null;
  }

  /** 一覧 (status が無ければ未解決のもの)。新しい番号から。 */
  list({ status = 'open', kind } = {}) {
    return this.#data.items
      .filter(i => (status === 'all' || i.status === status) && (!kind || i.kind === kind))
      .sort((a, b) => b.id - a.id);
  }

  /** 書き込みは 1 つずつ順番に (同時に来ても番号が重ならないように)。 */
  #write(change) {
    const run = this.#queue.then(async () => {
      const result = change(this.#data);
      await fs.mkdir(path.dirname(this.#file), { recursive: true });
      const temp = `${this.#file}.tmp`;
      await fs.writeFile(temp, JSON.stringify(this.#data, null, 2));
      await fs.rename(temp, this.#file);
      return result;
    });
    this.#queue = run.catch(() => {});
    return run;
  }
}
