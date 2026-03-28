# UI 验收矩阵

本文档用于按迭代/页面勾选 UI 验收项（间距、字号、选中态、空状态、高密度信息）。

**阶段 0 基线与范围冻结**：见 [UI_REFACTOR_PHASE0_BASELINE.md](./UI_REFACTOR_PHASE0_BASELINE.md)（原型映射、非目标、技术约束、视觉 Tokens、改造优先级）。  
**阶段 1 Design Tokens / 主题字典**：见 [UI_TOKENS_AND_THEMES.md](./UI_TOKENS_AND_THEMES.md)。  
**阶段 2 Shell 骨架**：验收 `MainShellPage` + `ProjectListPage` — 侧栏/顶栏与 `docs/页面原型设计/主界面/screen.png`；主导航项高度/内边距/共享模板；主内容 `AloneContentMaxWidth`；筛选/排序 `AloneChromeButtonStyle`；首次启动 `Ctrl+K` TeachingTip（`ApplicationData.LocalSettings` 键 `AloneDev.Ui.GlobalSearchShortcutTipShown`）。

**阶段 3 列表页与卡片系统**：验收 `Themes/Cards.xaml`（`AloneListCardSpacing` / `AloneListCardPadding`、标题与元信息样式、`AloneStatusChipBorderStyle`）；`ProjectListPage` 卡片结构（标题行 + 状态 Chip + 元信息行）、`ListEmptyState` 空状态、`InfoBar` 错误条（`StringNotEmptyToBoolConverter` + 关闭时清空 `ErrorBanner`）；`FeatureListPage` / `TaskListPage` 根 `Padding="0"`（与 Shell `AloneContentHostPadding` 对齐）、详情侧栏 `AloneDetailPanelMargin` + `DetailPanelWidth`、看板单列复用 `FeatureKanbanCardTemplate` / `TaskKanbanCardTemplate`、表格行使用卡片 Padding/间距 Token；深浅色下空态 / 无匹配 / 列表 / 看板四态与 [主界面 DESIGN](../页面原型设计/主界面/DESIGN.md) 卡片层级对照。

**阶段 4 表单与对话框体系**：验收 `Themes/Dialogs.xaml`（`AloneContentDialogStyle` 圆角、`AloneDialogPrimaryButtonStyle` / `AloneDialogDestructivePrimaryButtonStyle`、`AloneFlyoutPresenterStyle` 亚克力 + 圆角、`AloneFormTextBoxStyle` / `AloneFormNumberBoxStyle` / `AloneFormToggleSwitchStyle`）；代码内 `AloneDialogFactory` 统一 imperative `ContentDialog`；不可逆删除类确认使用危险色主按钮 + 默认焦点在取消；`ProjectListPage` 项目编辑在 `PrimaryButtonClick` 内联校验 + `Description`；账号 / 筛选 / 灵感筛选 / 全局搜索 Flyout 使用 `AloneFlyoutPresenterStyle`，`GlobalSearchPanel` 根背景透明以免与 Presenter 双层；`DataManagementPage` / `DocumentListPage` 表单控件套 `AloneForm*`；深浅色下随手试新建、删除确认、账号菜单与搜索浮层。

**阶段 5 长表单页（设置 / 数据管理）**：验收 `Themes/FormSections.xaml`（`AloneFormSectionCardStyle` 无描边 + `AloneSurfaceContainerLowBrush` + `AloneFormSectionCardPadding`、`AloneFormSectionTitleStyle` / `AloneFormSectionDescriptionStyle`、`AloneFormSectionSpacing`、`AloneFormPageScrollBottomPadding`、`AloneFormPageStickyFooterStyle`）；`SettingsPage`/`DataManagementPage` 根 `ScrollViewer` 横向 `Padding="0"`（与 Shell `MainShellPage` 的 `AloneContentHostPadding` 一致、避免双重左右留白），内容 `StackPanel` `MaxWidth="920"` + 区块之间 `AloneFormSectionSpacing` + 底部 `AloneFormPageScrollBottomPadding`；各节统一节卡片样式与标题/说明字阶；`SettingsPage` 快捷键「保存设置」「恢复默认快捷键」置于 `Grid.Row1` 固定底栏（与滚动区分层，无 1px 分割）；深浅色下长滚动、嵌套 `Highest` 小卡层级、底栏始终可见；错误/警告条幅与阶段 7 一致：`ThemeResource AloneWarningBrush` / `AloneErrorBrush`。

**阶段 6 全局搜索与浮层**：验收 `Themes/GlobalSearch.xaml`（`AloneGlobalSearchHitRowMinHeight`、`AloneGlobalSearchHitRowPadding`、`AloneGlobalSearchHitButtonStyle`、`AloneGlobalSearchFocusVisualMargin`）；`GlobalSearchUiCoordinator` 仍使用 `AloneFlyoutPresenterStyle`（亚克力），`GlobalSearchPanel` 根透明避免双层；结果行 `Background` 经 `SearchRowBackgroundConverter` + `IsKeyboardHighlighted` 与列表页 `SearchRowHighlightBrush` 一致；有结果时默认扁平索引 0 高亮，**Down** 自搜索框进入结果、**Up** 在第一行时回到搜索框并将索引置 -1、**Home/End**、**Enter**（`FocusedHitFlatIndex >= 0` 时打开该项，否则打开首条）；`UseSystemFocusVisuals` + 程序化焦点与 `StartBringIntoView`；分组「查看更多」展开/收起后 `DisplayedHitsRebuilt` 重编号与焦点钳位；浅色/深色下对比浮层层次与焦点环可见性。

**阶段 7 深色模式与系统主题**：验收 [UI_TOKENS §7](./UI_TOKENS_AND_THEMES.md) 中 ThemeResource/StaticResource 规则与 PR 自检 grep 说明；列表/设置/数据管理/搜索等页 **错误类条幅** `ThemeResource AloneErrorBrush`、**提示警告条幅** `ThemeResource AloneWarningBrush`（与 `Colors.xaml` Light/Dark 成对）；`App.ApplyRequestedTheme` + `UISettings.ColorValuesChanged`（配置为 **跟随系统** 时订阅，固定浅/深时退订），防抖后重算 `RequestedTheme`；手动：在设置中切换 **浅色 / 深色 / 跟随系统** 各一轮；**跟随系统** 下于 Windows 设置切换系统浅色↔深色（应用不关），抽检 **正文与表面**、**错误/警告文案**、**禁用态**（如有）、**全局搜索高亮行**、`MainShellPage` 导航与 `OperationBar`、`ContentDialog` 抽样；无「仅背景切换、正文仍像另一主题」现象。

**阶段 8 打磨与一致性审计**：深浅色下按下方 **跨页走查表**逐项勾选：根间距与 Shell `AloneContentHostPadding` 无双重左右留白；错误条/筛选/空态/无匹配句式与 `ListEmptyState` 模式一致；主列表区域与详情侧栏间距 Token 统一；列表行与列表壳体 **无** 多余 `ThemeShadow`（仅浮层按 [Elevation](./UI_TOKENS_AND_THEMES.md) 与 [UI_TOKENS §8](./UI_TOKENS_AND_THEMES.md)）。详见 **§8** 中虚拟化与 x:Bind 约定。

### 跨页走查表（列表宿主）

| 页面 | 页根 Padding | 错误 UI | 筛选 | 空态 | 无匹配文案 | 主列表 | 虚拟化 | 备注 |
|------|--------------|---------|------|------|------------|--------|--------|------|
| `ProjectListPage` | `0` | `InfoBar` | Shell `OperationBar` | `ListEmptyState` + 无主按钮无匹配 | 调整筛选或搜索 | `ScrollViewer`+`ItemsControl` | 无 | 详情 `AloneDetailPanelMargin` |
| `FeatureListPage` | `0` | `InfoBar` | Shell | `ListEmptyState` + 无匹配 `StackPanel` | 调整筛选或搜索 | 表格+看板 `ItemsControl` | 无 | 看板多列 |
| `TaskListPage` | `0` | `InfoBar` | Shell | 同 Feature | 同左 | 表格+看板 | 无 | 同左 |
| `IdeaListPage` | `0` | `Border` | 页内 `AloneChromeButtonStyle`+Flyout | `ListEmptyState` | 调整筛选或搜索 | `ScrollViewer`+`ItemsControl` | 无 | 侧栏 360dp |
| `ReleaseListPage` | `0` | `Border` | 页内 `ComboBox` 项目 | `ListEmptyState`（含选项目/无项目） | 调整筛选或搜索 | `ScrollViewer`+`ItemsControl` | 无 | 侧栏 340dp |
| `DocumentListPage` | `0` | `Border` | 树隐式 | （树+编辑器） | — | 左 `ListView` 树 / 右编辑器 | 树可虚拟化 | **双栏例外**：列内自管 `Padding`/`Margin` |

---

## 视觉 MCP（`user-Windows-Vision`）运行与抽检记录

**脚本**：`tools/vision-mcp-runbook.ps1` — 推荐流程 `dotnet build src/PMTool.sln -c Debug` → `Start-Process ... -WorkingDirectory (exe 目录)` → Wait → Snapshot；已构建时用 `-SkipBuild` 或 `-StartOnly`。

**降级**：MCP/受限终端内若出现 `Microsoft.VisualStudio.RemoteControl.dll` 拒绝访问、NETSDK1064 等，在 **Cursor 内置终端**（或与 IDE 同权限）完成 `restore`/`build`，MCP 侧只做启动与桌面操作。

**证据形式**：无单独落盘截屏路径时，以本机 MCP **Snapshot（`use_ui_tree` / `use_vision`）** 输出与下列勾选为准；日期 **2026-03-28**。

### P0 阻塞（产品/导航/崩溃）

| 项 | 状态 |
|----|------|
| 应用启动、主导航、核心列表切换 | 无 P0（本波未观察到崩溃/XAML 解析错误） |
| Agent 沙箱内 `dotnet build` 失败 | **非产品缺陷**：环境/NuGet 权限，按上行降级 |

### 波次 × 验收阶段（MCP 实测摘要）

| 波次 | 矩阵阶段 | MCP 动作摘要 | 结果 |
|------|----------|----------------|------|
| A | 2 Shell | Snapshot：`AloneDev` 焦点窗；侧栏 + `OperationBar`；顶栏「新建项目」、居中搜索占位符 | 通过（与 Shell 骨架一致） |
| B | 3 列表/卡片 | Click 侧栏 y≈351 → Snapshot：顶栏「新建特性」「搜索特性名称或描述…」 | 通过（模块切换 + 列表栏文案切换） |
| C–D | 4–5 表单/长页 | Click 侧栏「系统设置」→ `use_vision` Snapshot：标题「系统设置」、侧栏高亮；`DisabledOperationBarViewModel` 下筛选/排序为禁用态 | 通过（长页控件在 WinUI 树上不完整为 **已知限制**，底栏/主题等辅以截屏或手工） |
| E | 6 全局搜索 | `Ctrl+K` 与侧栏「搜索」坐标点击在当前 MCP 树上 **未稳定暴露浮层子树** | **部分**：与计划一致，浮层需 **截屏（`use_vision`）** 或手工确认；快捷键已由 `MainShellPage` 页级 `KeyboardAccelerator` 注册 |
| F | 7–8 主题/走查 | 本轮为 **深色** 界面抽检（截图描述）；浅/深/跟随系统全组合需设置内切换后复 Snapshot | **部分完成**：需在设置中显式切换主题后补一轮 |

### 跨页走查表（视觉 MCP 复检口令）

对表里各页执行：**Snapshot（列表态）→ 切换空态/无匹配（若可）→ 再 Snapshot**；与上表同用 `display` 限定多显示器时令 `AloneDev` 所在屏。

---

