# 迭代 9 验收：全局搜索（PRD 6.10 / 8.1.3 对齐）

## 构建

- 命令：`dotnet build src\PMTool.sln -c Debug`
- 预期：0 错误（WinUI XAML Markup Pass2 已通过）。

## 行为核对（手工）

| 要求 | 验证方式 |
|------|----------|
| 关键词 ≥1，输入停止 **≥500ms** 后再检索 | 快速连击字符，网络/调试断点或日志仅在停顿后触发 `ExecuteSearchAsync`；`DispatcherQueueTimer` 500ms |
| 清空关键词则结果清空 | 删除 Query 至空，`Groups` 清空、`StatusMessage` 为空或提示 |
| 范围默认全选；勾选变化后用当前词 **立即** 重搜 | 取消某范围后应立刻重查（`ScheduleSearchImmediate`） |
| 分组展示；每组先 **5** 条，「查看更多」展开 | UI 分组标题 + 展开后条数增加 |
| 点击条目：关面板、跳转模块、选中行 **+ 高亮约 3s** | 点击命中 → Flyout 关闭 → 对应列表 `IsSearchHighlight` / 转换器背景 |
| 特殊字符 `\ / : * ?` 过滤 + 提示 | 输入含上述字符，见 `FilterHint` 文案 |
| 性能感官 / 秒表（千级以内 ≤1s） | 本地造数据后全范围搜索，观察 `StatusMessage` 中毫秒数及「内容较多」提示 |

## 快捷键

- **Ctrl+K**：`MainShellPage` 键盘加速键打开 Flyout 并聚焦搜索框。
- **Esc / Enter**：面板 `KeyUp` 关闭或打开首条结果。

## 实现备注（问题排查记录）

- **UserControl** 根上若将 `x:DataType` **直接**指向 `GlobalSearchViewModel`（partial + MVVM Toolkit 源生成属性），当前 WinUI XAML 工具链在 **MarkupCompilePass2** 会以退出码 1 失败且 MSBuild 往往**无具体 XAML 行号**。
- **规避**：与 `MainShellPage` 一致，`x:DataType` 指向 **控件自身** `GlobalSearchPanel`，公开 `ViewModel` 桥接属性，绑定写为 `{x:Bind ViewModel.…}`；`DataContext` 仍由 `GlobalSearchUiCoordinator` 注入 `GlobalSearchViewModel`。`GlobalSearchViewModel` 可继续保留主构造函数。
- 跨程序集的 `GlobalSearchHit` 不宜作为内层 `DataTemplate` 的 `x:DataType`，使用 `GlobalSearchHitRowViewModel` 包装以稳定编译。

## 已知后续（非本迭代 DoD）

- 结果键盘 **↑↓** 逐条（当前 Enter 用首条简化）。
- 展开后 **>100 条分页**（PRD 6.10）可作为 9.x 小步。
