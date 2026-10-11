---
name: snapline-test
description: 测试 Snapline Windows 新功能或修复，扩展核心/UI E2E 回归用例，验证最终 EXE 并记录可复现证据。用户要求测试、验收、复现缺陷或新增功能回归时使用。
compatibility: Windows 10/11，.NET 10 SDK，PowerShell 7；物理 UI E2E 需要解锁且空闲的交互桌面。
---

# Snapline 测试

项目根目录为本 skill 目录的 `../../..`。先解析为绝对路径；下列命令都在项目根目录运行。
已有入口：`scripts/Test.ps1`、`scripts/Build.ps1`、`scripts/E2E.ps1`；不要另造一套测试框架。
详细覆盖/人工清单：`../../../docs/TESTING.md`。结果模板：`references/result-template.md`。

## 1. 确定范围与安全条件

- 读取当前改动、相关实现和测试；把用户行为写成“前置状态 → 操作 → 可观察结果”。
- 测试核心逻辑、开发 EXE、最终免安装 EXE 必须分别记录，不能相互冒充。
- E2E 会控制键鼠、替换并最终清空剪贴板。运行前说明这些副作用，确认用户授权、桌面空闲且 Snapline 已退出。
- 已有实例、锁屏、权限限制、并发鼠标输入：暂停并记录 `BLOCKED`，不要自动杀进程、阻止用户输入、绕过安全策略。
- 只用临时目录与生成图片。不读/备份原剪贴板、用户图库、浏览器数据或凭据。
- 保留 `--e2e-data-dir` 的隔离设置和合成截图背景；不因测试而接管真实桌面截图。额外真实内容测试需另行授权。
- 生成图片可以渲染查看；“窗口 HWND 可见”不等于内容可见、可点击。
- 本 skill 不授权安装、推送、发布 Release 或部署，不改全局 Pi 配置。

## 2. 扩展新功能测试

每个功能至少覆盖：成功路径、失败/取消、恢复/再次调用，以及受影响的旧行为。

| 改动 | 扩展位置 | 必须观察的结果 |
|---|---|---|
| 纯逻辑/设置 | `tests/Snapline.Tests/Program.cs` | 边界、无效输入、兼容旧设置、持久化、不破坏源文件 |
| 控件/交互/状态 | `tests/Snapline.E2E/Program.cs` | 真实入口操作后内容可见/可操作；取消、延时、反复操作仍可恢复 |
| 外观/透明层 | 生成图片 + 渲染/命中测试 | 像素或布局断言，空白区域与控件命中分别验证 |
| 移动/位置 | 核心 + E2E | 真实拖动、点击不混用、收起/截图/重启恢复、重置；多屏另列人工项 |
| 跨应用/系统差异 | `docs/TESTING.md` 人工清单 | 写清环境和未测范围，不用合成数据证明真实桌面或所有 DPI 场景 |

优先给控件加稳定 AutomationId，作用域限定为被测进程。用既有 `Check`/`Wait`，等待状态稳定再断言，设置有界超时。
不要只加固定 sleep、增加重试次数或放宽断言来掩盖失败；测试工具自身问题与产品缺陷分别记录。
修复缺陷时，先让新增回归用例复现原问题，再验证修复；无法复现则如实说明。

## 3. 按层运行

```powershell
# 最小回归：不触碰桌面/剪贴板
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet run --project tests/Snapline.Tests -c Release

# Snapline 已退出时：核心 + 构建 + 合成 UI smoke
./scripts/Test.ps1

# 桌面空闲且用户允许副作用时：开发 EXE 真实交互
./scripts/E2E.ps1 -Executable ./src/Snapline.Windows/bin/Release/net10.0-windows/Snapline.exe

# 构建最终免安装 EXE/ZIP，然后对同一个最终 EXE 回归
./scripts/Build.ps1
./scripts/E2E.ps1 -Executable ./artifacts/Snapline-win-x64/Snapline.exe
```

只测试纯逻辑时可只跑第一层。UI/交付验收需 smoke 和最终 EXE E2E；无法运行的层标记 `BLOCKED`/`NOT_RUN`。
每个命令检查退出码；失败立即调查，不继续宣称后续层成功。已确认的环境干扰解除后至多重跑一次，保留两次结果；仍失败就停下报告。

## 4. 保存证据并交付

- 每次 E2E 都覆盖 `artifacts/e2e-report.json` 和 `artifacts/shelf-preview.png`；重跑前复制到唯一的 `artifacts/test-runs/<时间>-<构建类型>/`。
- 保存命令/退出码、运行时间、源码 commit + dirty 状态、EXE 路径/版本/SHA256、报告及失败检查项；脱敏私人路径和内容。
- 用 `references/result-template.md` 记录到 `docs/TEST-RESULTS-<版本或功能>.md`，扩展 `docs/TESTING.md`。计数来自本次报告，不沿用历史数字。
- 最终 EXE 又被重新构建或修改后，先前 E2E 不再证明这个新二进制；重新验证或明确未测。
- `PASS` 仅指同一被测构建完整通过；部分通过不是完整通过。GitHub CI 目前只跑核心测试/发布构建，不跑交互 E2E。
- 最终回复简述：测试对象、新增覆盖、分层结果、缺陷/阻塞、未测范围、证据路径。未经测试的行为不作保证。
