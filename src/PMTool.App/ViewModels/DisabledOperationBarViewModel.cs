using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PMTool.App.ViewModels;

/// <summary>未实现模块：禁用工具栏交互。</summary>
public sealed class DisabledOperationBarViewModel : ObservableObject, IOperationBarViewModel
{
    private readonly ReadOnlyObservableCollection<Models.OperationBarMenuItem> _empty =
        new(new ObservableCollection<Models.OperationBarMenuItem>());

    public string SearchPlaceholderText => string.Empty;

    public string SearchQuery { get; set; } = string.Empty;

    public ReadOnlyObservableCollection<Models.OperationBarMenuItem> FilterMenuItems => _empty;

    public ReadOnlyObservableCollection<Models.OperationBarMenuItem> SortMenuItems => _empty;

    public string PrimaryActionLabel => string.Empty;

    public CommunityToolkit.Mvvm.Input.IRelayCommand? PrimaryActionCommand => null;

    public bool IsOperationBarInteractive => false;

    public bool ShowModuleSearch => false;

    public string FilterButtonText => "筛选";

    public string SortButtonText => "排序";
}
