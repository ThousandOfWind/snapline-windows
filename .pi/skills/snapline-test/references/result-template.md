# <功能 / 版本> 测试结果

- 时间 / 环境：<Windows、架构、.NET、显示器/DPI；未知项写未知>
- 测试对象：<commit；clean/dirty；最终 EXE 路径、版本、SHA256>
- 验收目标：<前置状态 → 用户操作 → 可观察结果>
- 新增用例：<文件/用例名；成功、取消/失败、恢复、旧行为回归>

| 层级 | 命令 / 被测对象 | 状态 | 退出码 / 本次计数 | 证据 |
|---|---|---|---|---|
| 核心 | `dotnet run --project tests/Snapline.Tests -c Release` | NOT_RUN | — | — |
| Smoke | `scripts/Test.ps1` | NOT_RUN | — | — |
| 开发 EXE E2E | `scripts/E2E.ps1 -Executable <开发EXE>` | NOT_RUN | — | — |
| 免安装构建 | `scripts/Build.ps1` | NOT_RUN | — | — |
| 最终 EXE E2E | `scripts/E2E.ps1 -Executable <最终EXE>` | NOT_RUN | — | — |
| 人工验收 | <场景/环境> | NOT_RUN | — | — |

状态：`PASS` / `FAIL` / `BLOCKED` / `NOT_RUN`。部分检查通过仍记 FAIL 或 BLOCKED，注明已通过范围。

## 失败 / 阻塞

- 复现：<状态 → 操作 → 预期/实际；失败用例名>
- 分类：<产品缺陷 / 测试工具缺陷 / 环境阻塞 / 尚未确定>
- 处理：<修复；重跑原因、次数及每次报告；不能靠反复重跑挑选成功>

## 安全与未测范围

- 数据：<临时目录、生成图片、合成桌面；原剪贴板未读取；副作用已获授权>
- 清理：<测试进程/临时目录清理；无需关闭或重启用户应用>
- 未测：<真实桌面捕获、第三方拖拽、多屏/DPI、其他版本等>
- 结论：<是否满足本次验收；下一步；不得用开发 E2E 或 CI 代替最终 EXE 证据>
