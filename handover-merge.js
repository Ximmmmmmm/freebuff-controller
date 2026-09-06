// Freebuff 多开控制器 — 会话接力合并脚本。
//
// 用 Freebuff 自带的 resources/bun/bun.exe 运行：bun:sqlite 直读两个实例的
// desktop-v2.db，把选定会话（threads + messages + queue_items +
// auto_run_decision_receipts + thread_deliveries）从来源库复制进目标库。
// 控制器自身保持无 SQLite 依赖的单文件 exe。
//
// 用法：
//   bun handover-merge.js list  <srcDb>
//   bun handover-merge.js merge <srcDb> <dstDb> <idsJson|@ids.json> [renamesJson|@renames.json]
// ids/renames 直接传 JSON 或传 "@路径"（控制器走文件，避开命令行转义）。
// 输出一行 JSON（UTF-8，stdout）：
//   {"ok":true,"action":"list","threads":[...]}
//   {"ok":true,"action":"merge","copied":[...],"skipped":[...]}
//   {"ok":false,"error":"..."}
// 任何路径异常都走 ok:false；merge 在单事务里完成，失败即整体回滚。
//
// 复制规则：
// - 幂等：目标库已有的 thread id 一律跳过，绝不覆盖。
// - 工作区解耦：thread 指向目标库中同一 root_path 的 projects 行（缺失时
//   自动创建，这是会话外键 project_id 的归属），来源/目标打开哪个工作区
//   互不影响。
// - 引擎私有状态清零（turn_state / harness_state / auto_run 账本 /
//   sponsored 令牌 / freebuff_instance_id / attention 未读 / world_snapshot），
//   接过去的账号从干净的「空闲」会话继续，不背上一账号的运行时欠账。
// - 列白名单：所有 INSERT 只写目标库真实存在的列（PRAGMA 交集），
//   Freebuff 版本更替增删列时不会拼出坏 SQL；目标库自身的列迁移交给
//   orchestrator 启动时的 upgrade 流程。

var Database = globalThis.Database || require("bun:sqlite").Database;

function out(obj) {
  process.stdout.write(JSON.stringify(obj) + "\n");
}

// argv[i]：内联 JSON，或 "@file"（读文件里的 JSON）。
function argJson(i, fallback) {
  var v = process.argv[i];
  if (!v) return fallback;
  if (v.charCodeAt(0) === 64) {
    var fs = require("fs");
    return JSON.parse(fs.readFileSync(v.slice(1), "utf8"));
  }
  return JSON.parse(v);
}

function die(msg) {
  out({ ok: false, error: String(msg) });
  process.exit(0); // 控制器只解析 stdout JSON，退出码无意义
}

// 只读打开；万一 bun 的选项名对不上，退回普通打开（文件仍可读）。
function openRO(path) {
  try {
    return new Database(path, { readonly: true });
  } catch (e) {
    return new Database(path);
  }
}

function tableCols(db, table) {
  return db.query("PRAGMA table_info(" + table + ")").all().map(function (c) {
    return c.name;
  });
}

// 把 rowObj 收窄到 dstCols 里存在的列后 INSERT OR IGNORE。
function insertRow(db, table, rowObj, dstCols) {
  var cols = [];
  var params = {};
  for (var k in rowObj) {
    if (dstCols.indexOf(k) < 0) continue;
    cols.push(k);
    params["$" + k] = rowObj[k];
  }
  if (cols.length === 0) return;
  var q = "INSERT OR IGNORE INTO " + table + " (" + cols.join(", ") +
    ") VALUES (" + cols.map(function (c) { return "$" + c; }).join(", ") + ")";
  db.query(q).run(params);
}

function listThreads(srcPath) {
  var src = openRO(srcPath);
  try {
    var counts = {};
    var mc = src.query(
      "SELECT thread_id, COUNT(*) AS n FROM messages GROUP BY thread_id"
    );
    for (var r of mc.all()) counts[r.thread_id] = r.n;
    var threads = [];
    var rows = src.query(
      "SELECT id, title, status, turn_state, model, project_path, updated_at" +
      " FROM threads ORDER BY updated_at DESC"
    ).all();
    for (var t of rows) {
      threads.push({
        id: t.id,
        title: t.title,
        status: t.status,
        turnState: t.turn_state,
        model: t.model,
        projectPath: t.project_path,
        messages: counts[t.id] || 0,
        updated: t.updated_at,
      });
    }
    out({ ok: true, action: "list", threads: threads });
  } finally {
    src.close();
  }
}

// 确保目标库存在 root_path 对应的 projects 行并返回其 id。正常情况下目标
// 实例自己打开过同一个工作区、行已存在；缺失时补一行（优先沿用来源的
// project_id——它由路径派生，同一台机器上不会变；id 撞车时换随机 id）。
function ensureProject(dst, rootPath, preferredId) {
  var found = dst
    .query("SELECT id FROM projects WHERE root_path = $p")
    .get({ $p: rootPath });
  if (found) return found.id;
  if (preferredId) {
    try {
      dst.query(
        "INSERT OR IGNORE INTO projects (id, root_path, default_branch, created_at)" +
        " VALUES ($id, $rp, $db, $ca)"
      ).run({ $id: preferredId, $rp: rootPath, $db: "main", $ca: Date.now() });
    } catch (e) { }
    found = dst
      .query("SELECT id FROM projects WHERE root_path = $p")
      .get({ $p: rootPath });
    if (found) return found.id;
  }
  var nid = crypto.randomUUID();
  dst.query(
    "INSERT INTO projects (id, root_path, default_branch, created_at)" +
    " VALUES ($id, $rp, $db, $ca)"
  ).run({ $id: nid, $rp: rootPath, $db: "main", $ca: Date.now() });
  return nid;
}

function mergeThreads(srcPath, dstPath, ids, renames) {
  if (!Array.isArray(ids) || ids.length === 0) die("没有要接力的会话");
  if (!dstPath || dstPath === srcPath) die("目标库缺失或与来源相同");
  if (!renames || typeof renames !== "object") renames = {};

  var src = openRO(srcPath);
  var dst = new Database(dstPath);
  var copied = [];
  var skipped = [];
  try {
    var srcThreadCols = tableCols(src, "threads");
    var dstThreadCols = tableCols(dst, "threads");
    var dstMsgCols = tableCols(dst, "messages");
    var dstQueueCols = tableCols(dst, "queue_items");
    var dstReceiptCols = tableCols(dst, "auto_run_decision_receipts");
    var dstDelivCols = tableCols(dst, "thread_deliveries");

    var projCache = {};
    var dstThreadStmt = null; // 每行列集可能不同，逐行构建

    dst.transaction(function () {
      for (var id of ids) {
        var th = src
          .query("SELECT * FROM threads WHERE id = $id")
          .get({ $id: id });
        if (!th) {
          skipped.push(id);
          continue;
        }
        var exists = dst
          .query("SELECT 1 FROM threads WHERE id = $id")
          .get({ $id: id });
        if (exists) {
          skipped.push(id); // 幂等：同 id 会话绝不覆盖
          continue;
        }

        var row = {};
        for (var col of srcThreadCols) row[col] = th[col];

        // 引擎私有状态清零；目标库没有对应列时 insertRow 会自动丢弃。
        row.project_id = ensureProject(dst, th.project_path, th.project_id);
        row.turn_state = "idle";
        row.queue_paused = 0;
        row.auto_run = 0;
        row.auto_run_started_at = null;
        row.auto_run_pass_count = 0;
        row.auto_run_refinement_count = 0;
        row.auto_run_decision_count = 0;
        row.auto_run_stopped_note = null;
        row.auto_run_stopped_at = null;
        row.harness_state = null;
        row.harness_state_id = null;
        row.world_snapshot = null;
        row.freebuff_instance_id = null;
        row.sponsored = null;
        row.sponsored_run_token = null;
        row.sponsored_settled_at = null;
        row.sponsored_terminal_reports = null;
        row.sponsored_terminal_ack_at = null;
        row.pending_briefs = null;
        row.pending_briefs_diagnostic_key = null;
        row.attention_acknowledged_revision = row.attention_revision || 0;
        row.attention_reason = null;
        row.attention_at = null;
        row.last_turn_outcome = null;
        if (renames[id]) row.title = String(renames[id]).slice(0, 200);
        row.updated_at = Date.now();

        insertRow(dst, "threads", row, dstThreadCols);
        copied.push(id);

        for (var m of src
          .query("SELECT * FROM messages WHERE thread_id = $id ORDER BY seq")
          .all({ $id: id })) {
          insertRow(dst, "messages", m, dstMsgCols);
        }

        for (var qi of src
          .query("SELECT * FROM queue_items WHERE thread_id = $id")
          .all({ $id: id })) {
          var st = String(qi.state || "").toLowerCase();
          if (st === "running" || st === "claimed") continue; // 上一账号的运行时残留
          insertRow(dst, "queue_items", qi, dstQueueCols);
        }

        for (var rc of src
          .query(
            "SELECT * FROM auto_run_decision_receipts WHERE thread_id = $id"
          )
          .all({ $id: id })) {
          insertRow(dst, "auto_run_decision_receipts", rc, dstReceiptCols);
        }

        for (var dv of src
          .query("SELECT * FROM thread_deliveries WHERE thread_id = $id")
          .all({ $id: id })) {
          insertRow(dst, "thread_deliveries", dv, dstDelivCols);
        }
      }
    })();
  } finally {
    try { src.close(); } catch (e) { }
    try { dst.close(); } catch (e) { }
  }
  out({ ok: true, action: "merge", copied: copied, skipped: skipped });
}

try {
  var mode = process.argv[2];
  if (mode === "list") {
    if (!process.argv[3]) die("缺少来源库路径");
    listThreads(process.argv[3]);
  } else if (mode === "merge") {
    if (!process.argv[3] || !process.argv[4]) die("缺少来源/目标库路径");
    mergeThreads(
      process.argv[3],
      process.argv[4],
      argJson(5, []),
      argJson(6, {})
    );
  } else {
    die("unknown mode: " + mode);
  }
} catch (e) {
  die(e && e.message ? e.message : String(e));
}
