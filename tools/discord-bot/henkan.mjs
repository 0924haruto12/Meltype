// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// henkan-test / japanese-henkan-test: Meltype のテスト用プログラム (--henkan / --jht) で、Meltype キーボードで打ってみる。
// 打った内容はコマンドとして実行せず、引数として渡すだけ (シェルを通さない)。
import { execFile } from 'node:child_process';

const MaxKeys = 300;

/** 打つキーとして使える文字だけにする (英数字・記号・空白)。 */
export function sanitizeKeys(text) {
  return [...(text ?? '')].filter(c => c >= ' ' && c <= '~').slice(0, MaxKeys).join('').trim();
}

/** 出てほしい文 (jht)。改行と制御文字を取り、長さを区切る。 */
export function sanitizeText(text) {
  return [...(text ?? '')].filter(c => c >= ' ').slice(0, 120).join('').trim();
}

function run(args, { dll, mozc, timeoutMs }) {
  return new Promise((resolve, reject) => {
    execFile('dotnet', [dll, ...args], {
      timeout: timeoutMs,
      windowsHide: true,
      maxBuffer: 4 * 1024 * 1024,
      env: { ...process.env, MELTYPE_MOZC: mozc ?? '' },
    }, (error, stdout) => {
      const line = stdout.trim().split('\n').pop() ?? '';
      let result = null;
      try { result = JSON.parse(line); } catch { /* 下で扱う */ }
      if (result?.error) {
        // 入力の問題 (読みが分からない など)。利用者に見せる
        const e = new Error(result.error);
        e.userMessage = result.error;
        return reject(e);
      }
      if (error) return reject(error);
      if (!result) return reject(new Error(`結果を読めませんでした: ${line}`));
      resolve(result);
    });
  });
}

export const runHenkan = (keys, options) => run(['--henkan', keys], { timeoutMs: 30000, ...options });

export const runJht = (text, options) => run(['--jht', sanitizeText(text)], { timeoutMs: 120000, ...options });
