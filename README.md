# PMTool

基于 **WinUI 3**（Windows App SDK）与 **.NET 8** 的本地项目管理工具：离线优先、按账户隔离的 **SQLite** 数据、**MVVM**（CommunityToolkit.Mvvm）。

## 技术栈与约束

- **SDK**：见仓库根 `global.json`（当前锁定 .NET 8 补丁线，并允许 `latestFeature` 滚动）。
- **包还原**：根目录 `nuget.config` 将全局包目录指向仓库内 `.nuget/`（已在 `.gitignore` 中忽略，避免污染提交）。
- **100% 离线**：应用内无网络请求；数据与附件均在本地路径下管理。

## 仓库结构（简述）

| 路径 | 说明 |
|------|------|
| `src/PMTool.sln` | 解决方案 |
| `src/PMTool.App` | WinUI 3 可执行项目 |
| `src/PMTool.Core` / `Application` / `Infrastructure` | 分层与 SQLite |
| `src/PMTool.Tests` | 单元测试 |
| `build/README.md` | **构建、发布、CI、框架依赖 publish 等详细说明** |
| `.cursor/rules/` | Cursor 侧约定（XAML / SQLite 迁移顺序 / Shell·DI·Flyout 等） |

## 本地开发与构建

在仓库根目录执行（与 `build/README.md` 一致）：

```powershell
dotnet restore src/PMTool.sln
dotnet build src/PMTool.sln -c Debug
dotnet test src/PMTool.sln -c Debug
```

**常见问题**：若出现 `MSB3027` 或「文件正由另一进程使用」，请先**退出正在运行的 `PMTool.App`** 再构建。

## `.gitignore` 策略摘要

以下内容应避免提交（详见根 `.gitignore`）：

- `bin/`、`obj/`、`obj-probe/` 等构建产物与探测输出  
- 仓库内 `.nuget/`、可选 `.nuget-workaround/`  
- `.vs/`、`.idea/`、`.user`、测试结果目录、`*.binlog` 及常用本地调试日志文件名  

提交前若工作区曾被本地构建污染，可执行 `dotnet clean src/PMTool.sln` 并确认未 `git add` 上述路径。

## 许可与贡献

按仓库内已有许可与团队规范执行；涉及 XAML 与数据库迁移的改动，请同步对照 `.cursor/rules/` 中的防回归条目。
