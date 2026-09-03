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

if gh release view "${TAG}" -R "${REPO}" >/dev/null 2>&1; then
  echo "ERROR: Release ${TAG} 已存在。升 FreebuffController.cs 里的 AssemblyVersion 再发新版，" >&2
  echo "  或先 gh release delete ${TAG} -R ${REPO} --yes。" >&2
  exit 1
fi

gh release create "${TAG}" "${HERE}/FreebuffController.exe" -R "${REPO}" \
  --title "Freebuff 多开控制器 v${VER}" \
  --notes "单文件 exe（约 56 KB，无运行时依赖），下载即用；多实例独立数据目录 + 独立账号 + 汉化包自动更新 + 代理接入。详见 README。"
echo "已发布 ${TAG}。"
