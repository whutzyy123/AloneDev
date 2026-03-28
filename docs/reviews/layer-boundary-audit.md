# 前后端与数据库对接复盘（工作区相对当前 HEAD）

复盘日期：2026-03-28。基线为 **当前提交 HEAD** 与工作区差异；未与远端 `main`/`develop` 对比。计划来源：内部「前后端与 DB 对接复盘」流程。

## 1. 变更分层表

| 层级 | 路径模式（相对 `src/`） | 触达对接 | 备注 |
|------|-------------------------|----------|------|
| **App · 纯 UI** | `PMTool.App/**/*.xaml`（Themes、Controls、Views） | 否 | 布局/样式/资源；`Spacing.xaml` 等与 `GridLength` 资源相关 |
| **App · 表现逻辑** | `PMTool.App/**/*.xaml.cs`、`ViewModels/**`、`Services/**`（App 专属） | 软对接 | 导航、Shell、全局搜索协调、快捷键等；依赖 Application/Core 抽象 |
| **App · 组合根** | `PMTool.App/App.xaml.cs` | 是 | `ConfigureServices`、`AddPmToolInfrastructure` / `AddPmToolApplication`、启动与异常策略 |
| **Application** | `PMTool.Application/**` | 是 | `AddPmToolApplication`、初始化顺序、`IAppInitializationService` 构造注入变更 |
| **Infrastructure** | `PMTool.Infrastructure/**` | 是 | 仓储/SQLite/导出/DI 注册；`DataRootProvider` 删除改为 `MutableDataRootProvider` |
| **Core** | `PMTool.Core/**` | 是 | `IDataRootProvider` 增加 `SetDataRootPath` |
| **Tests** | `PMTool.Tests/**` | 是 | 与数据根等行为对齐的单元测试 |

**Git 已跟踪且相对 HEAD 有改动的源文件（不含 bin；排除 obj 后要点）**：

- App：`App.xaml` / `App.xaml.cs`，`Controls/GlobalSearchPanel`、`OperationBar`，`Themes/*`，`Views/Placeholder/ModulePlaceholderPage`、`Projects/ProjectListPage`、`Shell/MainShellPage`，`ViewModels/ProjectListViewModel`、`ProjectRowViewModel`、`ShellViewModel`，`Services/GlobalSearchUiCoordinator.cs`。
- Application：`DependencyInjection/ServiceCollectionExtensions.cs`，`Services/AppInitializationService.cs`。
- Core：`Abstractions/IDataRootProvider.cs`。
- Infrastructure：`DependencyInjection/ServiceCollectionExtensions.cs`，`Storage/DataRootProvider.cs`（删除）。
- Tests：`Storage/DataRootProviderTests.cs`。

**未跟踪（`??`）的大量 `PMTool.App` 页面/VM/服务**：属于同一迭代的**全栈增量**；合并前须按上表补全「跨界原因」行（新列表页、备份、导出、设置等）。

---

## 2. 接口与 DI 差异

| 项目 | 变更 | 影响范围 |
|------|------|----------|
| `AddPmToolApplication` | 注册 `IAccountManagementService` → `AccountManagementService` | 所有依赖该服务的应用层入口 |
| `AppInitializationService` | 注入 `IAccountManagementService`、`IAppConfigStore`；`InitializeAsync` 改为异步管线：`TryRepairOnCorruptAsync` → `LoadCatalogAndApplyLastAccountAsync` → 建目录 → `LoadAsync` | **启动顺序与账户/SQLite 打开时机**；须保证无 UI 线程长阻塞 |
| `AddPmToolInfrastructure` | `IDataRootProvider` 改为工厂创建 `MutableDataRootProvider`（锚点或默认文档根）；大量 `Singleton` 仓储与 `ISqliteConnectionHolder`、`IAppConfigStore`、`IDataRootMigrationService` 等 | 组合根膨胀；**实现类替换错误将导致构造失败或连错库** |
| `IDataRootProvider` | 新增 `void SetDataRootPath(string absolutePath)` | `DataRootMigrationService` 调用；实现见 `MutableDataRootProvider` |

**App 层**：核对 `ConfigureServices` 中新增/删除的 VM 与 `ShellViewModel`、导航注册的参数一致（计划阶段 B）；本次未逐项 diff `App.xaml.cs` 全文件，**合入前建议再跑一次** `git diff HEAD -- src/PMTool.App/App.xaml.cs`。

---

## 3. 数据库与启动链路

| 检查项 | 结论 |
|--------|------|
| `ProjectsSchema.EnsureAsync` → `SchemaMigration.ApplyAsync` | `DatabaseBootstrap.EnsureAsync` 顺序符合仓库规则 |
| 老库索引 | `ProjectsSchema` 等对列存在性做 `PRAGMA table_info` / 列名集合分支后再建索引；缺列走降级路径 |
| 迁移 | `SchemaMigration` 管线路径存在；详细版本号以源码 `TargetUserVersion` 为准 |

**老库冷启动**：需使用历史 `pmtool.db` 备份在本机手测（本报告未替代运行验证）。

---

## 4. 非法依赖（App → Infrastructure 实现）

- `grep`：`PMTool.App` 下 `using PMTool.Infrastructure` **仅** `App.xaml.cs` → `PMTool.Infrastructure.DependencyInjection`（组合根调用 `AddPmToolInfrastructure`）。
- **未发现** App 直接 `using` 具体 `*Repository` / `*Store` 实现。

---

## 5. 验证结果

| 步骤 | 状态 |
|------|------|
| `dotnet build src/PMTool.sln -c Debug` | **已通过**（2026-03-28） |
| 老库冷启动 | **待手测**（见 §3） |
| 账号切换 / 全局搜索 / 列表 CRUD | **待手测**（合入前按 PRD 点验） |

**说明**：若构建报 `MSB3026` 等文件锁错误，先结束正在运行的 `PMTool.App` 进程再编译。

---

## 6. 风险摘要

1. **启动路径**：初始化顺序与账户加载、配置修复耦合，回归重点为首次启动、换账号、损坏配置修复。
2. **数据根可变**：`SetDataRootPath` 与迁移服务联动，迁移后路径与 `ICurrentAccountContext`、连接_holder 须一致。
3. **未跟踪文件批量大**：合并前应用 `git status` 完整列表更新 §1 分层表，避免「只审了跟踪 diff」的盲区。
