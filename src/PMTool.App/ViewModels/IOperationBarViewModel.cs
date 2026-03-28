using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PMTool.App.Models;

namespace PMTool.App.ViewModels;

/// <summary>绑定到 <see cref="Controls.OperationBar"/> 的模块工具栏状态（搜索、筛选、排序、主操作）。</summary>
public interface IOperationBarViewModel : INotifyPropertyChanged
{
    string SearchPlaceholderText { get; }

    string SearchQuery { get; set; }

    ReadOnlyObservableCollection<OperationBarMenuItem> FilterMenuItems { get; }

    ReadOnlyObservableCollection<OperationBarMenuItem> SortMenuItems { get; }

    string FilterButtonText { get; }

    string SortButtonText { get; }

    string PrimaryActionLabel { get; }

    IRelayCommand? PrimaryActionCommand { get; }

    /// <summary>占位模块等场景下隐藏主按钮与筛选/排序。</summary>
    bool IsOperationBarInteractive { get; }

    /// <summary>模块内搜索已收敛到顶栏全局搜索，操作栏不再渲染局部搜索框。</summary>
    bool ShowModuleSearch => false;
}
