using CommunityToolkit.Mvvm.ComponentModel;
using PMTool.Core.Models.Settings;

namespace PMTool.App.ViewModels;

public partial class SettingsShortcutRowViewModel : ObservableObject
{
    public ShortcutActionId ActionId { get; }

    public string Label { get; }

    public bool IsReadOnlyBinding => ActionId == ShortcutActionId.GlobalSearch;

    [ObservableProperty]
    private string _bindingDisplay = "";

    public SettingsShortcutRowViewModel(ShortcutActionId actionId, string label, string bindingDisplay)
    {
        ActionId = actionId;
        Label = label;
        BindingDisplay = bindingDisplay;
    }
}
