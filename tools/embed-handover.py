#!/usr/bin/env python3
# 把仓库里的 JS 以 Base64 内嵌进 FreebuffController.cs（幂等，随时可重跑）：
#   handover-merge.js → HandoverMergeJsB64（会话接力，控制台用）
#   tools/threaddb.js → ThreadDbJsB64（会话库直读/直删）
# build.bat / release.sh 在编译前自动调用；改了 JS 后重新编译即可生效。
# 按字节读写，保持 C# 源文件原有的 CRLF 行尾不动。
import base64
import os
import re
import sys

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.dirname(here)
cs_path = os.path.join(root, "FreebuffController.cs")

targets = [
    ("handover-merge.js", "HandoverMergeJsB64", os.path.join(root, "handover-merge.js")),
    ("tools/threaddb.js", "ThreadDbJsB64", os.path.join(here, "threaddb.js")),
]

with open(cs_path, "rb") as f:
    cs = f.read().decode("utf-8")

for label, const, js_path in targets:
    with open(js_path, "rb") as f:
        b64 = base64.b64encode(f.read()).decode("ascii")
    pattern = re.compile(r'(' + const + r' = ")(?:[^"]*)(")')
    cs, n = pattern.subn(lambda m: m.group(1) + b64 + m.group(2), cs)
    if n == 0:
        print("ERROR: FreebuffController.cs 里没找到 %s 常量" % const, file=sys.stderr)
        sys.exit(1)
    print("embedded %s (%d bytes -> base64 %d chars)" % (label, os.path.getsize(js_path), len(b64)))

with open(cs_path, "wb") as f:
    f.write(cs.encode("utf-8"))
