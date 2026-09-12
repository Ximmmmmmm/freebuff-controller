# Freebuff 多开控制器 / Freebuff Multi-Instance Controller

一个 Windows 桌面小工具：让 [Freebuff](https://www.freebuff.com) 桌面版支持**多开**，并且**每个窗口可以登录不同的账号**。

A small Windows utility that lets the Freebuff desktop app run multiple instances simultaneously — each with its own independent account.

> 纯第三方工具，不修改 Freebuff 本体，与 Freebuff 官方无关。
> A third-party tool. It does not modify the Freebuff app itself.

## 功能 / Features

- **多开**：主实例 + 实例 1~9，互不干扰
- **多账号**：每个实例独立 Chromium 配置和登录态，可分别登录不同账号
- **状态一览**：实时显示每个实例的运行状态和当前登录的账号邮箱
- **额度显示**：每个账号的 **Freebucks** 额度（v0.0.88+ 起的新模型）——今日剩余 / 总额度，钱包有余额时附带显示；悬停可见剩余量、钱包余额、最便宜模型每小时单价与今日额度补充时间（太平洋每日 0 点）。请求会话接口时带 `x-freebuff-multi-session` / `x-freebuff-include-unused-rate-limits` 两个头（与桌面端 orchestrator 一致），否则服务器不返回 Freebucks。旧版服务端无 Freebucks 时回退为各模型中最紧的剩余额度。启动时与手动刷新时更新，另有 5 分钟自动刷新
- **额度模型说明**：v0.0.88 起 Freebuff 不再有「每周 / 每月的会话上限」，旧的日/周/月窗口（`freeWindows`）已退役，本工具不再显示它们
- **版本检测**：显示本机 Freebuff 版本，自动对比官方更新源（**窗口里没有常驻的版本文字**——只有真有事时，按钮下方才浮出一行小字（8 秒后自动消失，平时完全空白）；手动查一次点窗口顶部的「检查更新」，查出已是新版就直接下载，「已是最新」那句话也只在那一行闪一下；启动时与每 30 分钟的自动检查静默，但发现新版那行会浮出来提醒（同时弹一个气泡）；发现新版本可一键下载官方安装包——必须取到更新源的 `latest.yml`（文件名 + SHA512；缺任一项或校验值非法就直接中止，不运行没校验的包），下载到 `%TEMP%` 校验通过后立即运行，下载进度就显示在那一行（也同步到托盘悬停提示）、完成 / 失败另外弹气泡。**装完自动删包**：检测到装机版本追平下载时的目标版本后，把这份安装包删掉并在那一行报一句释放了多少（删不掉——安装器还占着文件——就留到下一轮检查再试）；官方 electron-updater 自己攒的缓存也在「装完」那一刻一起清（见「无用安装包清理」）
- **控制器自更新**：控制器启动时与每 30 分钟会检查本仓库（Ximmmmmmm/freebuff-controller）的最新 Release tag，比自己新时右上角出现「自更新」入口——一键下载新 exe（暂存为 `*.new-v版本.exe`，SHA512 校验当 Release 附带 `sha512.txt` 时启用），退出控制器后由 `self-update.cmd` 脚本自动替换并重启；下载失败再点一次会打开 Release 页面手动下载。**这条路以前其实走不通，v1.8.20/v1.8.21 才修好**：线上 `sha512.txt` 是 sha512sum 的二进制格式（`<hex> *FreebuffController.exe`），而旧版校验只拿裸文件名去比对，永远不命中、每次都停在「Release 缺少有效 SHA512」；即使过了这关，替换脚本的重试间隔用的是 `timeout`，它在无控制台（GUI 进程 `CreateNoWindow` 拉起）环境下会立刻返回 rc=125（实测 0.02 秒），号称「重试 60 次」实际约 8 秒就放弃 —— 旧进程还没退利索就只能报「替换失败」。现在认 `*` / `\` 前缀，延时改 `ping`（同样环境实测会真等），实测目标文件被占用 8 秒时能等到释放后成功替换。**v1.8.22 起替换成功后还会**：把旧 exe 备份成 `*.bak-v旧版本.exe`（同目录只留一份、可回滚），并写 `updated-to.txt` 标记——重启后的控制器读到它，就把「已升级到 vX ✓（旧版已备份为 …）」浮到状态行（8 秒后回落）并删掉标记；失败路径什么都不动，只留新 exe 等手动改名
- **网络路径**：检查更新、下载安装包、额度查询按「本地代理 → 系统代理 → 直连」依次尝试，并**记住上次成功的路由优先重试**。本地代理**自动探测**常见端口（7890 / 7897 / 10808 / 10809 / 1080），且为功能级探测——经代理真实请求 204 端点确认可用，SOCKS-only 端口不会误判（`socks5://` 地址仅用于启动的实例，控制器自身请求会自动跳过）。启动 Freebuff 实例同样接入：代理在运行时为实例加 `--proxy-server`（覆盖界面与内置更新器的流量）并注入 `HTTP(S)_PROXY` 环境变量（后端 orchestrator 若读取则同样走代理；回环地址始终直连），代理未运行或 `off` 时按原样启动，实例自行回落系统代理（**v1.8.19 修**：槽位实例以前会拿到一个空的 `--proxy-server=` 与空 `HTTP_PROXY`/`HTTPS_PROXY`——那与「不加参数」不是一回事，空值会被当成一份非法／空白的代理配置；现在与主实例一致，代理不可用就真的一个参数都不加）。主窗口右上角「代理设置」：实时状态、逐端口探测结果（✓ = 端口可达且功能探测通过）、修改地址、停用（off）、恢复默认——与手编 `%APPDATA%\FreebuffController\proxy.txt` 等价，保存立即生效。**实例掉线会主动告诉你**（v1.8.17 起）：控制器自己的请求有候选链兜底，但已经启动的实例换不了代理（`--proxy-server` 是启动参数，代理客户端一重启/换端口，实例里就静默没网了）。所以每 60 秒对每个实例还在用的代理做一次存活检查（TCP 先探，非 SOCKS 再补一次功能探测，僵死代理也能识破），一旦从可用变不可用就弹气泡说清「哪个实例 + 哪个地址 + 重启它才能重新接入」，恢复后重新布防（下次再掉会再报）；实例关掉就不再提醒。manual 模式下配置的代理挂了也会报一次（控制器请求会自动回落系统代理 / 直连，无需操作）
- **无用安装包自动清理**：两处一起收。Freebuff 官方更新器（electron-updater）把下好的安装包攒在 `%LOCALAPPDATA%\@codebufffreebuff-desktop-updater`，实测约 300 MB（安装器本体 + `pending/` 里等待安装的那一份）；控制器自己从更新源下载的安装包落在 `%TEMP%`（**只跟踪最近一份**，更早下过又没装成的会变成孤儿，实测一台机器上攒了 17 个、共 2.16 GB）。控制器**启动时**、**每次 Freebuff 更新装完后**（立刻清一次 + 20 秒后复查一次）以及**每 30 分钟**兜底各自动清理一次，只删已经用不上的包（安装包版本 ≤ 当前已装版本），**比已装版本新的 `pending/` 包绝不碰**（那是你已下好、等退出时自动安装的更新），差分下载要用的 `current.blockmap` 也保留；`%TEMP%` 里正被记录、正在下载（2 小时内改动过）的文件同样不碰。窗口里既不常驻缓存行、也**没有「清理缓存」按钮**——v1.8.17 已把那个「手动过目 + 确认框」入口下线，自动清理就是全部入口（清完在按钮下方那行浮一句，没得清时一声不吭）
- **一键操作**：启动 / 停止 / 重置账号 / 停止全部；双击表格行直接启动
- **窗口内入口（不用碰托盘）**：顶部一行从左到右是「汉化状态 / 检查更新 / 代理设置」——
  汉化状态 = 立刻检查有没有适配本机版本的新汉化包（有就自动应用）并把当前状态写在按钮下方那行；
  检查更新 = 手动查 Freebuff 新版（查出就直接下载）。**清理无用安装包没有入口**：它本来就会
  在启动时 / 每 30 分钟 / 每次 Freebuff 更新装完后自动跑（见「无用安装包自动清理」）。
  托盘右键菜单里只剩「退出」（双击托盘图标仍可把窗口叫回来），所有功能都在窗口里点得到
- **会话共享（所有账号共用一份聊天记录，默认开启）**：不再有「共享会话」按钮——控制器启动时（或启动未共享的实例时）自动提示并入，确认后把每个实例的 `projects` 目录变成指向**主实例会话库**的 Windows junction——之后所有窗口读写同一个 `desktop-v2.db`，聊天记录天然只有一份：A 账号额度用完，B 账号的窗口打开同一份历史直接接着聊，**不需要复制、不需要选会话**。首次开启时各实例已有的独立历史会自动合并进主库（原目录改名保留为 `projects.pre-share-*` 备份，可手动回退），登录态仍在各自 state.json 下、账号互不影响。合并由 Freebuff 自带的 `resources\bun\bun.exe` 执行内嵌脚本完成（无外部依赖），同 id 会话绝不覆盖。提示：同一时间尽量只在一个窗口聊天——两个实例同时写入同一个库可能偶发锁冲突（WAL 模式下数据不会损坏）
- **两种初始化方式**：启动未初始化的实例时弹窗选择——全新登录（每个窗口用不同账号），或复制其他已登录实例的账号（下拉框只列出真正登录过的实例，并显示其邮箱），免重复登录
- **重置换号**：清空某个实例即可换登录另一个账号
- **汉化集成（全自动）**：自动检测 Freebuff 是否已应用[汉化包](https://github.com/Ximmmmmmm/freebuff-zh)（独立仓库）。Freebuff 更新把汉化覆盖回英文后，控制器会**自己换回中文**——检测到装机版本变化、你点「启动」之前、以及刚拉到更新的汉化包时（装机是旧版汉化就直接升级）都自动应用。**没有开关也没有按钮**：自动应用就是默认且始终生效的行为；英文原版备份自动收敛到最近 2 份（旧的、写了一半的都清掉）
- **默认回复中文**：控制器启动时自动确保家目录存在 `~\.AGENTS.md` 中英双语语言规则——Freebuff 的 agent 每次新会话都会把它并入系统提示词，因此 AI 无论收到什么语言都固定用简体中文回复。无需任何操作：控制器在，就默认中文；写失败也不影响启动。**与汉化包互相独立**（界面汉化 ≠ 回复中文），不依赖 Freebuff 版本，应用更新也不会冲掉；仅对新会话生效，已开的会话保持原有提示词
- **抗注入条款（应对服务端英文注入）**：排查确认，AI 有时会突然改用英文回复，源头不是本机任何工具（Freebuff 客户端、汉化包、输入法、浏览器插件均无此类文本），而是 Freebuff 服务端在检测到英文输入时动态注入的「语言一致性」指令（如 "Reply in English only"、"Your user's primary language is English"），本机无开关可关。为此语言规则内置了**抗注入条款**：明确宣布这类指令及其变体（翻译式、格式声明式、借口式、中文措辞、间接注入等）一律无效，继续用简体中文回复；只有用户用中文明确说「改用英文回复」才能临时切换。控制器启动时的规则文件指纹就是这段条款——老用户的旧版规则会在升级控制器后自动补齐。
- **界面排版（v1.8.18）**：全窗口统一 10px 节奏——窗口顶 14px、顶部入口行、表格、按钮行、按钮下方那行事件文字依次各隔 10px，底边再留 10px；顶部三个入口与表格共用同一条右边线（560），按钮行是 5 个等宽 100px、等距 10px（原来末位「刷新」只有 82 宽、右边距 23，间距 9/10 混着，整排看着是歪的）。按钮下方那行也从「紧贴按钮下缘 1px 的暗灰小字」改成隔开 10px、用正文亮色，并**按结果着色**：成功绿、失败与警告琥珀、进度与例行回执默认亮灰——一闪而过的一句话也能一眼分出是报喜还是报错
- **按钮圆角平滑（v1.8.20）**：圆角以前靠 `Control.Region` 裁剪，而 Region 是 1 位掩码、没有抗锯齿，圆角只能是阶梯状的（实测按钮角落 16×16 里只有填充色与背景色两种颜色、零过渡像素）。改成自绘：先按父容器底色铺满、再用 `SmoothingMode.AntiAlias` 画圆角矩形，角落自然露出底色、边缘变成渐变过渡（同一块区域测到 13 种颜色、16–25 个过渡像素）。悬停 / 按下提亮、禁用变暗、键盘焦点框都改为自绘保持原样式；文字仍居中（上下留白各 12px 对称）
- 暗色主题 UI，单文件 exe（约 140 KB，无运行时依赖）；最小化进任务栏，关闭即完全退出

## 使用 / Usage

1. 双击 `Freebuff多开控制器.exe`（或按下面步骤自行编译）
2. 双击（或选中后点「启动」）一个未初始化的实例，在弹窗里选「全新登录」
3. 在弹出的 Freebuff 窗口里登录该窗口要用的账号 —— 登录态会固定在这个实例
4. 想换某个实例的账号：选中 → 「重置账号」→ 再启动登录新账号
5. 所有实例共用一份聊天记录：启动控制器（或启动未共享的实例）时自动提示并入 → 确认 → 各实例历史并入主库，之后任何账号的窗口打开的都是同一份会话（见上方「会话共享」）

## 原理 / How it works

Freebuff 是 Electron 应用，用 `requestSingleInstanceLock()` 限制单开。本工具**不修改任何应用文件**，而是给每个实例分配独立的数据目录：

- `--user-data-dir=<APPDATA>\Freebuff-slot-N` — 独立 Chromium 配置文件与单实例锁
- `FREEBUFF_DESKTOP_STATE_PATH=<user>\.config\freebuff-desktop\slots\slot-N\state.json` — 独立后端 orchestrator 状态（避开其 SQLite 文件锁）
- `~\.configreebuff-desktop\slots\slot-N\projects\<工作区>\desktop-v2.db` — 本地会话库（聊天会话线程与消息都在这里，与登录态相互独立）

**会话共享原理**：主实例的会话库在 `~\.config\freebuff-desktop\projects\`。默认共享下，每个 slot 的 `projects` 目录被替换成指向它的 junction（`mklink /J`，不需要管理员权限），于是所有实例读写同一个 `desktop-v2.db`，聊天记录天然共享；各实例的登录 token 仍在自己的 `state.json` 里，账号互不影响。首次开启会把各实例已有的独立历史合并进主库，原目录改名为 `projects.pre-share-<时间戳>` 保留备份——想回退到独立模式时，关掉全部实例、删掉 junction、把备份目录改回 `projects` 即可

因此 Freebuff 应用升级不会使本工具失效。

The app is an Electron app that enforces a single-instance lock. This tool gives each instance its own `--user-data-dir` and its own orchestrator state file via an environment variable, so instances don't share locks — no app files are touched, and app updates don't break it.

## 汉化集成 / Localization

Freebuff 的自动更新会用原版文件覆盖[汉化包](https://github.com/Ximmmmmmm/freebuff-zh)的产物。汉化状态不再常驻窗口
（底部原来那两行已移除，**平时窗口底部完全空白**；只有事件与进度才在按钮下方浮出一行小字，8 秒后自动消失——
「汉化包更新中 · 下载 40%」这类进度用它报，常驻的「汉化 ✓ · 版本」只走托盘悬停提示），
**没有「应用汉化 / 还原英文」按钮**——换文件全自动（首次应用自动备份英文原版，与 `apply.sh` 的备份/还原机制完全兼容）。
想确认当前状态：鼠标停在托盘图标上看悬停提示，或点窗口顶部的「汉化状态」——它会立刻去检查有没有适配本机 Freebuff 版本的新包（有就自动应用），并把当前状态写在按钮下方那行；换文件失败会弹气泡提醒（成功不打扰）。
窗口顶部那一行就是全部入口：**汉化状态 / 检查更新 / 代理设置**（托盘菜单只剩「退出」，双击托盘图标可把窗口叫回来；清理无用安装包全自动，不在这一行里）。

**自动应用汉化（无开关）**：控制器在五个时机自动换上 `hanhua/output/` 里的构建——
① 每 3 秒的轮询发现装机版本变了（Freebuff 刚自动更新，汉化必然已被覆盖回英文）；
② **你打开控制器时**（多半正是「Freebuff 刚更新完、界面变回英文」的场景；启动时 3 秒轮询
不会因「版本没变」触发，不补这一下就得等点「启动」）；
③ 你点「启动」拉起某个实例之前（先换文件再拉进程，看到的第一眼就是中文）；
④ **本地跑完 `bash build.sh` 之后**：每 3 秒看一眼 `output/`，三处产物（`app.asar`、
`ui/index.html`、`ui/assets` 目录）的 mtime 连续 8 秒不再变化就认这是一个完整的新构建
——稳定判定是必须的，构建中途 `app.asar` 还在写、`assets` 还在拷，半成品装上就是白屏；
⑤ 刚拉到适配当前版本的新汉化包时。
装机是英文就恢复，装机是旧版汉化（同一 Freebuff 版本的修正重发 v0.0.103.1 这类）就直接升级，
两种都不用点按钮。动手前逐条确认：装机不是汉化版**或 `output/` 里的包比装机的更新**、
构建的 `targetVersion` 与装机版本一致、**且没有任何实例在运行**（换文件会打断任务，
有实例在跑就等下一个时机）。不满足就静默跳过，不弹任何对话框。

版本比对按**前三段**做（`0.0.106.1` 这种修正重发标记不算差异，Freebuff 自己从不用第四段）：
本地 `output/` 这条路径拿到的是 `ui/index.html` 里的 packVersion 戳、发布包那条路拿到的是
manifest 的 targetVersion，两条口径现在一致，同版本的修正重发包不会再被误判成「不匹配」而跳过。

> ②④ 与修正重发的版本比对都是 **v1.8.17** 补上的：此前打开控制器不会自动恢复汉化
> （`hanhuaRecheckVersion` 在启动时就被置成当前版本，3 秒轮询不会因「版本没变」触发），
> 本地重新 `build.sh` 也不会被发觉（只能靠点「启动」），带第四段的重发包则会被判成版本不匹配。
没有任何开关可关：这套逻辑始终生效（旧版留下的 `hanhua-auto.txt` 开关文件会在启动时清掉）。

**备份保留策略**：英文原版快照（每份约 40 MB：`app.asar` 28 MB + `ui/` 12 MB）不会无限堆积。
控制器只留最近 2 份**完整**的备份（`app.asar` 与 `ui/index.html` 都在），更旧的一律清掉，
写了一半的（只有 `app.asar`、`ui/` 没写完）也一并清掉——它还原过去只会让两半版本对不上。
清理时机：每次应用/自动恢复之后，以及控制器启动 1.5 秒后（老版本或命令行 `apply.sh` 堆下的
积压不等下一次更新就收拾）。清掉了什么会在托盘提示报一句，例如
「已清理 1 份旧汉化备份（2.9 MB），只保留最近 2 份」；应用/恢复本身有自己的成功文案，
清理提示等它回落后再单独占一行，不抢话。清理只数 `resources\hanhua-backup-*` 这个模式，
不分是谁创建的（手动 `apply.sh` 建的也算），删除失败（被占用）就留着下次再试，
绝不影响应用/恢复本身。保留 2 份而不是 1 份是为了留个余量：回退时永远用最新的那份完整的，
更旧的连版本都对不上——那时装机文件已经是另一个 Freebuff 版本了。

自测：`bash tools/verify-autorestore.sh`（先把 Freebuff 完全退出）——脚本记录基线、跑 `restore.sh`
把装机还原成英文，提示你在控制器里点「启动」，然后自动核对装机是否回到中文、是否与 `output/`
逐字节一致、英文备份链有没有被污染（先退出应用是硬前提，也是自动恢复自己的第一条闸门）。

边界（不做的事）：控制器不检查 Freebuff 是否在跑就直接换文件——它先确认没有实例运行，
所以「更新后一直开着 Freebuff」的机器要等下次启动（或重新打开控制器）才恢复；
用 `restore.sh` 在命令行还原英文，下次启动仍会自动汉化（「选英文」的标记已经彻底没有了）。
本地新构建的自动发现（④）要求 `output/` 落盘后稳定 8 秒，`bash build.sh` 跑完约 10 秒
就会换上；只想看构建结果、先不换文件的话，构建中途别让它稳定下来即可（或用
`restore.sh` 之外的手段自己掌控节奏——这一条只影响「自动换」的时机，不影响点「启动」那条路）。

汉化仓库的查找顺序：

1. exe 同级或上一级的 `hanhua/` 目录（monorepo 布局）
2. exe 同级或上一级的 `freebuff-zh/` 目录（汉化仓库独立克隆）
3. 手编记住的路径 `%APPDATA%\FreebuffController\hanhua-path.txt`（内容就是仓库目录，一行）——窗口里已经没有「选目录」对话框了

控制器还会对比本机 Freebuff 版本与 `hanhua/manifest.json` 的 `targetVersion`：
本机版本更新时提醒先更新词典并重新构建，避免把过时的汉化产物打上去。

汉化包本身也可以作为 GitHub Release 分发（`hanhua/tools/release.sh` 发布）：控制器
每 30 分钟检查一次 pack Release（与 Freebuff 更新检查共用同一条代理链），当包的
targetVersion 与本机 Freebuff 版本一致且 packVersion 比已装/已暂存的新时，自动下载、
SHA512 校验并落到 `hanhua/output/`，随后**自动应用**：装机是英文（更新刚覆盖过）时接着
自动恢复，装机是中文但有更新的包时自动升级换上——都不需要点击。只有自动应用没法进行时
（有实例正在运行）才停在托盘提示，等下一个时机。
若最新包适配的是别的 Freebuff 版本（targetVersion 不匹配），不再静默跳过，而是
在托盘提示「最新汉化包 vX 适配 Freebuff vY，本机是 vZ」，等待版本追上后自动恢复检查。

Freebuff 的 automatic updates overwrite the localization pack's patched files.
The controller re-applies the pack automatically — when the installed version changes, before
launching an instance, and when a newer pack has been staged. The window has no standing status
text: only events and progress surface as a single line under the buttons that clears itself after
8 seconds (the standing localization status lives in the tray icon's tooltip). The first apply backs
up the pristine English files (compatible with `apply.sh`'s backup mechanism); there is no manual
apply/restore button.

## 无用安装包清理 / Updater cache + %TEMP%

Freebuff 用 electron-updater，下载好的安装包落在
`%LOCALAPPDATA%\@codebufffreebuff-desktop-updater\`，每更新一次就多一份 150 MB 级别的包，
而它们在安装完成后就没有用了（真正执行安装的是被启动的那个安装器进程，不是这个缓存）。
控制器自己从更新源下载的安装包则落在 **`%TEMP%\`**（`Freebuff-0.0.105-win-x64.exe` 这种名字），
装完之后同样没用。实测一个用了两台 Freebuff 版本的机器上这里是 **294 MB**：

```
installer.exe                            153.8 MB   0.0.105 ← 已经装上的版本
pending\Freebuff-0.0.100-win-x64.exe     153.7 MB   0.0.100 ← 比已装版本还旧
pending\current.blockmap                  161 KB
pending\update-info.json                   174 B
current.blockmap                          161 KB   （差分下载用，保留）
```

清理**已经自动化**，跟着「装完」这个时刻走：

- 控制器**启动时**（约 2.2 秒后）清一次；
- **每次 Freebuff 更新装完后**都清一次——无论是它自己 electron-updater 装的（3 秒轮询
  发现装机版本变了），还是控制器拉起的那个安装包装的（装机版本追平更新源）——并且
  **20 秒后再复查一次**：安装器常还占着自己的包，第一次删不掉，等它退出就清净了；
- **每 30 分钟**再兜底一次，把前面几轮因文件被占用而没删掉的补上。

规则与下面完全一致，删完在按钮下方那行浮出一句「已自动清理无用安装包 ✓ 释放 N MB」（没得清时
一声不吭）——那句 8 秒后自己消失，窗口与托盘都不会常驻任何缓存文字。

这条链路**没有按钮**：v1.8.17 起窗口顶部的「清理缓存」已下线——它当初的作用只是「手动过目 +
确认框」，而判定规则与自动清理一字不差，所以没有任何清理动作需要你点；想确认两处到底占了多少，
清理发生时报的那句就是结果，剩下的要么是比已装版本新的待安装包（本来就该留着）、要么是差分下载
要用的 blockmap。

两处一起扫（更新器缓存 + `%TEMP%` 里自己下过的安装包），规则同一条：安装包版本 ≤ 当前已装版本
才可删。`%TEMP%` 这一处是给「下过、但没装成」的孤儿包兜底的——`pending-installer.txt` 只记得住
**最近一份**下载（每下新的就覆盖记录），所以更早那些没装成的包原本会一直躺在那；下载中途断掉的
半截文件（SHA 校验失败那一支会自删，连接断掉那一支不会）同样由这里收。

**安全规则**（判定只有一条：安装包版本 ≤ 当前已装版本才可删）

- **不碰等待安装的更新**：`pending/` 里如果是比你当前版本新的包，那是你已下好、等退出时
  自动安装的更新；`%TEMP%` 里比已装版本新的、以及 `pending-installer.txt` 正记着的那一份
  同理留着。删掉就等于默默取消了一次更新。版本比对从安装包的 PE 资源里读
  （`installer.exe` 这种名字不带版本号），不靠文件名猜。
- **读不出版本的 `.exe` 一律不动**，宁可不清理也不猜；唯一例外是 `%TEMP%` 里名字匹配
  `Freebuff-*-win-x64.exe`、版本读不出、**且最后修改时间早于 2 小时**的（失败下载留下的半截
  文件）；「最近 2 小时」这条是为了放过正在写入的那一份。版本都读不到时整个入口什么都不删。
- **`pending/` 的 `update-info.json` / `blockmap` 只跟着它自己那个安装包一起删**，
  根目录的 `current.blockmap` 永远保留（差分下载要用，而且只有 160 KB）。
- **不动正在运行的 Freebuff**：删的是磁盘上的闲置安装包，正在跑的应用不读这个目录；
  删不掉的（被占用、无权限）会跳过并在托盘提示说明，绝不影响应用本身。
- 清理**与控制器自己的更新下载无关**：它下载安装包走 `%TEMP%`，不往这个目录写。

自动清理不弹确认框：上面这条判定保证不可能删掉「等着安装的那一份」，不需要人工过目；
装机版本在每次扫描前重新读一遍，所以「刚更新完」那一轮不会误判。

## 编译 / Build

> 不想编译？直接从 [Releases](https://github.com/Ximmmmmmm/freebuff-controller/releases) 下载单文件 exe 即可。发新版：改 `FreebuffController.cs` 里的 `AssemblyVersion` 后跑 `bash release.sh`（编译 + 打 tag + 上传）。

不需要安装任何 SDK，Windows 自带的 .NET Framework C# 编译器即可：

```
build.bat
```

或手动：

```
%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe ^
  -optimize+ -codepage:65001 ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Management.dll ^
  -win32icon:app.ico -out:FreebuffController.exe FreebuffController.cs
```

> 源码需兼容 C# 5（系统自带编译器的语言版本）。

## 项目结构 / Structure

```
├── FreebuffController.cs   # 全部源码（UI + 逻辑）
├── handover-merge.js       # 会话共享迁移脚本（编译时内嵌进 exe）
├── build.bat               # 一键编译脚本
├── tools/
│   └── embed-handover.py   # 编译前把 handover-merge.js 内嵌进 C# 源码
└── app.ico                 # 应用图标
```

## License

MIT
