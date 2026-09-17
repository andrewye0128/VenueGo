#!/bin/sh
# PreToolUse guard for Edit|Write: ask before touching .csproj, appsettings*.json
# (except the gitignored appsettings.Local.json / appsettings.Local.example.json),
# or anything under a Migrations-style path. Per user's personal workflow rule.
# Uses node (not jq — jq isn't available in this environment) to read stdin JSON.

node -e '
let data = "";
process.stdin.on("data", c => data += c);
process.stdin.on("end", () => {
  let f;
  try { f = JSON.parse(data).tool_input && JSON.parse(data).tool_input.file_path; } catch (e) { f = null; }
  if (!f) process.exit(0);

  let reason = null;
  if (/(^|[\\\/])appsettings(\.[A-Za-z]+)?\.json$/i.test(f) && !/appsettings\.local/i.test(f)) {
    reason = "appsettings 設定檔";
  } else if (/\.csproj$/i.test(f)) {
    reason = "csproj 專案檔";
  } else if (/[\\\/]migrations?[\\\/]/i.test(f)) {
    reason = "Migration 檔案";
  }

  if (!reason) process.exit(0);

  const out = {
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: "ask",
      permissionDecisionReason: `這是${reason}（${f}），依你的個人規則，改動前要先跟你確認。`
    }
  };
  process.stdout.write(JSON.stringify(out));
});
'
