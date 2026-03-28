using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PMTool.Application.Abstractions;
using PMTool.Core.Abstractions;

namespace PMTool.App.ViewModels;

public partial class AccountManagementViewModel(
    IAccountManagementService accountManagement,
    IIteration1ProbeRepository probeRepository) : ObservableObject
{
    public ObservableCollection<string> Accounts { get; } = [];

    [ObservableProperty]
    private string _newAccountName = "";

    [ObservableProperty]
    private string? _selectedAccountName;

    [ObservableProperty]
    private string _currentAccountDisplay = "";

    [ObservableProperty]
    private string _statusMessage = "";

    public async Task RefreshAsync()
    {
        Accounts.Clear();
        var list = await accountManagement.GetAccountsAsync().ConfigureAwait(true);
        foreach (var a in list)
        {
            Accounts.Add(a);
        }

        CurrentAccountDisplay = accountManagement.CurrentAccountName;
        SelectedAccountName = accountManagement.CurrentAccountName;
        StatusMessage = "";
        AddAccountCommand.NotifyCanExecuteChanged();
        SwitchAccountCommand.NotifyCanExecuteChanged();
    }

    partial void OnNewAccountNameChanged(string value)
    {
        AddAccountCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedAccountNameChanged(string? value)
    {
        SwitchAccountCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAddAccount))]
    private async Task AddAccountAsync()
    {
        try
        {
            StatusMessage = "";
            await accountManagement.CreateAccountAsync(NewAccountName).ConfigureAwait(true);
            NewAccountName = "";
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private bool CanAddAccount() => !string.IsNullOrWhiteSpace(NewAccountName);

    [RelayCommand(CanExecute = nameof(CanSwitchAccount))]
    private async Task SwitchAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedAccountName))
        {
            return;
        }

        try
        {
            StatusMessage = "";
            await accountManagement.SwitchToAsync(SelectedAccountName).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private bool CanSwitchAccount() => !string.IsNullOrWhiteSpace(SelectedAccountName);

    [RelayCommand]
    private async Task InsertProbeAsync()
    {
        try
        {
            StatusMessage = "";
            var payload = $"{accountManagement.CurrentAccountName} @ {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z";
            await probeRepository.InsertMarkerAsync(payload).ConfigureAwait(true);
            StatusMessage = "已写入环境校验数据。";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
