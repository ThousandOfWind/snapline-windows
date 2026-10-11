# Snapline for Windows

<img src="src/Snapline.Windows/Assets/snapline.png" width="88" alt="Original Snapline icon">

**截图，挂在屏幕顶部。需要时出现，不需要时收起。**

灵感来源于 [alejandrobujan/tendedero](https://github.com/alejandrobujan/tendedero)。
这是独立的 Windows 原生实现，不是原作者发布的 Windows 版，也不与原作者关联。
原项目使用 Swift/AppKit；本项目使用 C#、.NET 10、WPF 和 Windows API。
遵守上游许可的名称/图标限制，使用独立名称 **Snapline** 和原创图标。

## 使用

- Windows 10/11，当前版本 **0.1.2**（功能性 MVP）。
- 本地免安装程序：`artifacts\Snapline-win-x64\Snapline.exe`。
- 可分发压缩包：`artifacts\Snapline-win-x64.zip`。解压后运行 `Snapline.exe`。
- 自包含包不要求预先安装 .NET，不需要管理员权限。
- 启动后直接展开截图栏，明确显示已运行；收起后驻留系统托盘（可能在任务栏的 `^` 隐藏图标中）。
- 再次双击 EXE 会唤出已有实例，不会静默退出，也不会启动第二个后台进程。
- 截图栏无整块底板，卡片间完全透明；收起后右上角保留小入口，点击即可找回。
- 内置截图成功或取消后自动恢复并保持展开，直到主动收起，不会截完图立即消失。
- 手动/快捷键/入口展开保持固定；只有鼠标悬停自动展开的栏会在移开后自动收起。
- 未签名，Windows SmartScreen 可能显示发布者未知。仅在确认来源后按组织安全策略运行；不要关闭系统保护。
- 不自动安装、不设置开机启动、不修改 Windows 截图位置或快捷键。

| 操作 | 功能 |
|---|---|
| `Ctrl+Alt+T` | 显示 / 隐藏截图栏 |
| `Ctrl+Alt+S` | 区域截图；拖动选择，`Esc` 取消 |
| 鼠标在屏幕顶部停留约半秒 | 临时展开已有截图；移开后自动收起 |
| 点击右上角 `◈ Snapline · N ↑` 小入口 | 找回截图栏并保持展开 |
| 单击托盘图标 | 显示截图栏并保持展开 |
| 单击缩略图 | 复制完整图片和文件引用 |
| 双击缩略图 | 内置预览，`Esc` 关闭 |
| 拖拽到资源管理器或接受文件的应用 | 发送文件副本，原文件仍保留 |
| 右键缩略图 | 复制、预览、默认应用打开、画图编辑、另存副本、定位文件、移除 |
| 点击 `×` | 从栏中移除，**不删除原文件** |
| 托盘菜单 | 导入图片、区域截图、监听目录、可选剪贴板监听、退出 |

最多保留 50 个文件引用，超出时移除最旧引用，原文件不删除；可横向滚动。
重启恢复栏中记录。PNG/JPEG/BMP/GIF/TIFF 可用，WebP 取决于系统 WIC 解码器。
动图只显示静态帧。图片文件被移动或删除后，栏中引用会自动清理。

### Windows 自带截图

默认监听系统 Pictures 目录下的 `Screenshots` / `屏幕截图`（存在时）。
如果截图位于 OneDrive 或其他工具的自定义目录，用托盘里的 **添加监听目录** 选择真实目录。
只监听所选目录新建/更新的图片，不扫描或导入历史内容，不递归子目录。
可在 **管理监听目录** 停止监听。

`Win+Shift+S` 是否保存文件取决于截图工具设置。若只复制到剪贴板，需要手动开启
**监听剪贴板图片**；也可以直接用 `Ctrl+Alt+S`，由 Snapline 保存区域截图。

### 隐私和数据

- 应用本身无网络请求、账号、遥测或云服务。构建工具/包下载和 GitHub CI 不是应用运行时。
- 设置和记录：`%LOCALAPPDATA%\Snapline\settings.json`。
- 内置截图/剪贴板图片：`%LOCALAPPDATA%\Snapline\Inbox`。
- 剪贴板监听**默认关闭**。开启后只处理图片，跳过同时带文本或文件、以及已知隐私/密码管理器标记的内容。
  无标记的敏感图片无法自动识别，使用前请考虑当前剪贴板内容。
- 开启剪贴板监听不会读取现有剪贴板，之后新复制的图片才会处理。
- 使用本应用复制不会再重复挂一张图。
- 区域截图会短暂将当前虚拟桌面保存在内存中，仅所选区域写入磁盘；取消不保存。
- 移除/淘汰/清空只删除记录，不删除文件。**收件箱没有自动清理**，需要时从托盘打开收件箱手动管理。
- 日志只记录异常类型和堆栈，不记录图片、剪贴板内容或文件路径。

## 与原版的差异 / 当前限制

- 没有 macOS 的系统 Markup、压感 Quick Look、原生截图接管、录屏缩略图或视频剪辑。
- 用 Windows 画图提供编辑入口，改动可通过目录监听刷新；不是内置标注编辑器。
- 拖拽始终复制，不自动把文件移出收件箱；不同应用对文件拖入/图片粘贴的支持不同。
- 只有卡片、按钮和细绳可见，无全宽不透明底板。透明空白区域返回 `HTTRANSPARENT`；卡片/按钮仍可操作。
- 收起后栏窗口隐藏，仅保留 130×28 DIP 的右上角入口；截图时入口也隐藏，避免被截入。
- 主动展开和内置截图结果不自动收起，需点击“收起”或按 Ctrl+Alt+T；悬停展开仍自动收起。
- 自动展开时遇到全屏应用会隐藏；主动启动/快捷键唤出不被全屏检测立即抹掉。独占全屏/受保护桌面无法保证截图。
- 已实现多屏定位和 DPI 感知，但不同 DPI、负坐标显示器、显示设置变更仍需人工回归。
- 快捷键固定；被占用时使用托盘菜单。没有安装器、代码签名和自动更新。

## 开发与构建

需要 Windows、[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)、PowerShell 7。

```powershell
git clone https://github.com/ThousandOfWind/snapline-windows.git
cd snapline-windows

# 核心测试 + UI 启动/渲染/退出 smoke test（只用生成的测试图片）
./scripts/Test.ps1

# 测试并生成自包含 x64 免安装 ZIP
./scripts/Build.ps1

# ARM64 可构建，尚未在 ARM64 机器上验证
./scripts/Build.ps1 -Runtime win-arm64

# 较小的依赖框架包（运行时需 .NET 10 Desktop Runtime）
./scripts/Build.ps1 -FrameworkDependent

# 真实桌面窗口 E2E（先从托盘退出 Snapline；测试会用生成图片替换剪贴板）
./scripts/E2E.ps1

# 修改图标后重新生成原创图标
./scripts/New-Icon.ps1
```

测试程序无需第三方测试框架：`dotnet run --project tests/Snapline.Tests -c Release`。
GitHub Actions 在 Windows 构建并提供 ZIP artifact，不自动发布或部署。

目录结构：

```text
src/Snapline.Core/       图片引用库、设置存储、文件类型/边界判定
src/Snapline.Windows/    托盘、截图栏、预览、区域截图、热键、剪贴板/目录监听
scripts/                构建、测试、原创图标生成
tests/Snapline.Tests/    无第三方依赖的核心回归测试
```

0.1.2 已在 Windows 本机运行 70 项真实 UI E2E 检查，最终免安装 EXE 同样通过。
验证包括正常启动、再次启动唤出、快捷键、目录监听、鼠标复制/预览、区域选择/取消、
移除、顶部悬停、真实剪贴板事件/隐私标记、托盘退出和重启恢复。
新增验证透明渲染/空白命中穿透、全屏合成窗口前截图后固定、取消恢复、快速反复收起/唤出、
右上角小入口找回、栏内截图按钮流程；报告为 `docs/E2E-v0.1.2.json`。
区域截图使用注入的生成桌面图片，未抓取实际桌面；拖拽导出和多屏组合仍需人工验收。
详见 [docs/TESTING.md](docs/TESTING.md)。
MIT 许可。上游版权和完整名称/图标限制见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
