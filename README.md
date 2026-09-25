# Freebuff 多开控制器 / Freebuff Multi-Instance Controller

一个 Windows 桌面小工具：让 [Freebuff](https://www.freebuff.com) 桌面版支持**多开**，并且**每个窗口可以登录不同的账号**。

A small Windows utility that lets the Freebuff desktop app run multiple instances simultaneously — each with its own independent account.

> 纯第三方工具，不修改 Freebuff 本体，与 Freebuff 官方无关。
> A third-party tool. It does not modify the Freebuff app itself.

## 功能 / Features

- **多开**：主实例 + 实例 1~9，互不干扰。重复双击快捷方式不会吃一句「已经被占用」，而是把已在跑的窗口**直接叫到前台**
- **多账号**：每个实例独立 Chromium 配置与登录态，可分别登录不同账号；「重置账号」即可换号
- **会话共享**（默认开）：各实例共用一份聊天记录——A 账号额度用完，B 账号的窗口打开同一份历史接着聊，不需要复制或选会话
- **额度显示**：每个账号的 **Freebucks** 今日剩余 / 总额度，悬停可见钱包余额与单价。v1.9.6 起各账号**并发拉取**（额度接口本身很慢，串行会累加成分钟级）；**没连代理就整轮跳过**，不会偷偷直连
- **自动恢复汉化**：Freebuff 更新冲掉[汉化包](https://github.com/Ximmmmmmm/freebuff-zh)后自动换回中文；**没有开关也没有按钮**，全自动
- **默认回复中文**：启动时自动写好 `~\.AGENTS.md` 语言规则（含抗注入条款），AI 无论收到什么语言都固定用简体中文回复
- **网络与代理**：检查更新 / 下载安装包 / 额度查询按「本地代理 → 系统代理 → 直连」依次尝试，并记住上次成功的路由；本地代理自动探测常见端口（功能级探测，僵死代理也能识破）。窗口顶部有「代理设置」（改地址 / 停用 / 留空即自动探测），实例掉线会主动弹气泡说明是哪个实例、哪个地址
- **窗口内入口很少**：顶部一行只有「删除会话 / 代理设置」，托盘菜单只剩「退出」——汉化、检查更新、清理安装包、额度刷新**全自动无入口**
- **停止 = 先礼后兵**：先请实例自己退出（最多等 2 秒，给 profile 里的 SQLite 收尾的机会）再硬杀，并顺着进程树把子孙编排器一起收掉
- **失败日志**：关键路径（换文件 / 杀进程 / 写 `state.json` / 建 junction / 唤起窗口）的失败会记一行到 `%TEMP%\freebuff-controller-log.txt`，不打扰界面，只在「双击了没反应」时给出现场
- **删除会话**：永久删除会话及其全部聊天记录（Freebuff 界面只有「归档 / 恢复」，没有删除入口）
- **自动清理无用安装包**：官方 electron-updater 的缓存与 `%TEMP%` 里自己下过的孤儿包，跟着「装完」这个时刻自动清
- **检查更新**：显示本机 Freebuff 版本、发现新版可一键下载官方安装包（必须取到 SHA512 才运行）；控制器自身也支持从本仓库 Release 自更新
- **界面**：v1.9.0 起的纯白简洁版 + 淡入 / 悬停等动效；单文件 exe（约 195 KB，无运行时依赖），最小化进任务栏

## 使用 / Usage

1. 双击 `Freebuff多开控制器.exe`（或自行编译，见下）
2. 双击（或选中后点「启动」）一个未初始化的实例，在弹窗里选「全新登录」
3. 在弹出的 Freebuff 窗口里登录该窗口要用的账号 —— 登录态固定在这个实例
4. 换账号：选中实例 → 「重置账号」→ 再启动登录新账号
5. 会话共享默认开启：首次并入时确认一次，之后各账号窗口打开的都是同一份会话

## 原理 / How it works

Freebuff 是 Electron 应用，用 `requestSingleInstanceLock()` 限制单开。本工具**不修改任何应用文件**，而是给每个实例分配独立的数据目录：

- `--user-data-dir=<APPDATA>\Freebuff-slot-N` — 独立 Chromium 配置文件与单实例锁
- `FREEBUFF_DESKTOP_STATE_PATH=<user>\.config\freebuff-desktop\slots\slot-N\state.json` — 独立后端 orchestrator 状态（避开其 SQLite 文件锁）
- `~\.config\freebuff-desktop\slots\slot-N\projects\<工作区>\desktop-v2.db` — 本地会话库

**会话共享原理**：每个 slot 的 `projects` 目录被替换成指向主实例会话库的 junction（`mklink /J`，不需要管理员权限；v1.8.30 起优先走原生 `DeviceIoControl`，`mklink` 只作兜底），于是所有实例读写同一个 `desktop-v2.db`，聊天记录天然共享；登录态仍在各自的 `state.json` 里，账号互不影响。首次开启会把已有历史合并进主库，原目录改名保留为 `projects.pre-share-*` 备份，可手动回退。

因此 Freebuff 应用升级不会使本工具失效。

The app is an Electron app that enforces a single-instance lock. This tool gives each instance its own `--user-data-dir` and its own orchestrator state file via an environment variable, so instances don't share locks — no app files are touched, and app updates don't break it.

## 汉化集成 / Localization

Freebuff 更新会把汉化产物覆盖回英文，控制器**自动**换上 `hanhua/output/` 里的构建，五个时机：装机版本变化、你打开控制器时、点「启动」拉起实例之前、本地 `bash build.sh` 跑完（`output/` 稳定 8 秒即认作完整构建）、刚拉到适配本机版本的新汉化包。动手前会确认「装机是英文或包更新、targetVersion 与装机一致、且没有实例在运行」；不满足就静默跳过，因「有实例在跑」被挡下的那次会留待办、10 秒后再试。

换文件是**先完整落地、再换位**：先整份拷到 `ui.new-*` → 校验 `index.html` 引用的资源齐全 → 两次同级改名换位 → 删旧份。装机目录要么是旧的完整份、要么是新的完整份，不会出现「控制器认为已应用、实际白屏」的半截态。装机被判为不完整时会自动重装。

英文原版备份只保留最近 2 份完整的（写了一半的也清掉），清理时机是每次应用之后与启动 1.5 秒后。窗口底部没有任何常驻状态文字——只有事件与进度才在按钮下方浮出一行小字，8 秒后消失；常驻信息在托盘悬停提示里。

汉化仓库的查找顺序：exe 同级或上一级的 `hanhua/` → 同级或上一级的 `freebuff-zh/` → 手编记住的路径 `%APPDATA%\FreebuffController\hanhua-path.txt`。

自测（会把装机还原成英文再核对恢复，先把 Freebuff 完全退出）：`bash tools/verify-autorestore.sh`

Freebuff's automatic updates overwrite the localization pack's patched files. The controller re-applies the pack automatically — when the installed version changes, on launch, and when a newer pack has been staged. The window has no standing status text; events and progress surface as a single line under the buttons that clears itself after 8 seconds. There is no manual apply/restore button.

## 无用安装包清理 / Updater cache + %TEMP%

官方 electron-updater 把安装包攒在 `%LOCALAPPDATA%\@codebufffreebuff-desktop-updater\`（实测约 300 MB），控制器自己下载的安装包落在 `%TEMP%`（下过但没装成的会变成孤儿）。两处一起扫，**判定只有一条：安装包版本 ≤ 当前已装版本才可删**。

- **启动时**清一次、**每次 Freebuff 更新装完后**清一次（20 秒后再复查一次，安装器常还占着文件）、**每 30 分钟**兜底一次
- **不碰等待安装的更新**：比已装版本新的包、以及 `pending-installer.txt` 正记着的那一份一律留着；读不出版本的 `.exe` 也一律不动
- 根目录的 `current.blockmap` 永远保留（差分下载要用）；删不掉（被占用 / 无权限）就下次再试，绝不影响应用本身
- 清完在按钮下方那行报一句释放了多少，没得清时一声不吭——**没有「清理缓存」按钮**，自动清理就是全部入口

## 编译 / Build

> 不想编译？直接从 [Releases](https://github.com/Ximmmmmmm/freebuff-controller/releases) 下载单文件 exe 即可。

不需要安装任何 SDK，Windows 自带的 .NET Framework C# 编译器即可：

```
build.bat
```

源码需兼容 **C# 5**（不能写 `?.`、`$""`、`nameof`、表达式体成员）。产物就是正在跑的那个 exe 自己，所以**控制器开着时写不进去**：先退出控制器再编译。编译后跑一遍离线自测（换文件那条最容易出事的路径，在临时目录的假树上验证，退出码 0 = 全过）：

```
FreebuffController.exe --self-test %TEMP%\selftest.txt && type %TEMP%\selftest.txt
```

发新版：改 `FreebuffController.cs` 里的 `AssemblyVersion`，跑 `bash release.sh`（编译 + 打 tag + 上传）。

## 项目结构 / Structure

```
├── FreebuffController.cs   # 全部源码（UI + 逻辑）
├── handover-merge.js       # 会话共享迁移脚本（编译时内嵌进 exe）
├── build.bat               # 一键编译脚本
├── tools/
│   └── embed-handover.py   # 编译前把 handover-merge.js 内嵌进 C# 源码
└── app.ico                 # 应用图标
```

## 版本记录 / Changelog

逐版本改动见 [Releases](https://github.com/Ximmmmmmm/freebuff-controller/releases) 与 `git log`。

## License

MIT
