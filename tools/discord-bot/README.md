# Meltype の Discord bot

テスター用の Discord サーバーで使う bot です。先頭に bot へのメンションを付けて送ります。

| コマンド | 別名 | すること | 使える場所 |
|---|---|---|---|
| `@Meltype help` | | 使い方 | カテゴリー内のどこでも |
| `@Meltype henkan-test <打つキー>` | `ht` | Meltype でどう変換されるか (打っている途中の表示・Enter・Space・候補) | カテゴリー内のどこでも |
| `@Meltype list [誤変換\|バグ\|提案\|解決済み\|クローズ\|すべて]` | `ls` | リストの一覧 (省略すると未解決) | カテゴリー内のどこでも |
| `@Meltype add-list [内容]` | `al` | リストに追加。メッセージに返信して送ると、そのメッセージを追加 | 誤変換・バグ報告・機能提案のチャンネル |
| `@Meltype close-list <番号>` | `cl` | クローズ (対応しない・重複など) | 同上 |
| `@Meltype submit-list <番号>` | `sl` | 解決してクローズ | 同上 |

- 番号は 誤変換・バグ・提案 で共通の通し番号です (GitHub の Issue と同じ)。追加したチャンネルで種類が決まります。
- 指定したカテゴリー (`CATEGORY_ID`) の中のチャンネルとスレッドでだけ反応します。
- `close-list` / `submit-list` は、`MANAGER_ROLE_ID` の役職の人だけ (空なら「メッセージの管理」の権限がある人と、リストに入れた本人)。
- リストは `data/list.json` に保存します (Git には入れません)。

## 準備

### 1. Discord の bot を作る (ご自身で)

1. [Discord Developer Portal](https://discord.com/developers/applications) → New Application (名前は Meltype など)
2. **Bot** → Reset Token でトークンを作る。**トークンはほかの人に見せない・チャットやファイルに貼らない** (下の `.env` にだけ書く)
3. **Bot** → Privileged Gateway Intents の **MESSAGE CONTENT INTENT** を ON (メッセージの内容を読むため)
4. **OAuth2 → URL Generator**: Scopes に `bot`、Bot Permissions に
   `View Channels`・`Send Messages`・`Send Messages in Threads`・`Embed Links`・`Read Message History` を選び、できた URL でサーバーに招待する

### 2. Meltype の変換のテストの準備

```
dotnet build src/Meltype.Core.Tests -c Release
```

Mozc の変換ヘルパー (`native/mozc/bin/`) があれば、アプリと同じく漢字に変換します。無ければ日本語 / 英語の判定だけを返します。

### 3. 起動

```
cd tools/discord-bot
npm install
copy .env.example .env    (Mac・Linux は cp)
```

`.env` の `DISCORD_TOKEN` にトークンを書いて (ほかはそのままで動きます)、

```
npm start
```

「ログインしました」と出れば動いています。止めるときは Ctrl+C。

## 動かす場所

bot は Discord に外から接続しに行くだけで、外からの接続は受けません (ポートを開ける必要はありません)。
動かしている間だけ反応するので、ずっと使うなら次のどれかで動かし続けます。

- 自分の PC: Windows のタスク スケジューラで、ログオン時に `npm start` を実行する
- 小さなサーバー (VPS・Raspberry Pi など): Linux 版の Mozc のヘルパーは GitHub Actions の linux.yml で作れます

## テスト

```
npm test
```
