// ============================================================
//  check-api-comments.js — 檢查 API 函式的 Swagger 式註解，跟後端對不對得上（昱）
//
//  用法（先用 https 設定檔啟動後端）：
//    npm run check:api
//    npm run check:api -- https://localhost:7078/openapi/v1.json   指定網址
//    npm run check:api -- ./v1.json                                  指定已經存下來的檔案
//
//  檢查三件事：
//    1. 註解裡寫的「[方法] 網址」，後端真的有這支 API 嗎？
//       沒有 → 註解過期（後端改了路由）或打錯字。
//    2. src/api 裡每個「會呼叫 http 的 export 函式」，上方註解有沒有照組內格式寫「[方法] 網址」？
//    3. （僅供參考，不算錯誤）後端有、但前端註解從來沒提到的 API。
//       可能是前端還沒串，也可能是註解漏寫。
//
//  網址比對規則：大括號 {xxx} 代表「任何一段」。
//    註解寫 /api/reviews/{kind}/{ticket}/form，
//    可以對上後端的 /api/reviews/visit/{token}/form 和 /api/reviews/booking/{id}/form。
//    所以這支程式只能確認「這個網址存在」，不能確認參數名稱寫得對不對。
// ============================================================
import fs from "node:fs";
import path from "node:path";
import http from "node:http";
import https from "node:https";

const DEFAULT_SOURCE = "https://localhost:7078/openapi/v1.json";
const API_DIR = path.resolve(import.meta.dirname, "../src/api");
const METHODS = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD"];

// 組內格式：[GET] /api/...
const STRICT_PATTERN = new RegExp(
  `\\[(${METHODS.join("|")})\\]\\s+(\\/api\\/[A-Za-z0-9_\\-/{}.]*)`,
);
// 寬鬆格式：方法前後沒有中括號也接受（GET /api/...），用來找出「寫了但格式不對」的註解
const LOOSE_SOURCE = `\\[?\\b(${METHODS.join("|")})\\b\\]?\\s+(\\/api\\/[A-Za-z0-9_\\-/{}.]*)`;
const LOOSE_PATTERN = new RegExp(LOOSE_SOURCE);

// ── 讀後端的 OpenAPI 文件 ──────────────────────────────────

// 從網址或檔案讀進來。網址連不到時，錯誤交給 main() 處理（顯示「後端有沒有開」的提示）。
async function loadOpenApi(source) {
  if (!/^https?:\/\//.test(source)) {
    return JSON.parse(fs.readFileSync(source, "utf8"));
  }
  return JSON.parse(await download(source));
}

// 下載文字內容。localhost 用的是開發用憑證，Node 不認得，所以只對 localhost 略過憑證檢查。
function download(url) {
  const { protocol, hostname } = new URL(url);
  const isLocal = hostname === "localhost" || hostname === "127.0.0.1";
  const client = protocol === "https:" ? https : http;
  return new Promise((resolve, reject) => {
    const request = client.get(url, { rejectUnauthorized: !isLocal }, (response) => {
      if (response.statusCode !== 200) {
        reject(new Error(`HTTP ${response.statusCode}`));
        response.resume();
        return;
      }
      let body = "";
      response.setEncoding("utf8");
      response.on("data", (chunk) => (body += chunk));
      response.on("end", () => resolve(body));
    });
    request.on("error", reject);
  });
}

// 把 OpenAPI 的 paths 攤平成 [{ method: "GET", path: "/api/ping" }, ...]
function listEndpoints(doc) {
  const endpoints = [];
  for (const [routePath, operations] of Object.entries(doc.paths ?? {})) {
    for (const key of Object.keys(operations)) {
      const method = key.toUpperCase();
      if (METHODS.includes(method)) endpoints.push({ method, path: routePath, used: false });
    }
  }
  return endpoints;
}

// ── 讀前端的 src/api ───────────────────────────────────────

/** 每個檔案的每一行：{ file, lineNo, text } */
function readApiFiles() {
  const lines = [];
  for (const name of fs
    .readdirSync(API_DIR)
    .filter((n) => n.endsWith(".js"))
    .sort()) {
    const text = fs.readFileSync(path.join(API_DIR, name), "utf8").replace(/\r/g, "");
    text.split("\n").forEach((line, i) => lines.push({ file: name, lineNo: i + 1, text: line }));
  }
  return lines;
}

const isCommentLine = (text) => /^\s*(\/\/|\/\*|\*)/.test(text);

/** 找出「會呼叫 http 的 export 函式」，以及它上方緊貼著的註解 */
function listApiFunctions(lines) {
  const result = [];
  lines.forEach((line, i) => {
    const match = line.text.match(/^export\s+(?:async\s+)?function\s+(\w+)/);
    if (!match) return;

    // 函式本體：到下一個 export 或檔案結尾為止
    let end = i + 1;
    while (
      end < lines.length &&
      lines[end].file === line.file &&
      !/^export\s/.test(lines[end].text)
    )
      end++;
    const body = lines
      .slice(i, end)
      .map((l) => l.text)
      .join("\n");
    if (!/\bhttp\.\w+\(/.test(body)) return; // 不呼叫 API 的工具函式不用寫

    // 往上收集緊貼的註解
    const comments = [];
    for (
      let j = i - 1;
      j >= 0 && lines[j].file === line.file && isCommentLine(lines[j].text);
      j--
    ) {
      comments.unshift(lines[j].text);
    }
    result.push({
      file: line.file,
      lineNo: line.lineNo,
      name: match[1],
      comment: comments.join("\n"),
    });
  });
  return result;
}

// ── 比對 ────────────────────────────────────────────────

// 一段一段比：兩邊有一邊是 {xxx} 就算對上，否則要一模一樣（不分大小寫）
function pathMatches(commentPath, routePath) {
  const a = commentPath.replace(/\/$/, "").split("/");
  const b = routePath.replace(/\/$/, "").split("/");
  if (a.length !== b.length) return false;
  const isParam = (s) => /^\{.+\}$/.test(s);
  return a.every(
    (seg, i) => isParam(seg) || isParam(b[i]) || seg.toLowerCase() === b[i].toLowerCase(),
  );
}

async function main() {
  const source = process.argv[2] ?? DEFAULT_SOURCE;

  let doc;
  try {
    doc = await loadOpenApi(source);
  } catch (error) {
    // 最常見的原因是後端沒開，這裡把原因翻成看得懂的提示
    console.error(`讀不到 OpenAPI 文件：${source}`);
    console.error(`原因：${error.message}`);
    console.error("請確認後端已經用 https 設定檔啟動，或改傳已經存下來的 JSON 檔案路徑。");
    process.exitCode = 1;
    return;
  }

  const endpoints = listEndpoints(doc);
  const lines = readApiFiles();
  const problems = [];

  // 檢查 1：註解裡的網址存在嗎？（寬鬆格式也檢查，網址錯了不管格式都該知道）
  for (const line of lines.filter((l) => isCommentLine(l.text))) {
    for (const [, method, commentPath] of line.text.matchAll(new RegExp(LOOSE_SOURCE, "g"))) {
      const hits = endpoints.filter((e) => e.method === method && pathMatches(commentPath, e.path));
      hits.forEach((e) => (e.used = true));
      if (hits.length === 0) {
        problems.push(`${line.file}:${line.lineNo}  後端沒有這支 API：${method} ${commentPath}`);
      }
    }
  }

  // 檢查 2：每個呼叫 API 的函式都有照格式寫註解嗎？
  for (const fn of listApiFunctions(lines)) {
    if (STRICT_PATTERN.test(fn.comment)) continue;
    const reason = LOOSE_PATTERN.test(fn.comment)
      ? "註解格式不對，方法要加中括號，例如 [GET] /api/ping"
      : "缺少「[方法] 網址」註解";
    problems.push(`${fn.file}:${fn.lineNo}  ${fn.name}()：${reason}`);
  }

  // 檢查 3（參考）：前端註解沒提到的 API
  const unused = endpoints.filter((e) => !e.used);

  console.log(`後端共 ${endpoints.length} 支 API；src/api 檢查完畢。\n`);
  if (problems.length === 0) {
    console.log("✔ 沒有發現問題");
  } else {
    console.log(`✘ 發現 ${problems.length} 個問題：`);
    problems.forEach((p) => console.log(`  ${p}`));
    process.exitCode = 1;
  }
  if (unused.length > 0) {
    console.log(`\n（參考）前端註解沒有提到的 API ${unused.length} 支：`);
    unused.forEach((e) => console.log(`  ${e.method} ${e.path}`));
  }
}

await main();
