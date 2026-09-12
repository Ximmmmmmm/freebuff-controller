#!/usr/bin/env bash
# 一次性实测「控制器自动恢复汉化」——把装机还原成英文 → 你打开控制器（或点「启动」）→
# 本脚本核对汉化是否在 Freebuff 拉起前被自动换回来，并检查英文备份链没被污染。
#
# 用法（先把 Freebuff 完全退出，再运行）：
#   bash tools/verify-autorestore.sh            # 手动：你在控制器里点「启动」
#   bash tools/verify-autorestore.sh --auto     # 全自动：脚本自己启动控制器（v1.8.17+
#                                               #   一打开控制器就会自动恢复），无需任何点击
#
# 为什么要退出应用：自动恢复的第一条闸门就是「没有任何实例在运行」。
# --auto 模式把整个流程变成一条命令，适合没人盯屏时跑（结果也会写进日志）。
set -euo pipefail

AUTO=0
case "${1:-}" in
  --auto) AUTO=1 ;;
  "") ;;
  *) echo "用法：bash tools/verify-autorestore.sh [--auto]" >&2; exit 2 ;;
esac

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# 结果同时落到 verify-autorestore.log：实测要求先完全退出 Freebuff（含当前这个客户端）
# 才能跑，跑完常常已经看不到终端了。VERIFY_NOLOG=1 可关掉这份副本。
if [ -z "${VERIFY_NOLOG:-}" ]; then
  exec > >(tee -a "${HERE}/verify-autorestore.log") 2>&1
fi
INSTALL="${LOCALAPPDATA:-}/Programs/@codebufffreebuff-desktop"
R="${INSTALL}/resources"
UI="${R}/orchestrator/ui"
CTRL="${HERE}/FreebuffController.exe"

fail() { echo "ERROR: $*" >&2; sleep 0.3 2>/dev/null || true; exit 1; }  # 等一下 tee，别把日志截尾
sha() { sha256sum "$1" 2>/dev/null | cut -d' ' -f1; }

# --- 0. 前置：应用必须全部退出（这同时也是自动恢复的第一条闸门）----------------
if tasklist //FI "IMAGENAME eq Freebuff.exe" 2>/dev/null | grep -q 'Freebuff\.exe'; then
  fail "检测到 Freebuff 仍在运行。先完全退出它（含多开实例）再跑本脚本。
  提示：自动恢复只在「没有任何实例在运行」时动手，开着应用跑本脚本必然测不出来。"
fi
[ -f "${UI}/index.html" ] || fail "找不到装机 UI：${UI}/index.html"
[ -f "${CTRL}" ] || fail "找不到控制器 exe：${CTRL}（先在控制器仓库跑 bash build.bat）"

# 控制器版本：低于 1.8.4 的 exe 没有自动恢复功能
CTRL_VER="$(powershell -NoProfile -Command "(Get-Item '${CTRL}').VersionInfo.FileVersion" 2>/dev/null | tr -d '\r' || true)"
echo "控制器：${CTRL}  版本 ${CTRL_VER:-未知}"

# --- 定位汉化仓库（与控制器同一套探测顺序：同级/上一级的 hanhua 或 freebuff-zh）----
HANHUA=""
for p in "${HERE}/hanhua" "${HERE}/../hanhua" "${HERE}/freebuff-zh" "${HERE}/../freebuff-zh"; do
  [ -f "${p}/dict.json" ] && { HANHUA="$(cd "$p" && pwd)"; break; }
done
[ -n "${HANHUA}" ] || fail "没找到汉化仓库（含 dict.json 的 hanhua/ 或 freebuff-zh/）。"
echo "汉化仓库：${HANHUA}"
[ -f "${HANHUA}/output/app.asar" ] || fail "${HANHUA}/output 缺少构建产物，先跑 bash build.sh。"

# --- 1. 记录基线，然后还原成英文原版 ------------------------------------------
echo
echo "== 1/3 还原成英文原版 =="
BK_BEFORE="$(ls -1dt "${R}"/hanhua-backup-* 2>/dev/null | head -1 || true)"
[ -n "${BK_BEFORE}" ] || fail "没有英文原版备份（resources/hanhua-backup-*），无从还原。"
BK_ASAR_BEFORE="$(sha "${BK_BEFORE}/app.asar")"
BK_IDX_BEFORE="$(sha "${BK_BEFORE}/ui/index.html")"
OUT_ASAR="$(sha "${HANHUA}/output/app.asar")"
OUT_IDX="$(sha "${HANHUA}/output/ui/index.html")"
echo "  基线备份：$(basename "${BK_BEFORE}")  app.asar ${BK_ASAR_BEFORE:0:16}…  index.html ${BK_IDX_BEFORE:0:16}…"
echo "  待装产物：output/  app.asar ${OUT_ASAR:0:16}…  index.html ${OUT_IDX:0:16}…"

(cd "${HANHUA}" && bash restore.sh)
grep -q '<html lang="en"' "${UI}/index.html" || fail "装机没有变成英文（restore.sh 似乎没生效）。"
echo "  ✓ 装机已是英文原版（<html lang=\"en\">）"

# --- 2. 触发 ---------------------------------------------------------------------
if [ "${AUTO}" -eq 1 ]; then
  echo
  echo "== 2/3 自动启动控制器（v1.8.17+ 打开即自动恢复，无需点击） =="
  # 控制器是多开管理器，不是 Freebuff 实例：它自己起来不违反上面那条闸门。
  cmd //c start "" "$(cygpath -w "${CTRL}")" || fail "启动控制器失败。"
  echo "  已启动 $(basename "${CTRL}")（版本 ${CTRL_VER:-未知}）"
  echo "  等它完成启动流程：约 10 秒后开始核对装机状态…"
  sleep 10
else
  cat <<'TIP'

== 2/3 该你了：打开控制器（或选中第一行点「启动」） ==
   控制器一打开就会自动恢复：状态行先出现「检测到汉化未应用 · 正在自动恢复…（控制器启动）」
   随后是「已自动恢复汉化 ✓ 下次打开 Freebuff 就是中文。」
   点「启动」走的是同一条路（括号里那句会变成「启动前」，第一次按 Enter 不必等它）。
   弹出的 Freebuff 窗口应当直接是中文界面。

   跑完回到这里按 Enter（不按也行：脚本最多等 5 分钟，检测到汉化就继续）。
TIP
  if [ -t 0 ]; then read -r _ || true; fi
fi

# --- 3. 核对 ---------------------------------------------------------------------
echo "== 3/3 核对结果 =="
PASS=1
for i in $(seq 1 300); do
  grep -q '<html lang="zh-CN"' "${UI}/index.html" 2>/dev/null && break
  [ $((i % 20)) -eq 0 ] && echo "  … 仍在等待自动恢复（${i}s）"
  sleep 1
done

if grep -q '<html lang="zh-CN"' "${UI}/index.html"; then
  echo "  ✓ 装机已回到中文（<html lang=\"zh-CN\">）"
else
  echo "  ✗ 5 分钟内装机仍是英文：自动恢复没有发生"
  echo "    可能原因：控制器版本低于 1.8.4 / 未找到汉化仓库 / 输出目录不是给当前版本的构建 / 有实例正在运行"
  PASS=0
fi

PACK="$(grep -o 'name="hanhua-pack" content="[^"]*"' "${UI}/index.html" | head -1 || true)"
echo "  装机 pack 版本戳：${PACK:-（无）}"

# 注意：Windows 路径带盘符冒号，不能用 ":" 分隔参数，一律分开传
check_same() { # 名称 装机文件 期望哈希
  local name="$1" file="$2" want="$3" got
  got="$(sha "${file}")"
  if [ "${got}" = "${want}" ]; then
    echo "  ✓ 装机 ${name} 与 output/ 逐字节一致（${got:0:16}…）"
  else
    echo "  ✗ 装机 ${name} 与 output/ 不一致（装机 ${got:0:16}… vs output ${want:0:16}…）"
    PASS=0
  fi
}
check_same "app.asar" "${R}/app.asar" "${OUT_ASAR}"
check_same "ui/index.html" "${UI}/index.html" "${OUT_IDX}"

echo "  备份链检查："
while IFS= read -r d; do
  lang="$(grep -o 'lang="[^"]*"' "${d}/ui/index.html" 2>/dev/null | head -1 || true)"
  a="$(sha "${d}/app.asar")"; i2="$(sha "${d}/ui/index.html")"
  if [ "${d}" = "${BK_BEFORE}" ]; then
    if [ "${a}" = "${BK_ASAR_BEFORE}" ] && [ "${i2}" = "${BK_IDX_BEFORE}" ]; then
      echo "    ✓ $(basename "${d}") 未被改动（原来那份英文原版）"
    else
      echo "    ✗ $(basename "${d}") 被改写了！备份链被污染"
      PASS=0
    fi
  else
    if [ "${lang}" = 'lang="en"' ]; then
      echo "    ✓ $(basename "${d}") 是新增的纯英文备份（换文件前装机确实是英文，属正常）"
    else
      echo "    ✗ $(basename "${d}") 不是英文备份（${lang:-未知}）——备份链被污染"
      PASS=0
    fi
  fi
done < <(ls -1dt "${R}"/hanhua-backup-* 2>/dev/null)

echo
if [ "${PASS}" -eq 1 ]; then
  echo "结论：✅ 自动恢复汉化实测通过 —— 汉化在 Freebuff 拉起前被自动换回，英文备份链干净。"
  echo "（本次记录：${HERE}/verify-autorestore.log）"
  sleep 0.3 2>/dev/null || true
else
  echo "结论：❌ 实测未通过（明细见上）。回退办法：cd ${HANHUA} && bash apply.sh（控制器里的手动按钮已下线）"
  exit 1
fi
