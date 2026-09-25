#!/usr/bin/env bash
# 编译并以 exe 的 FileVersion 打 tag，发布控制器 Release（单文件 exe 资产）。
# 升版本 = 改 FreebuffController.cs 里的 AssemblyVersion，再跑本脚本。
#
# 用法：bash release.sh          # 需要 gh CLI 已登录
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="Ximmmmmmm/freebuff-controller"
WINHERE="$(cygpath -w "$HERE")"

cd "$HERE"
# 会话接力脚本以 Base64 内嵌进 exe，编译前先刷新（优先 python3，
# Windows 上常只有 python，fallback 过去）。
python3 tools/embed-handover.py 2>/dev/null || python tools/embed-handover.py || \
  { echo "EMBED FAILED（需要 python3）" >&2; exit 1; }
# Build the single-file exe directly with csc. Avoid `cmd //c build.bat`,
# which git-bash can't invoke when a sandbox blocks cmd.
CSC="${SYSTEMROOT:-C:\Windows}/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
"$CSC" -nologo -target:winexe -platform:anycpu -optimize+ -codepage:65001 \
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Management.dll \
  -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll \
  -win32icon:"app.ico" -out:"FreebuffController.exe" "FreebuffController.cs" \
  || { echo "BUILD FAILED（csc 编译出错，详见上方）" >&2; exit 1; }
echo "built FreebuffController.exe"

VER="$(powershell -NoProfile -Command "(Get-Item '${WINHERE}\\FreebuffController.exe').VersionInfo.FileVersion" | tr -d '\r' | sed 's/\.[0-9]*$//')"
TAG="v${VER}"
echo "FreebuffController.exe v${VER} → Release ${TAG}"

# SHA512 digest alongside the exe (sha512sum-style "<hex>  <file>" lines).
# OnSelfUpdateClick fetches this to verify the download before swapping the
# running exe; without it the self-update silently skips verification.
if command -v sha512sum >/dev/null 2>&1; then
  sha512sum FreebuffController.exe > sha512.txt
else
  HEX="$(powershell -NoProfile -Command "(Get-FileHash -Algorithm SHA512 '${WINHERE}\\FreebuffController.exe').Hash.ToLower()" | tr -d '\r')"
  printf '%s  FreebuffController.exe\n' "$HEX" > sha512.txt
fi

if gh release view "${TAG}" -R "${REPO}" >/dev/null 2>&1; then
  echo "ERROR: Release ${TAG} 已存在。升 FreebuffController.cs 里的 AssemblyVersion 再发新版，" >&2
  echo "  或先 gh release delete ${TAG} -R ${REPO} --yes。" >&2
  exit 1
fi

# Release 说明取自 CHANGELOG.md 里本版那一节（## vX.Y.Z 到下一个 ## 之间）。
# 没那一节就直接中止：Release 页是给用户看的，宁可现在补一行，也不让它变成空话/旧话。
NOTES="$(awk -v ver="${TAG}" '
  $0 ~ ("^## " ver "([ ·]|$)") { found = 1; next }
  found && /^## / { exit }
  found { print }
' CHANGELOG.md)"
if [ -z "$(printf '%s' "${NOTES}" | tr -d '[:space:]')" ]; then
  echo "ERROR: CHANGELOG.md 里没有 ${TAG} 一节，无法生成 Release 说明。" >&2
  echo "  按时间倒序在顶部补一节（改了什么 + 对用户的影响），标题写成「## ${TAG} · $(date +%F)」再重跑。" >&2
  exit 1
fi

gh release create "${TAG}" "${HERE}/FreebuffController.exe" "${HERE}/sha512.txt" -R "${REPO}" \
  --title "Freebuff 多开控制器 v${VER}" \
  --notes "${NOTES}"
echo "已发布 ${TAG}。"
