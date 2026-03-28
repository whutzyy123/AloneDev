# 迭代 10 验收：PRD 6.8 数据管理（备份 / 恢复 / 导出）

## 构建

- 命令：`dotnet build src/PMTool.sln -c Debug`
- 结果：通过（0 错误，WinUI 应用 `PMTool.App` 已生成）。

## 6.8.2 业务规则对照

| PRD 要点 | 实现说明 |
| --- | --- |
| 手动备份 `备份_yyyyMMdd_HHmmss.db` | `AccountBackupService.CreateBackupAsync` 按时间戳命名；默认可用账户下备份子目录。 |
| 自动备份：凌晨 2 点 + 启动补备 + 保留 N 份 | `AutoBackupScheduler`：`DispatcherQueueTimer` 对齐本地次日 2:00；`PostLaunchAsync` 后 `TryStartupCatchUpAsync` 按 `LastSuccessfulBackupUtc` 与 `MaxBackupIntervalHours` 补备；修剪由 `CreateBackupAsync` 内 `retentionCount` 完成。 |
| 备份校验 | 副本 `PRAGMA integrity_check`；失败删除副本并抛「备份文件损坏，请重新备份。」 |
| 恢复前预备份 `恢复前_*.db` | `RestoreFromBackupAsync` 先校验再预备份再替换。 |
| 恢复失败回滚 | 覆盖后校验失败则自预备份拷回 `pmtool.db` 并提示 6.8.7 语义；`user_version` 超出支持范围则拒绝恢复。 |
| 导出 Excel/CSV、UTF-8 | `DataExportService`：MiniExcel xlsx；CSV 为 UTF-8（含 BOM 便于 Excel）；附件/图片仅路径列。 |
| 全量 ZIP 压缩包 | **未实现**（计划 Phase 2 / 迭代后置）；当前为多模块多文件导出。 |

## 6.8.3 / 6.8.6 UI

- 导航：`ShellViewModel`「数据管理」进入 `DataManagementPage`；顶部操作栏仍为无按钮占位条。
- 分区使用 `AloneSurfaceContainerLowBrush` 等背景区分，无 1px 分割线。
- 自动备份开启确认：`ToggleSwitch` 在代码后置 `Toggled`（避免 WinUI 编译器对 `IsOffContent`/`IsOnContent` 为中文时与 `x:Bind` 同页生成失败的问题）。

## 6.8.8 验收项速查

**手动备份**：工具栏选手动目录 → `CreateBackupToDirectoryAsync`；列表 `ListBackupsAsync`。

**自动备份**：`backup_settings.json` 存开关、保留数、间隔、`LastSuccessfulBackupUtc`；账户切换时 `CurrentAccountChanged` 重排定时器并尝试补备。

**恢复**：文件选择 → 确认对话框 → `RestoreFromBackupAsync`；成功提示重启（进程内不强制退出，与 PRD「请重启软件」一致，由用户手动重启）。

**导出**：模块多选 + 格式 + 目录 + 进度条占位 `ShowExportProgress`。

**全量 ZIP**：文档中明确 **后续迭代**。

**备份列表**：名称、时间、大小；打开文件夹、删除；排序由服务按时间倒序。

**数据信息区**：路径、大小、最后修改时间（Refresh 时填充）。

## 已知限制 / 备注

- PRD 6.8.8「未执行提示：是否立即备份」为可选项，当前以启动补备为主，未单独弹窗追问。
- 列表「校验状态」列若需枚举展示可在 UI 扩展（当前以成功备份为准，失败文件不入库）。
