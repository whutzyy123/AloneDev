using Microsoft.UI.Xaml.Input;
using PMTool.App.ViewModels;
using PMTool.App.Views.Shell;
using PMTool.Core.Abstractions;
using PMTool.Core.Models.Settings;
using Windows.System;

namespace PMTool.App.Services;

public sealed class MainShellShortcutController(
    IAppConfigStore configStore,
    ShellViewModel shellViewModel,
    ProjectListViewModel projectListViewModel,
    FeatureListViewModel featureListViewModel,
    TaskListViewModel taskListViewModel,
    DocumentListViewModel documentListViewModel,
    IdeaListViewModel ideaListViewModel)
{
    private readonly List<KeyboardAccelerator> _dynamicAccelerators = new();

    public async Task ReloadAsync(MainShellPage page, CancellationToken cancellationToken = default)
    {
        var cfg = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!page.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                ApplyConfigAccelerators(page, cfg);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                return;
            }

            tcs.TrySetResult();
        }))
        {
            tcs.TrySetException(new InvalidOperationException("无法在 UI 线程更新快捷键。"));
        }

        await tcs.Task.ConfigureAwait(false);
    }

    private void ApplyConfigAccelerators(MainShellPage page, AppConfiguration cfg)
    {
        foreach (var acc in _dynamicAccelerators)
        {
            page.KeyboardAccelerators.Remove(acc);
        }

        _dynamicAccelerators.Clear();

        foreach (ShortcutActionId id in Enum.GetValues<ShortcutActionId>())
        {
            if (id == ShortcutActionId.GlobalSearch)
            {
                continue;
            }

            var name = id.ToString();
            if (!cfg.Shortcuts.TryGetValue(name, out var text) || string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (!ShortcutBindingParser.TryParse(text, out var vk, out var mods, out _))
            {
                continue;
            }

            var acc = new KeyboardAccelerator { Key = vk, Modifiers = mods };
            var captured = id;
            acc.Invoked += (_, args) =>
            {
                args.Handled = true;
                Dispatch(captured);
            };
            page.KeyboardAccelerators.Add(acc);
            _dynamicAccelerators.Add(acc);
        }
    }

    private void Dispatch(ShortcutActionId id)
    {
        var nav = shellViewModel.ActiveNavKey;
        switch (id)
        {
            case ShortcutActionId.NewProject when string.Equals(nav, "projects", StringComparison.Ordinal):
                projectListViewModel.RequestNewProjectUiCommand.Execute(null);
                break;
            case ShortcutActionId.NewFeature when string.Equals(nav, "features", StringComparison.Ordinal):
                featureListViewModel.RequestNewFeatureUiCommand.Execute(null);
                break;
            case ShortcutActionId.NewTask when string.Equals(nav, "tasks", StringComparison.Ordinal):
                taskListViewModel.RequestNewTaskUiCommand.Execute(null);
                break;
            case ShortcutActionId.NewDocument when string.Equals(nav, "documents", StringComparison.Ordinal):
                documentListViewModel.RequestNewDocumentUiCommand.Execute(null);
                break;
            case ShortcutActionId.NewIdea when string.Equals(nav, "ideas", StringComparison.Ordinal):
                ideaListViewModel.RequestNewIdeaUiCommand.Execute(null);
                break;
            case ShortcutActionId.Save when string.Equals(nav, "documents", StringComparison.Ordinal):
                documentListViewModel.SaveCurrentCommand.Execute(null);
                break;
            case ShortcutActionId.Undo:
            case ShortcutActionId.Redo:
                break;
        }
    }
}
