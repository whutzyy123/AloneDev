# 验收：迭代 11 — 系统设置（PRD 6.9）

对照 PRD §6.9 与第五章快捷键规则的实现说明与自测要点。

## 构建

```bash
dotnet build d:/AloneDev/src/PMTool.sln -c Debug
```

## 6.9.x 功能对照

| 要求 | 实现要点 |
|------|----------|
| 主题实时 | `SettingsViewModel` 中 `SelectedTheme` 变更即 `IAppConfigStore.SaveAsync` 并调用 `App.ApplyRequestedTheme`；启动在 `LaunchSequenceAsync` 中于首次导航前 `InitializeAsync` 后读 config 应用主题。 |
| 数据根与 config | `config.json` 位于 `IDataRootProvider.GetDataRootPath()`；锚点 `%LocalAppData%\AloneDev\config.anchor.json` 由基础设施解析（见 `ConfigAnchorStore` / DI 工厂）。 |
| 路径校验 | `IDataRootMigrationService.ValidateTargetPathAsync`：须为空目录或可创建空目录。 |
| 迁移进度 | `RunAsync` 通过 `IProgress<(string message, int percent)>` 驱动设置页 `ProgressBar` 与状态文。 |
| 失败回滚 | 迁移异常时清理目标目录内容并 `ClearPendingStateAsync`；不写新锚点（成功后才 `SetEffectiveDataRootAsync`）。 |
| 中断恢复 | `%LocalAppData%\AloneDev\migration_state.json`；`MainShellPage` 加载后若 `GetPendingStateAsync` 非空则对话框「继续 / 放弃」；继续则 `RunAsync(null, …)`，放弃则 `RollbackPendingOnlyAsync`。 |
| 快捷键表 | `config.json` → `Shortcuts`；设置页表格可编辑、「录制…」对话框、`保存设置` 时 `ShortcutBindingParser.TryValidateShortcutTable`（互斥、可解析、全局搜索强制 **Ctrl+K**）。 |
| Ctrl+K 与当前模块 | 全局搜索仍在 `MainShellPage.xaml` 固定 `Ctrl+K`；`MainShellShortcutController` 仅注册非 GlobalSearch 项；派发时根据 `ShellViewModel.ActiveNavKey` 调用对应列表 VM 的「新建 / 保存」命令。 |
| 撤销/重做 | 枚举与配置项已保留；派发层暂无文档编辑器 API 对接，按键不生效（PRD 迭代外可合并富文本场景）。 |

## 与第五章一致

- **新增类动作**：仅在当前主导航模块（`ActiveNavKey`）匹配时执行 `NewProject` / `NewFeature` / `NewTask` / `NewDocument` / `NewIdea`。
- **全局搜索**：固定 **Ctrl+K**，保存配置时校验拒绝其他绑定。

## 自动备份与 config

- 自动备份相关键以 `AppConfiguration` / `config.json` 为单一真源；首次加载可合并账户目录下遗留 `backup_settings.json`（见 `AppConfigStore.MergeLegacyBackupSettingsIfNeededAsync`）。

## 已知范围说明

- 「跟随系统」在启动时根据 `UISettings` 背景色映射为 Light/Dark；未在运行期订阅系统主题变化（可后续用 `ActualThemeChanged` 补强）。
- 路径迁移「取消」：`RunAsync` 内若需可传入 `CancellationToken`；当前设置页未单独暴露取消按钮（可按产品需求追加 `CancellationTokenSource`）。
