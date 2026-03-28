# UI 重构 · 阶段 0：基线与范围冻结

**版本**：阶段 0（文档冻结，不改代码）  
**目的**：固定「对齐谁」——原型、现有页面/控件、主题资源、非目标与技术约束；后续 Shell + 高频页改造均以本文与对应原型截图为验收依据。

**阶段 1 · Design Tokens 真源**（实现与合并顺序、语义色、间距/圆角/字阶/elevation、无分割线约定）：见 [UI_TOKENS_AND_THEMES.md](./UI_TOKENS_AND_THEMES.md)。本文 §5 视觉 Tokens 为阶段 0 快照；**以色板与主题字典实现及阶段 1 文档为准**。

---

## 目录

1. [目标原型清单（冻结）](#1-目标原型清单冻结)
2. [UI 映射表：原型区块 → 现有实现](#2-ui-映射表原型区块--现有实现)
3. [非目标（本轮明确不做）](#3-非目标本轮明确不做)
4. [技术约束（冻结）](#4-技术约束冻结)
5. [视觉 Tokens 表](#5-视觉-tokens-表)
6. [页面改造优先级（小步快跑）](#6-页面改造优先级小步快跑)

---

## 1. 目标原型清单（冻结）

| 屏幕 / 模块 | 原型路径 | DESIGN | 关键图 | 状态 |
|-------------|----------|--------|--------|------|
| 主界面（Shell：侧栏、顶栏、内容区、搜索入口） | `docs/页面原型设计/主界面/` | `DESIGN.md` | `screen.png` | **已冻结** |
| 项目列表（卡片列表、空状态、与 Shell 组合） | 同上（主界面承载列表区） | `DESIGN.md` §5 Card-Based Project List | `screen.png` | **已冻结** |
| 特性 / 任务列表（高密度列表、操作条） | `docs/页面原型设计/需求管理页/` | `DESIGN.md` | （见目录内资源） | **已冻结**（与主界面同一设计体系） |
| 文档列表 | — | — | — | **待原型补全**（基线：`docs/PRD.md` + 现有 `DocumentListPage`） |
| 系统设置 | — | — | — | **待原型补全**（基线：`docs/PRD.md` + 现有 `SettingsPage`） |

**说明**：文档页、设置页在 `docs/页面原型设计` 下暂无独立原型；阶段 1 起可按 PRD 与运行截图自建「临时验收图」，原型到位后替换本表链接。

---

## 2. UI 映射表：原型区块 → 现有实现

| 原型区块（见主界面 DESIGN） | 现有实现 | 备注 |
|-----------------------------|----------|------|
| Side Navigation（`surface-container-low`、左侧 4px 主色竖条、激活底） | `src/PMTool.App/Views/Shell/MainShellPage.xaml` 左栏 `Grid` + `ItemsControl` + `NavEntryViewModel` | **不引入 `NavigationView`**（阶段 0 冻结）；结构与 DESIGN §5 Side Navigation 对齐 |
| 账号入口、账号 Flyout | `MainShellPage.xaml` 内 `AccountMenuButton` + Flyout；`AccountManagementViewModel` | 仅 UI 映射，不改账号业务契约 |
| 全局搜索入口（侧栏「搜索」按钮） | `MainShellPage.xaml` `OpenSearchButton`；`GlobalSearchUiCoordinator` / `GlobalSearchViewModel` | 与 DESIGN Operation Bar & Search 协同验收 |
| Operation Bar（区段标题、筛选/动作条） | `src/PMTool.App/Controls/OperationBar.xaml`；`DataContext` = 当前 `IOperationBarViewModel`（由各列表 VM 实现） | `ShellViewModel` 切换模块时切换 `CurrentOperationBar` |
| 主内容区 | `MainShellPage.xaml` 中 `Frame` `ContentFrame` | `INavigationService` 导航各 `*Page` |
| 项目列表 / 卡片 / 侧栏详情（若原型含） | `src/PMTool.App/Views/Projects/ProjectListPage.xaml` + `ProjectListViewModel` / `ProjectRowViewModel` | P1 改造对象 |
| 特性列表 | `Views/Features/FeatureListPage.xaml` + `FeatureListViewModel` | P2；对照需求管理原型 |
| 任务列表 | `Views/Tasks/TaskListPage.xaml` + `TaskListViewModel` | P2 |
| 文档列表 | `Views/Documents/DocumentListPage.xaml` + `DocumentListViewModel` | P3；待原型 |
| 数据管理 | `Views/DataManagement/DataManagementPage.xaml` + `DataManagementViewModel` | 壳内 Footer 导航；优先级次于 P0–P2 |
| 系统设置 | `Views/Settings/SettingsPage.xaml` + `SettingsViewModel` | P3；待原型 |
| 全局搜索浮层 | `src/PMTool.App/Controls/GlobalSearchPanel.xaml` + `GlobalSearchUiCoordinator` | 玻璃/阴影在阶段 1 按 Token 落实 |
| 色 / 间距 / 圆角 / 字阶 / 按钮 | `src/PMTool.App/App.xaml` → `Themes/Colors.xaml`、`Spacing.xaml`、`TypeRamp.xaml`、`Controls.xaml` | **单一设计真源（XAML）**；页面仅组合资源键 |
| 应用级转换器、搜索行高亮等 | `App.xaml` `Application.Resources` 内 `PMTool.App.Converters` | 与列表选中/搜索态验收相关 |

---

## 3. 非目标（本轮明确不做）

- **不重做业务流程**：状态机、SQLite 表结构、仓储与领域规则维持现状。
- **不加网络**：无 `HttpClient`、遥测、云同步等（与 PRD 离线原则一致）。
- **不大改 ViewModel 对外契约**：命令签名、对外可绑定属性不轻易破坏性变更；若仅增加纯展示用属性，单开迭代说明。
- **不大爆炸式全量改 XAML**：先 Shell + 1～2 个高频页，再批量铺开。
- **阶段 0 不修改** `Themes/*.xaml`、各 `Page` / `Controls` 代码；仅新增本文档。

---

## 4. 技术约束（冻结）

| 约束 | 说明 |
|------|------|
| **数据绑定** | 列表与页面使用 **`x:Bind`**，并为主页 / `DataTemplate` 设置 **`x:DataType`**，与现有代码一致。 |
| **分隔规则** | **禁止**使用 **1px 实线边框**作为区块分割（与 DESIGN「No-Line」及项目规则一致）。层次依靠 `Surface` / `Surface-Container-*` 背景差；若必须区分同值表面，采用 **ghost**：`ControlStrokeColorDefaultBrush` 等 **ThemeResource** + **极低不透明度** 的 `Border`，而非条状分割线。 |
| **侧栏导航宿主** | 维持 **`Grid` + 自定义侧栏**。**不**在本轮仅为「_WINUI 范式_」引入 `NavigationView`；若后续无障碍或原型强制，单独立项。 |
| **玻璃态与模糊** | DESIGN 要求浮层约 **60% 表面色 + 20–40px 背景模糊**。WinUI 3 中 **`BackdropMaterial`** / **Mica** / **亚克力** 适用场景与 HTML 原型不完全等价；阶段 1 在 **`Flyout` / `TeachingTip` / 搜索面板** 上按控件支持情况选用，并在验收矩阵中注明「实现方式」与「与原型的视觉差」。 |

---

## 5. 视觉 Tokens 表

**对照关系**：`DESIGN.md` 命名 ↔ 当前 XAML 资源。单位：字阶为与 WinUI 一致的 px（`FontSize`）；间距为 dip（`Margin`/`Padding` 数值）。

### 5.1 颜色（Surface / Brand / On-color）

| DESIGN token（含义） | XAML 资源键 | 值 / 说明 |
|----------------------|-------------|-----------|
| surface | `AloneSurfaceColor` / `AloneSurfaceBrush` | `#f8f9ff` |
| surface-container-low | `AloneSurfaceContainerLowBrush` | `#f1f3fc` |
| surface-container-lowest | `AloneSurfaceContainerLowestBrush` | `#ffffff` |
| surface-container-high | `AloneSurfaceContainerHighBrush` | `#e6e8f0` |
| surface-container-highest（侧栏激活背景等） | `AloneSurfaceContainerHighestBrush` | `#e0e2ea` |
| primary | `AlonePrimaryBrush` | `#0078D4` |
| primary-container（渐变一端） | `AlonePrimaryContainerBrush` | `#005faa` |
| 主 CTA 渐变 | `AlonePrimaryGradientBrush` | `LinearGradientBrush`，约 135°（Start/EndPoint 0,0 → 1,1） |
| on-surface | `AloneOnSurfaceBrush` | `#181c22` |
| on-surface-variant | `AloneOnSurfaceVariantBrush` | `#404752` |
| on-primary | `AloneOnPrimaryBrush` | `#ffffff` |
| secondary-container（悬停柔光等） | `AloneSecondaryContainerBrush` | `#b8d3fd` |
| 导航行悬停（30% 语义） | `AloneNavHoverBrush` | `AloneSecondaryContainerColor` + **Opacity 0.3** |
| outline-variant @ 15%（ghost 边界） | — | **仅 DESIGN**：代码层尚无专用 Brush；**阶段 1** 可用 `ThemeResource` 描边色 + `Opacity` 实现 |

### 5.2 间距与布局尺度

| DESIGN token | XAML 键 | 值 (dip) | DESIGN 文案 |
|--------------|---------|----------|-------------|
| spacing-4（卡片垂直间距等） | `Spacing4` | 16 | 1rem |
| spacing-6 | `Spacing6` | 24 | 1.5rem |
| spacing-8 | `Spacing8` | 32 | 2rem |
| 侧栏宽度 | `NavRailWidth` | 256 | 与主界面栅格一致 |
| 操作条高度 | `OperationBarHeight` | 56 | 顶栏条 |
| 详情面板宽（若实现侧栏详情） | `DetailPanelWidth` | 320 | DESIGN Collapsible Detail Panel |
| 默认圆角（卡片、按钮、多数输入） | `DefaultCornerRadius` | **8** | DESIGN DEFAULT 8px |
| 搜索框小圆角 | — | **待阶段 1**：DESIGN 写 **4px**；当前全局默认 **8**；可增 `SearchFieldCornerRadius` 等资源键 |

### 5.3 字阶（Segoe UI Variable）

| DESIGN token | XAML FontSize 键 | 样式键 | DESIGN | 代码对齐 |
|--------------|------------------|--------|--------|----------|
| display-md | `DisplayMdFontSize` 44 | `AloneDisplayMdTextBlockStyle` | 2.75rem Semibold | 已对齐（44≈2.75×16） |
| headline-sm | `HeadlineSmFontSize` 24 | `AloneHeadlineSmTextBlockStyle` | 1.5rem Semibold，tracking -0.01em | **CharacterSpacing -10**（1/1000 em 单位） |
| title-sm | `TitleSmFontSize` 16 | `AloneTitleSmTextBlockStyle` | 1.0rem Medium | 已对齐 |
| title-lg（详情面板标题等） | （内联 18） | `AloneTitleLgTextBlockStyle` | DESIGN 提及 | 已存在 |
| body-md | `BodyMdFontSize` 14 | `AloneBodyMdTextBlockStyle` | 0.875rem Regular | 已对齐 |
| label-md | `LabelMdFontSize` 12 | `AloneLabelMdTextBlockStyle` | 0.75rem Bold，辅色 | `Foreground` = `AloneOnSurfaceVariantBrush` |

### 5.4 组件样式

| 用途 | XAML 样式 | 说明 |
|------|-----------|------|
| 主按钮 | `AlonePrimaryButtonStyle` | 渐变底、`DefaultCornerRadius`、`BodyMdFontSize` |
| 次要按钮 | `AloneSecondaryButtonStyle` | `AloneSurfaceContainerLowBrush` 底 |
| 导航项（备用基底） | `AloneNavItemButtonStyle` | 实际 Shell 中导航 largely 内联Setter；阶段 1 可收敛重复 |

### 5.5 阴影与深度（DESIGN → 阶段 1 落地）

| 场景 | DESIGN 参数 | XAML 现状 |
|------|-------------|-----------|
| 浮层（下拉、详情面板） | blur **32**，Y **8**，**6%** `on-surface` | **未**集中定义为 Resource；阶段 1 用 `ThemeShadow` / 模板 统一 |

### 5.6 侧栏导航像素细节（验收用）

| 项 | DESIGN | 代码参考 |
|----|--------|----------|
| 激活指示条 | 左侧 **4px** 主色竖条（pill） | `MainShellPage` `Rectangle` Width=4，`AlonePrimaryBrush`，`AccentOpacity` 绑定 |
| 图标态 | 未激活 `on-surface-variant`，激活 `primary` | 阶段 1 与 `NavEntryViewModel` 视觉态逐项对齐 |

---

## 6. 页面改造优先级（小步快跑）

| 优先级 | 范围 | 说明 |
|--------|------|------|
| **P0** | `MainShellPage` + `OperationBar` | 全应用壳与操作条；与 `主界面/screen.png` 首先对齐 |
| **P1** | `ProjectListPage` | 高频；卡片 gutter、空状态、无分割线列表 |
| **P2** | `FeatureListPage` + `TaskListPage` | 对照 `需求管理页` 原型；高密度信息与操作条 |
| **P3** | `DocumentListPage`、`SettingsPage` | 待独立原型后精修；过渡期内以 PRD + 当前页为准 |

**每步验收**：附上对应**原型截图 + 标注**（间距、字号、选中态、空状态、高密度信息），并在 `docs/ui/acceptance-matrix.md`（或等价表）中勾验。

---

## 相关文件索引

- 应用资源合并：`src/PMTool.App/App.xaml`
- 主题：`src/PMTool.App/Themes/Colors.xaml` · `Spacing.xaml` · `TypeRamp.xaml` · `Controls.xaml`
- 阶段 0 说明（本文件）：`docs/ui/UI_REFACTOR_PHASE0_BASELINE.md`
