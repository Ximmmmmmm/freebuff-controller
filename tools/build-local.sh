#!/usr/bin/env bash
# 只编译、不发布的构建脚本：release.sh 的编译段独立出来，方便本机验证新代码。
# 用法：bash tools/build-local.sh [输出路径]   （默认输出到仓库根的 FreebuffController.exe）
#   环境变量 SKIP_EMBED=1 跳过 handover-merge.js 内嵌刷新（已刷新过时可跳）。
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$HERE"
OUT="${1:-FreebuffController.exe}"

if [ "${SKIP_EMBED:-0}" != "1" ]; then
  python3 tools/embed-handover.py 2>/dev/null || python tools/embed-handover.py || \
    { echo "EMBED FAILED（需要 python3 或 python）" >&2; exit 1; }
fi

CSC="${SYSTEMROOT:-C:\Windows}/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
"$CSC" -nologo -target:winexe -platform:anycpu -optimize+ -codepage:65001 \
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Management.dll \
  -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll \
  -win32icon:"app.ico" -out:"$OUT" "FreebuffController.cs"
echo "built $OUT"
