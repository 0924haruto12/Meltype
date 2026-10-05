// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// henkan-test / japanese-henkan-test: Meltype のテスト用プログラム (--henkan / --jht) で、Meltype キーボードで打ってみる。
// 打った内容はコマンドとして実行せず、引数として渡すだけ (シェルを通さない)。
import { execFile, spawn } from 'node:child_process';
import readline from 'node:readline';

const MaxKeys = 300;

/** 打つキーとして使える文字だけにする (英数字・記号・空白)。 */
export function sanitizeKeys(text) {
  return [...(text ?? '')].filter(c => c >= ' ' && c <= '~').slice(0, MaxKeys).join('').trim();
}

/** 出てほしい文 (jht)。改行と制御文字を取り、長さを区切る。 */
export function sanitizeText(text) {
  return [...(text ?? '')].filter(c => c >= ' ').slice(0, 120).join('').trim();
}

/**
 * テスト用プログラムに渡す環境変数。bot のトークンなどの秘密は渡さない
 * (テスト用プログラムやその先の Mozc のヘルパーに、Discord を操作できる値を持たせない)。
 */
export function childEnv(mozc, base = process.env) {
  const env = { ...base, MELTYPE_MOZC: mozc ?? '' };
  for (const key of Object.keys(env)) if (/TOKEN|SECRET|PASSWORD|API_KEY/i.test(key)) delete env[key];
  return env;
}

/** テスト用プログラムの 1 行の答えを結果にする (入力の問題なら利用者に見せる文を付けたエラー)。 */
function parseResult(line) {
  let result = null;
  try { result = JSON.parse(line); } catch { /* 下で扱う */ }
  if (result?.error) {
    const e = new Error(result.error);
    e.userMessage = result.error;
    throw e;
  }
  if (!result) throw new Error(`結果を読めませんでした: ${line}`);
  return result;
}

function run(args, { dll, mozc, timeoutMs }) {
  return new Promise((resolve, reject) => {
    execFile('dotnet', [dll, ...args], {
      timeout: timeoutMs,
      windowsHide: true,
      maxBuffer: 4 * 1024 * 1024,
      env: childEnv(mozc),
    }, (error, stdout) => {
      const line = stdout.trim().split('\n').pop() ?? '';
      let result;
      try {
        result = parseResult(line);
      } catch (e) {
        // 入力の問題 (読みが分からない など) は、プロセスの失敗より先に利用者に見せる
        return reject(e.userMessage || !error ? e : error);
      }
      if (error) return reject(error);
      resolve(result);
    });
  });
}

export const runHenkan = (keys, options) => run(['--henkan', keys], { timeoutMs: 30000, ...options });

export const runJht = (text, options) => run(['--jht', sanitizeText(text)], { timeoutMs: 120000, ...options });

/**
 * jht を、起動したままのテスト用プログラム (--jht-batch) で続けて試す。chjht のように多くの文を試すときに使う。
 * 1 文ごとに dotnet と Mozc を起動し直さない分、ずっと速い (試す内容・結果は 1 文ずつのときと同じ)。
 * size 個のプロセスを同時に使い、しばらく使わなければ止める。
 */
export class JhtWorkers {
  #options;
  #idle = [];
  #all = new Set();
  #waiting = [];
  #idleTimer = null;

  /** options: { dll, mozc, size, timeoutMs, idleMs, spawn } */
  constructor(options) {
    this.#options = { size: 4, timeoutMs: 120000, idleMs: 60000, spawn, ...options };
  }

  /** 動いているプロセスの数。 */
  get size() { return this.#all.size; }

  /** 出てほしい文 (と / 読み) を試す。 */
  run(text) {
    // 空行はテスト用プログラムにとって「終わり」なので送らない
    const line = sanitizeText(text);
    if (!line) return Promise.reject(Object.assign(new Error('文が空です'), { userMessage: '文が空です。' }));
    return new Promise((resolve, reject) => {
      this.#waiting.push({ line, resolve, reject });
      this.#dispatch();
    });
  }

  /** すべてのプロセスを止める (待っているものは失敗にする)。 */
  close() {
    clearTimeout(this.#idleTimer);
    for (const job of this.#waiting.splice(0)) job.reject(new Error('止めました'));
    for (const worker of [...this.#all]) this.#kill(worker, new Error('止めました'));
  }

  #dispatch() {
    clearTimeout(this.#idleTimer);
    while (this.#waiting.length > 0) {
      const worker = this.#idle.pop() ?? (this.#all.size < this.#options.size ? this.#start() : null);
      if (!worker) break;
      this.#send(worker, this.#waiting.shift());
    }
    if (this.#waiting.length === 0 && this.#idle.length === this.#all.size && this.#all.size > 0) {
      // しばらく使わなければ止める (Mozc を含めてメモリを使うので)
      this.#idleTimer = setTimeout(() => { for (const worker of [...this.#idle]) this.#kill(worker); }, this.#options.idleMs);
      this.#idleTimer.unref?.();
    }
  }

  #start() {
    const { dll, mozc } = this.#options;
    const child = this.#options.spawn('dotnet', [dll, '--jht-batch'], { windowsHide: true, env: childEnv(mozc), stdio: ['pipe', 'pipe', 'ignore'] });
    const worker = { child, job: null, timer: null };
    this.#all.add(worker);
    const lines = readline.createInterface({ input: child.stdout });
    lines.on('line', line => {
      const job = worker.job;
      if (!job) return;
      clearTimeout(worker.timer);
      worker.job = null;
      try { job.resolve(parseResult(line.trim())); } catch (e) { job.reject(e); }
      this.#idle.push(worker);
      this.#dispatch();
    });
    child.on('error', error => this.#kill(worker, error));
    child.on('exit', () => this.#kill(worker, new Error('テスト用プログラムが止まりました')));
    child.stdin.on('error', () => { /* 止まったプロセスへの書き込みは exit で扱う */ });
    return worker;
  }

  #send(worker, job) {
    worker.job = job;
    worker.timer = setTimeout(() => this.#kill(worker, new Error('時間内に終わりませんでした')), this.#options.timeoutMs);
    worker.child.stdin.write(job.line + '\n');
  }

  #kill(worker, error = null) {
    if (!this.#all.delete(worker)) return;
    clearTimeout(worker.timer);
    this.#idle = this.#idle.filter(w => w !== worker);
    if (worker.job) worker.job.reject(error ?? new Error('止めました'));
    worker.job = null;
    try { worker.child.stdin.end(); } catch { /* 既に止まっている */ }
    try { worker.child.kill(); } catch { /* 既に止まっている */ }
    // 待っているものがあれば、新しいプロセスで続ける
    if (this.#waiting.length > 0) this.#dispatch();
  }
}
