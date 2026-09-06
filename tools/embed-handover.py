#!/usr/bin/env python3
# 把 handover-merge.js 以 Base64 内嵌进 FreebuffController.cs：
# 替换 HandoverMergeJsB64 常量引号里的内容（幂等，随时可重跑）。
# build.bat / release.sh 在编译前自动调用；改了 JS 后重新编译即可生效。
# 按字节读写，保持 C# 源文件原有的 CRLF 行尾不动。
import base64
import os
import re
import sys

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.dirname(here)
cs_path = os.path.join(root, "FreebuffController.cs")
js_path = os.path.join(root, "handover-merge.js")

with open(js_path, "rb") as f:
    b64 = base64.b64encode(f.read()).decode("ascii")

with open(cs_path, "rb") as f:
    cs = f.read().decode("utf-8")

pattern = re.compile(r'(HandoverMergeJsB64 = ")(?:[^"]*)(")')
new_cs, n = pattern.subn(lambda m: m.group(1) + b64 + m.group(2), cs)
if n == 0:
    print("ERROR: FreebuffController.cs 里没找到 HandoverMergeJsB64 常量", file=sys.stderr)
    sys.exit(1)

if new_cs != cs:
    with open(cs_path, "wb") as f:
        f.write(new_cs.encode("utf-8"))
print("embedded handover-merge.js (%d bytes -> base64 %d chars)" % (
    os.path.getsize(js_path), len(b64)))
