import test from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync, chmodSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { spawnSync } from "node:child_process";

const target = resolve("scripts/ci/stop-disposable-supabase.sh");
function run(mode) {
  const root = mkdtempSync(join(tmpdir(), "pharmalabvr-stop-"));
  try {
    const count = join(root, "calls");
    writeFileSync(count, "0");
    const supabase = String.raw`#!/bin/sh
n=$(cat "$MOCK_COUNT_FILE")
n=$((n + 1))
printf '%s\n' "$n" > "$MOCK_COUNT_FILE"
if [ "$MOCK_MODE" = transient ] && [ "$n" -eq 1 ]; then exit 1; fi
if [ "$MOCK_MODE" = always-fail ]; then exit 1; fi
exit 0
`;
    const docker = String.raw`#!/bin/sh
if [ "$1" != ps ]; then exit 42; fi
case "$MOCK_MODE" in
  leftover) echo "supabase_db_pharmalabvr" ;;
  transient)
    if [ "$(cat "$MOCK_COUNT_FILE")" -lt 2 ]; then echo "supabase_db_pharmalabvr"; fi ;;
  daemon-fail) exit 99 ;;
  otherproject) echo "supabase_db_somebody_else" ;;
esac
`;
    writeFileSync(join(root, "supabase"), supabase, { mode: 0o755 });
    writeFileSync(join(root, "docker"), docker, { mode: 0o755 });
    chmodSync(join(root, "supabase"), 0o755);
    chmodSync(join(root, "docker"), 0o755);
    const result = spawnSync("bash", [target], {
      encoding: "utf8", timeout: 15_000,
      env: { ...process.env, PATH: root + ":" + process.env.PATH,
        MOCK_COUNT_FILE: count, MOCK_MODE: mode, PLV_STOP_RETRY_DELAY: "0" }
    });
    return { ...result, calls: Number(requireCount(count)) };
  } finally { rmSync(root, { recursive: true, force: true }); }
}
function requireCount(path) {
  const { readFileSync } = requireShim();
  return readFileSync(path, "utf8");
}
function requireShim() { return { readFileSync: importedRead }; }
import { readFileSync as importedRead } from "node:fs";

test("cleanup retries transient CLI failure and proves no owned containers remain", () => {
  const r=run("transient");
  assert.equal(r.status,0,r.stderr);
  assert.equal(r.calls,2);
});
test("cleanup fails closed if project containers remain after all attempts", () => {
  const r=run("leftover");
  assert.notEqual(r.status,0);
  assert.equal(r.calls,3);
});
test("cleanup fails closed if Docker cannot enumerate project containers", () => {
  const r=run("daemon-fail");
  assert.notEqual(r.status,0);
});
test("cleanup does not mistake unrelated project containers for its own", () => {
  const r=run("otherproject");
  assert.equal(r.status,0,r.stderr);
});