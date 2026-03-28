# UI · Design Tokens 与主题体系（阶段 1）

**代码真源**：[`src/PMTool.App/App.xaml`](../../src/PMTool.App/App.xaml) 合并顺序 + [`Themes/`](../../src/PMTool.App/Themes/) 下各字典。  
**策略**：自定义表面与品牌色在 **Light/Dark** 下使用**同名**画笔键；页面与主题 **Style** 中对画笔使用 `{ThemeResource ...}`，对 **Style** 本身继续 `{StaticResource ...}`（样式键不分主题，内部 Setter 已用 ThemeResource 指画笔）。

---

## 1. 资源字典拓扑（合并顺序）

```mermaid
flowchart TD
  App[App.xaml Application.Resources]
  XamlControls[XamlControlsResources]
  Colors[Themes/Colors.xaml]
  Spacing[Themes/Spacing.xaml]
  TypeRamp[Themes/TypeRamp.xaml]
  Controls[Themes/Controls.xaml]
  Cards[Themes/Cards.xaml]
  Elevation[Themes/Elevation.xaml]
  Dialogs[Themes/Dialogs.xaml]
  FormSections[Themes/FormSections.xaml]
  GlobalSearch[Themes/GlobalSearch.xaml]
  Converters[Converters in App resources]
  App --> XamlControls
  App --> Colors
  App --> Spacing
  App --> TypeRamp
  App --> Controls
  App --> Cards
  App --> Elevation
  App --> Dialogs
  App --> FormSections
  App --> GlobalSearch
  App --> Converters
```

| 文件 | 职责 |
|------|------|
| `Colors.xaml` | `ThemeDictionaries` **Light** / **Dark**：`Alone*Brush`、`AlonePrimaryGradientBrush`、`SearchRowHighlightBrush`、语义色、禁用文字色；**`AloneCodePreviewHostBrush`**（片段代码预览 WebView 外框）；**`ToggleSwitchFillOn` / `ToggleSwitchStrokeOn` / `ToggleSwitchKnobFillOn*`** 等（应用级覆盖 Fluent Accent，开态统一品牌蓝） |
| `Spacing.xaml` | `AloneSpace4`～`24`、兼容 `Spacing4/6/8`、`NavRailWidth`、`OperationBarHeight`、`DetailPanelWidth`、圆角 **Small / Default / Large**、**`AloneSearchPillCornerRadius`**（顶栏/操作栏高搜索框） |
| `TypeRamp.xaml` | 字号常量、`TextBlock` **Style**（含页面主标题 **`AlonePageTitleTextBlockStyle`**、模块元信息 **`AloneModuleMetaTextBlockStyle`**、标题/副标题/正文等）；画笔 Setter 均为 **ThemeResource** |
| `Controls.xaml` | `Button` 等控件 Style；渐变与表面引用 **ThemeResource** |
| `Cards.xaml` | 列表卡片间距、标题/元信息/状态 Chip 样式 |
| `Elevation.xaml` | 浮层参数（`AloneShadow*` 文档化）、`ThemeShadow`；**列表虚拟化卡片仍不加阴影**；**看板拖拽卡片**用 **`AloneKanbanCardBorderStyle`** 加阴影；顶栏/侧栏/主 `Frame` 壳等大块可挂载 `ThemeShadow` 做柔和层级 |
| `Dialogs.xaml` | `ContentDialog` 圆角与对话框内按钮样式；`AloneDialogDestructivePrimaryButtonStyle`；`AloneFlyoutPresenterStyle`（亚克力 + `DefaultCornerRadius` + `AloneFlyoutPanelPadding`）；`AloneFormTextBoxStyle` / `AloneFormNumberBoxStyle` / `AloneFormToggleSwitchStyle`；`AloneDialogContentPadding` |
| `FormSections.xaml` | 长表单页区块：`AloneFormSectionCardStyle`（`BorderThickness="0"` + `AloneSurfaceContainerLowBrush` + `AloneFormSectionCardPadding`）；`AloneFormSectionTitleStyle` / `AloneFormSectionDescriptionStyle`；`AloneFormSectionSpacing`（节与节垂直间距）；`AloneFormPageScrollBottomPadding`（滚动内容底部呼吸）；`AloneFormPageStickyFooterStyle` / `AloneFormPageStickyFooterPadding`（可选固定底栏，与 Scroll 内容表面区分层） |
| `GlobalSearch.xaml` | 全局搜索面板结果行：`AloneGlobalSearchHitRowMinHeight`、`AloneGlobalSearchHitRowPadding`、`AloneGlobalSearchHitButtonStyle`（自定义模板，避免 Fluent `PointerOver` 覆盖 `Background` 绑定）、`AloneGlobalSearchFocusVisualMargin`；高亮色与列表跳转一致，统一使用 [`SearchRowHighlightBrush`](../../src/PMTool.App/Themes/Colors.xaml) + [`SearchRowBackgroundConverter`](../../src/PMTool.App/Converters/SearchRowBackgroundConverter.cs) |

**阶段 6（全局搜索浮层）约定**：Flyout 外壳 = [`AloneFlyoutPresenterStyle`](../../src/PMTool.App/Themes/Dialogs.xaml)（亚克力 + 圆角 + `AloneFlyoutPanelPadding`）；[`GlobalSearchPanel`](../../src/PMTool.App/Controls/GlobalSearchPanel.xaml) 根 **Background 透明**，材质不重复叠灰；结果行默认表面与转换器「非高亮」支路一致（`AloneSurfaceContainerLowestBrush`），键盘/选中索引行用 `SearchRowHighlightBrush`。不在此面板再套 `AloneFloatChromeBorderStyle` 整块实色壳（易与 Presenter 冲突）。

**阶段 5 长页布局约定**（`SettingsPage`、`DataManagementPage`）：根 `ScrollViewer` **横向** `Padding="0"`，左右由 Shell `AloneContentHostPadding` 负责；内层 `StackPanel`（或单一大纲容器）`MaxWidth="920"`（或与项目既有长页策略一致）、`HorizontalAlignment="Stretch"`、节间 `Spacing` 引用 `AloneFormSectionSpacing`，最外层内容设 `Padding="{StaticResource AloneFormPageScrollBottomPadding}"` 以免内容贴底；需常驻操作（如设置页快捷键保存/恢复默认）用 **两行 `Grid`**：`Row0` = `ScrollViewer`，`Row1` = 套用 `AloneFormPageStickyFooterStyle` 的 `Border` + 横向按钮区；节内嵌套列表行等继续用 `AloneSurfaceContainerHighestBrush` + `DefaultCornerRadius`，不用 `BorderThickness="1"` 做主分割。

**代码侧**： imperative 对话框统一走 [`AloneDialogFactory`](../../src/PMTool.App/UI/AloneDialogFactory.cs)（使用 `WinUiApplication.Current` 取资源，避免与 `PMTool.Application` 程序集名冲突）。

**主壳层级（Light）**：画布 `AloneSurfaceBrush` **#F5F7FA**；侧栏外包 `Border` 使用 **`AloneSurfaceContainerLowestBrush`（白）+ `ThemeShadow`**；主内容区 `Frame` 外包层同为 **白 + `ThemeShadow`**；顶栏/操作栏保持 elevated 白底+阴影。顶栏与模块内 `AutoSuggestBox` 使用 **`AloneSurfaceContainerHighBrush`** 与大白底区分。Dark 主题下同名键为深灰阶，`Lowest` 为最亮卡片面，逻辑一致。

**侧栏导航（[`MainShellPage`](../../src/PMTool.App/Views/Shell/MainShellPage.xaml)）**：主导航项**选中态**为整行 **胶囊形**低饱和叠色（`AloneNavRailActiveOverlayBrush`），**无左侧高对比竖条**。主列表图标框见 `AloneNavIconBox`。底部区块为**纵向**「数据管理 / 系统设置」+ 其下 **工作区切换**按钮（类 IDE 底栏）；侧栏整体外边距见 `AloneNavRailOuterPadding`。

---

## 2. 色板速查

### 2.1 表面与品牌（同名键，Light/Dark 值不同）

| 键 | 用途 |
|----|------|
| `AloneSurfaceBrush` | 页面/内容宿主下的画布（Light **#F5F7FA**） |
| `AloneSurfaceContainerLowBrush` | 中性浅灰填充：列表槽、侧栏底栏 capsule、大区块衬底（非整块侧栏） |
| `AloneSurfaceContainerLowestBrush` | 白卡 / elevated 顶层（侧栏壳、主 `Frame` 壳、顶栏） |
| `AloneSurfaceContainerHighBrush` / `AloneSurfaceContainerHighestBrush` | 次级控件面：次要按钮、搜索框槽、强调用的更高一层灰 |
| `AlonePrimaryBrush` | 主色与纯色强调（Light 偏深 **#006CBE**；Dark 提亮 **`#47A7FF`**） |
| `AlonePrimaryContainerBrush` | Chip/辅助上的深色字色或深色主题渐变端 |
| `AlonePrimaryGradientBrush` | **主按钮**与品牌块（多段蓝渐变；Light 深蓝→品牌蓝） |
| `AloneOnSurfaceBrush` / `AloneOnSurfaceVariantBrush` | 正文 / 辅助文字 |
| `AloneOnPrimaryBrush` | 主按钮上的文字 |
| `AloneSecondaryContainerBrush` / `AloneNavHoverBrush` | 次要容器、导航悬停（带透明度） |
| `AloneOnSurfaceDisabledBrush` | 禁用态文字（约 38% 不透明） |
| `AloneCodePreviewHostBrush` | 代码片段页预览区：`Border` + WebView 衬底与 **atom-one-dark**（`#282c34`）一致；Dark 主题为略提亮 `#353B45` |
| `ToggleSwitchFillOn` 等 | 与 `AlonePrimaryBrush` 对齐的 **SolidColorBrush**，覆盖 WinUI 默认 `ToggleSwitch` 开态轨道/描边/旋钮（**全局**生效；`AloneFormToggleSwitchStyle` 仍只做 `MinWidth=0`） |

### 2.2 语义色

| 键 | 用途 |
|----|------|
| `AloneSuccessBrush` | 成功 |
| `AloneWarningBrush` | 警告 |
| `AloneErrorBrush` | 错误 |
| `AloneInfoBrush` | 信息（常接近主色） |

### 2.3 其它

| 键 | 用途 |
|----|------|
| `SearchRowHighlightBrush` | 全局搜索列表高亮行（随主题变化） |
| `AloneProjectCoverGradient0` … `AloneProjectCoverGradient7` | 项目卡片顶部封面条 **`LinearGradientBrush`**（Light/Dark 同名键）；下标由 [`ProjectCoverPalette`](../../src/PMTool.Core/Ui/ProjectCoverPalette.cs) 对项目 Id 稳定哈希取模 |
| `AloneKanbanColumnBackgroundBrush` | 看板列槽统一极浅底（Light **#F9FAFB**）；列状态下标用 **`AloneKanbanColumnAccent0`～`3`** 顶部 **3px** 细条 |
| `AloneKanbanColumnBrush0`～`3` | 与 `AloneKanbanColumnBackgroundBrush` **同色**（兼容旧键；拖拽恢复仍可按列缓存） |
| `AloneKanbanDropTargetBrush` | 拖拽悬停列高亮（半透明主色，浅灰列上可辨） |

---

## 3. 间距与圆角（8px 栅格）

**原则**：列表/区块级 `Margin`、`Padding`、`Spacing`、`CornerRadius` **优先使用 8 的倍数**（8、16、24、32…），保证版面节奏一致。`AloneSpace4` 仍可作细缝；导航指示条、Flyout 行距等 **4dip** 级微调保留为**局部例外**，不在全页面上堆魔法数。

| 键 | 值 (dip) | 说明 |
|----|-----------|------|
| `AloneSpace4` | 4 | 细缝、紧凑内边距 |
| `AloneSpace8` | 8 | 小间距 |
| `AloneSpace12` | 12 | **遗留**：中间距；新布局优先 **`AloneSpace16`** |
| `AloneSpace16` | 16 | 区块竖/横间距与旧 **Spacing4** 对齐 |
| `AloneSpace24` | 24 | 与旧 **Spacing6** 相同 |
| `Spacing4` | 16 | **兼容**：等同设计 spacing-4 |
| `Spacing6` | 24 | **兼容**：等同 design spacing-6 |
| `Spacing8` | 32 | **兼容**：更大呼吸区 |
| `CornerRadiusSmall` | 8 | 小圆角块、侧栏图标底等 |
| `DefaultCornerRadius` | 8 | 默认（卡片、按钮） |
| `CornerRadiusLarge` | 16 | 大卡、看板象限外层 |
| `AloneSearchPillCornerRadius` | 24 | **MinHeight≈40** 的 `AutoSuggestBox`、圆形账号按钮（8 的倍数） |
| `AloneProjectCardGridItemWidth` | 300 | [`项目列表`](../../src/PMTool.App/Views/Projects/ProjectListPage.xaml) `ItemsWrapGrid.ItemWidth`；卡片右/下 gutter 另计 |
| `AloneKanbanColumnSpacing` | 8 | [`FeatureListPage`](../../src/PMTool.App/Views/Features/FeatureListPage.xaml) / [`TaskListPage`](../../src/PMTool.App/Views/Tasks/TaskListPage.xaml) 看板四列 `Grid.ColumnSpacing` |

**阶段 2（Shell）补充**：`AloneContentMaxWidth`（主内容最大宽度）、`AloneNavItemHeight` / `AloneNavItemPadding` / `AloneNavIconBox`、`AloneContentHostPadding`（`Frame` 宿主横向留白）、`AloneOperationBarPadding`、`AloneChromeButtonPadding`、`AloneNavRailOuterPadding` / `AloneNavHeaderStackPadding` / `AloneNavFooterSeparatorPadding`、`AloneFlyoutPanelPadding`、`AloneDetailPanelMargin` 等见 [Spacing.xaml](../../src/PMTool.App/Themes/Spacing.xaml)；顶栏筛选排序使用 **`AloneChromeButtonStyle`**（[Controls.xaml](../../src/PMTool.App/Themes/Controls.xaml)）。

新页面优先引用 **`AloneSpace8` / `AloneSpace16` / `AloneSpace24`** 与上表圆角键；旧页可继续消化 `AloneSpace12` 与零散像素，逐步迁移。

---

## 4. 字阶（TextBlock Style）

| 键 | 语义 |
|----|------|
| `AlonePageTitleTextBlockStyle` | **模块工具栏主标题**（约 **26sp**、`LineHeight` 32、**Bold**），用于 [`OperationBar`](../../src/PMTool.App/Controls/OperationBar.xaml) 等 |
| `AloneHeadlineSmTextBlockStyle` | 大标题（24 SemiBold），其它仍依赖 24 档的文案 |
| `AloneTitleTextBlockStyle` | 与 `AloneHeadlineSmTextBlockStyle` 等价别名 |
| `AloneTitleLgTextBlockStyle` / `AloneTitleSmTextBlockStyle` | 列表卡片主行等（**Lg=18 SemiBold** 为 [`AloneListCardTitleStyle`](../../src/PMTool.App/Themes/Cards.xaml) 基类） |
| `AloneModuleMetaTextBlockStyle` | **次要元信息**（**12**、Normal、**`AloneOnSurfaceVariantBrush`**）：顶栏当前模块名、侧栏说明、列表卡片灰字、文档树副行 |
| `AloneSubtitleTextBlockStyle` | 副标题（辅色，15 Medium） |
| `AloneBodyTextBlockStyle` / `AloneBodyMdTextBlockStyle` | 正文（14） |
| `AloneBodySecondaryTextBlockStyle` | 正文次级（14 + Variant） |
| `AloneLabelMdTextBlockStyle` | 分区标签（12 SemiBold + 小节标题色） |
| `AloneCaptionTextBlockStyle` | Caption（11px） |
| `AloneDisplayMdTextBlockStyle` | 空状态大标题 |

**字号资源**（如 `AlonePageTitleFontSize`、`LabelMdFontSize`）在 [`TypeRamp.xaml`](../../src/PMTool.App/Themes/TypeRamp.xaml)；`Foreground` 一律 **ThemeResource** 指向 `Alone*` 画笔。

---

## 5. Elevation（扁平优先）

- **默认**：列表、壳体主区域**不加** `ThemeShadow`。
- **例外**：**模块 / 任务看板**内可拖拽卡片使用 [`AloneKanbanCardBorderStyle`](../../src/PMTool.App/Themes/Elevation.xaml)（`BasedOn` `AloneElevatedCardBorderStyle` + `AloneFloatThemeShadow`），与长列表虚拟化行区分。
- **仅浮层**（全局搜索外壳、Flyout、菜单根）：可使用 `AloneFloatChromeBorderStyle` 或自行对根 `Border` 设置 `Shadow="{StaticResource AloneFloatThemeShadow}"`。
- **文档化参数**：`AloneShadowBlurFloat`（32）、`AloneShadowOffsetYFloat`（8）、`AloneShadowOpacityFloat`（0.06）— WinUI `ThemeShadow` 由系统渲染，数值供设计与后续模板对齐参考。

---

## 6. 不用 `BorderThickness` 做「分割」的约定

1. **区块分区**：靠 **背景阶梯**（`AloneSurface*` vs `AloneSurfaceContainer*Brush`）+ **间距**（`Margin`/`Padding` 用 `AloneSpace*` 或 `Spacing*`）。  
2. **卡片**：`CornerRadius` + `AloneSurfaceContainerLowestBrush`，卡片之间用 **垂直 `Spacing` gutter**，不用 `BorderThickness="1"` 分隔线。  
3. **Ghost 边界**（同底色需微量分离时）：`Border` + `BorderBrush="{ThemeResource ControlStrokeColorDefaultBrush}"`（或 `ControlStrongStrokeColorDefaultBrush`）+ **`BorderThickness="1"` 且 Opacity 极低**（例如整块 `Border` `Opacity="0.15"`），用于 whisper 边界；**禁止**高对比 1px 通栏分割线风格。  
4. **导航/按钮**：继承现有「无描边 + 透明底 + 表面高亮层」模式；`AloneNavItemButtonStyle` 仍 `BorderThickness="0"`（不是分割线语义）。

---

## 7. XAML 引用约定（ThemeResource vs StaticResource）

| 用法 | 标记扩展 | 说明 |
|------|----------|------|
| `Alone*Brush`、`SearchRowHighlightBrush`、`AlonePrimaryGradientBrush` | **ThemeResource** | 随 `Application.RequestedTheme` 解析 [Colors.xaml](../../src/PMTool.App/Themes/Colors.xaml) 的 Light/Dark 词典 |
| `TextFillColor*`、`SystemFillColor*`、`Acrylic*`、`Control*` 等 WinUI 系统画笔（作 `Foreground` / `Background` / `BorderBrush`） | **ThemeResource** | 随当前应用主题解析；**禁止**对上述「随主题变化的画刷」使用 `StaticResource`，否则易出现换幕后前景仍锁在旧主题解析结果的问题 |
| 页面内 **错误/警告** 长文案条幅 | **ThemeResource** `AloneErrorBrush` / `AloneWarningBrush`（推荐）或与上项一致的系统语义色 + **ThemeResource** | 与调色板成对维护，深浅可读性一致（阶段 7 实装采用 `Alone*`） |
| `Alone*TextBlockStyle`、`Alone*ButtonStyle` | **StaticResource** | 样式键名不随主题变；Style 内 Setter 对画刷仍须 **ThemeResource** |
| `ThemeShadow`、间距/圆角 `x:Double` / `Thickness` / `CornerRadius`（无 Light/Dark 分支的字典键） | **StaticResource** | 纯数值或与主题无关的资源 |

**维护检索（Code Review 可粘贴）**：在仓库内对 `src/PMTool.App` 的 XAML 自检可疑用法，例如：

- `StaticResource` 与 `Brush` 同文件同层：`Foreground="\{StaticResource`、`\{StaticResource .*Brush`
- 系统键名：`SystemFillColor`、`TextFillColor` 等若与 `StaticResource` 同现，优先改为 **ThemeResource** 或改用 `Alone*` 语义色

排除 `obj/`、`bin/` 目录。

**跟随系统**：应用在配置为 `FollowSystem` 时会订阅 `UISettings.ColorValuesChanged` 并在防抖后重算 `Application.RequestedTheme`，见 [`App.ApplyRequestedTheme`](../../src/PMTool.App/App.xaml.cs)。

---

## 8. 阶段 8 附录：列表性能、编译绑定与阴影负载

本附录与 [acceptance-matrix 阶段 8](./acceptance-matrix.md) 走查一致，供代码评审与后续迭代引用。

**编译绑定（WinUI）**：列表、卡片等 `DataTemplate` 必须声明 `x:DataType` 并在模板内使用 **`x:Bind`**（`Mode` 显式），避免页面级反射 `Binding` 回退。极少数需 `ElementName` 指向页面根以转发 `RelayCommand` 的模板行可保留 `Binding`，应在 PR 中注明原因。

**虚拟化**：`ScrollViewer` 内嵌 **`ItemsControl` 不提供 UI 虚拟化**，大列表依赖 PRD 业务上限；若日后要承载更长列表，需在不影响行内按钮、选中与看板并存的前提下评估迁移至 **`ListView`**（或其容器）或 **`ItemsRepeater`**（配合布局与交互重做），单独立项与回归。

**视觉负载（Elevation）**：与 **§5 Elevation（扁平优先）** 一致 — **列表行、列表外框、表格主区域**不叠加 `ThemeShadow`；仅 **浮层根**（对话框、Flyout、`AloneFloatChromeBorderStyle` 等）使用既有阴影策略，避免滚动区内阴影堆积。

---

## 9. 相关文档

- [UI_REFACTOR_PHASE0_BASELINE.md](./UI_REFACTOR_PHASE0_BASELINE.md)：原型映射、阶段 0 非目标与优先级（§5 Token 表为快照，**以色板实现与本文为准**）。
- [acceptance-matrix.md](./acceptance-matrix.md)：按页勾选验收。
