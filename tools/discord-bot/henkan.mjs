// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Yukishiro
//
// henkan-test: Meltype のテスト用プログラム (Meltype.Core.Tests の --henkan) で、打ったキーを Meltype キーボードで打ってみる。
// 打った内容はコマンドとして実行せず、引数として渡すだけ (シェルを通さない)。
import { execFile } from 'node:child_process';

const MaxKeys = 300;

/** 打つキーとして使える文字だけにする (英数字・記号・空白)。 */
export function sanitizeKeys(text) {
  return [...(text ?? '')].filter(c => c >= ' ' && c <= '~').slice(0, MaxKeys).join('').trim();
}

export function runHenkan(keys, { dll, mozc, timeoutMs = 30000 }) {
  return new Promise((resolve, reject) => {
    execFile('dotnet', [dll, '--henkan', keys], {
      timeout: timeoutMs,
      windowsHide: true,
      maxBuffer: 1024 * 1024,
      env: { ...process.env, MELTYPE_MOZC: mozc ?? '' },
    }, (error, stdout) => {
      if (error) return reject(error);
      const line = stdout.trim().split('\n').pop();
      try {
        resolve(JSON.parse(line));
      } catch {
        reject(new Error(`結果を読めませんでした: ${line}`));
      }
    });
  });
}
