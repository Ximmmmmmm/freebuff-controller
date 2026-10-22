// Freebuff 多开控制器 —— 会话库直读/直删脚本。
//
// 由控制器用 Freebuff 自带的 resources/bun/bun.exe 运行（bun:sqlite），不引新依赖。
// 数据源就是各项目自己的库：<会话库目录>\<项目目录>\desktop-v2.db。
//
// 为什么不用 /api/projects 那套：见控制器里 ThreadDb 上面的注释——
// 项目目录不在了、会话是云端的、id 还是 legacy-chat-*，三种情况下接口都列不出来/删不掉。
//
// 用法：
//   bun threaddb.js list   <projectsDir>
//   bun threaddb.js delete <projectsDir> <idsJson|@ids.json>
// ids 里只有十六进制和短横线，写成 JSON 数组即可；太长就落文件（@路径）。
//
// 输出（stdout，UTF-8，一行 JSON）：
//   list   → {"projects":[{"dir":"...","projectPath":"...","threads":[{...}]}],"errors":[{"dir":"...","error":"..."}]}
//   delete → {"deleted":["id"...],"failed":[{"id":"...","error":"..."}]}
// 任何异常都退化成 JSON，控制台不抛栈（控制器只解析 stdout）。

const { Database } = require("bun:sqlite");
const fs = require("fs");
const path = require("path");

const root = process.argv[2];
const mode = process.argv[3] || "list";

function argJson(value, fallback) {
  if (!value) return fallback;
  if (value.charAt(0) === "@") return JSON.parse(fs.readFileSync(value.slice(1), "utf8"));
  return JSON.parse(value);
}

function collect(dir) {
  const out = [];
  let names = [];
  try {
    names = fs.readdirSync(dir);
  } catch (e) {
    return out;
  }
  for (const name of names) {
    const full = path.join(dir, name);
    let st = null;
    try {
      st = fs.statSync(full);
    } catch (e) {
      continue;
    }
    if (!st.isDirectory()) continue;
    const db = path.join(full, "desktop-v2.db");
    if (!fs.existsSync(db)) continue;
    let projectPath = "";
    try {
      projectPath = JSON.parse(fs.readFileSync(path.join(full, "project.json"), "utf8")).projectPath || "";
    } catch (e) {}
    out.push({ dir: name, db: db, projectPath: projectPath });
  }
  return out;
}

// 只读优先；库被 Freebuff 占着时只读会打不开（需要 shm 写权限），退回普通打开。
function open(file, readonly) {
  let db = null;
  if (readonly) {
    try {
      db = new Database(file, { readonly: true });
    } catch (e) {
      db = null;
    }
  }
  if (!db) db = new Database(file);
  try {
    db.exec("PRAGMA busy_timeout = 4000");
  } catch (e) {}
  if (!readonly) {
    // 级联删 messages / queue_items 全靠它；不加就只删掉 threads 一行，记录全成孤儿。
    try {
      db.exec("PRAGMA foreign_keys = ON");
    } catch (e) {}
  }
  return db;
}

function tables(db) {
  const set = new Set();
  try {
    for (const row of db.query("select name from sqlite_master where type='table'").all()) set.add(row.name);
  } catch (e) {}
  return set;
}

function columns(db, table) {
  const set = new Set();
  try {
    for (const row of db.query("PRAGMA table_info(" + table + ")").all()) set.add(row.name);
  } catch (e) {}
  return set;
}

const projects = collect(root);

if (mode === "list") {
  const result = { projects: [], errors: [] };
  for (const p of projects) {
    const entry = { dir: p.dir, projectPath: p.projectPath, threads: [] };
    let db = null;
    try {
      db = open(p.db, true);
      const have = tables(db);
      if (have.has("threads")) {
        const cols = columns(db, "threads");
        // 列名跨版本变过（archived_at → sidebar_archived_at 之类），先问表里有什么再拼 SELECT。
        const want = ["id", "title", "status", "turn_state", "project_path", "created_at", "updated_at",
                      "last_prompt_at", "last_turn_finished_at", "sidebar_archived_at", "archived_at", "will_continue"];
        const pick = want.filter((c) => cols.has(c));
        if (cols.has("id")) {
          for (const row of db.query("SELECT " + pick.join(", ") + " FROM threads").all()) entry.threads.push(row);
        }
      }
      if (have.has("thread_drafts")) {
        for (const row of db.query("SELECT id, configuration FROM thread_drafts").all()) {
          let cfg = {};
          try {
            cfg = JSON.parse(row.configuration) || {};
          } catch (e) {}
          entry.threads.push({
            id: row.id,
            title: cfg.title || "",
            status: cfg.status || "open",
            draft: 1,
            project_path: cfg.projectPath || p.projectPath,
            created_at: cfg.createdAt || 0,
            updated_at: cfg.updatedAt || 0
          });
        }
      }
      result.projects.push(entry);
    } catch (e) {
      result.errors.push({ dir: p.dir, error: String((e && e.message) || e) });
    } finally {
      try {
        if (db) db.close();
      } catch (e) {}
    }
  }
  console.log(JSON.stringify(result));
} else {
  const ids = argJson(process.argv[4], []);
  const done = [];
  const failed = [];
  const left = new Set(ids);
  for (const p of projects) {
    if (!left.size) break;
    let db = null;
    try {
      db = open(p.db, false);
      const have = tables(db);
      if (!have.has("threads") && !have.has("thread_drafts")) continue;
      for (const id of Array.from(left)) {
        let n = 0;
        try {
          if (have.has("threads")) {
            const r1 = db.query("DELETE FROM threads WHERE id = ?").run(id);
            n += (r1 && r1.changes) || 0;
          }
          if (have.has("thread_drafts")) {
            const r2 = db.query("DELETE FROM thread_drafts WHERE id = ?").run(id);
            n += (r2 && r2.changes) || 0;
          }
        } catch (e) {
          failed.push({ id: id, error: String((e && e.message) || e) });
          left.delete(id);
          continue;
        }
        if (n > 0) {
          done.push(id);
          left.delete(id);
        }
      }
    } catch (e) {
      failed.push({ dir: p.dir, error: String((e && e.message) || e) });
    } finally {
      try {
        if (db) db.close();
      } catch (e) {}
    }
  }
  for (const id of left) failed.push({ id: id, error: "没有找到这条会话所在的库" });
  console.log(JSON.stringify({ deleted: done, failed: failed }));
}
