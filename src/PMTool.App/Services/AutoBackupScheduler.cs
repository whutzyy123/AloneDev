using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using PMTool.Application.Abstractions;
using PMTool.App.ViewModels;
using PMTool.Core.Abstractions;

namespace PMTool.App.Services;

/// <summary>自动备份：启动补备 + 每日凌晨 2 点（应用运行时）。</summary>
public sealed class AutoBackupScheduler
{
    private readonly IServiceProvider _services;
    private readonly IAccountManagementService _accountManagement;
    private DispatcherQueueTimer? _twoAmTimer;
    private bool _started;

    public AutoBackupScheduler(IServiceProvider services, IAccountManagementService accountManagement)
    {
        _services = services;
        _accountManagement = accountManagement;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is null)
        {
            return;
        }

        _started = true;
        _accountManagement.CurrentAccountChanged += (_, _) =>
        {
            _ = dq.TryEnqueue(() =>
            {
                RescheduleTwoAm();
                _ = TryStartupCatchUpAsync();
            });
        };

        RescheduleTwoAm();
    }

    /// <summary>在初始化完成后由 <see cref="App"/> 调度调用。</summary>
    public async Task TryStartupCatchUpAsync()
    {
        try
        {
            var vm = _services.GetRequiredService<DataManagementViewModel>();
            var settings = await vm.GetSettingsAsync().ConfigureAwait(true);
            if (!settings.AutoBackupEnabled)
            {
                return;
            }

            var hours = Math.Max(1, settings.MaxBackupIntervalHours);
            if (settings.LastSuccessfulBackupUtc is { } last &&
                DateTime.UtcNow - last < TimeSpan.FromHours(hours))
            {
                return;
            }

            await vm.RunScheduledBackupAsync().ConfigureAwait(true);
        }
        catch
        {
            // 静默失败；用户可在数据管理页查看备份状态
        }
    }

    private void RescheduleTwoAm()
    {
        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is null)
        {
            return;
        }

        _twoAmTimer ??= dq.CreateTimer();
        _twoAmTimer.IsRepeating = false;
        _twoAmTimer.Tick -= OnTwoAmTick;
        _twoAmTimer.Tick += OnTwoAmTick;
        _twoAmTimer.Stop();

        var localNow = DateTime.Now;
        var next = localNow.Date.AddHours(2);
        if (next <= localNow)
        {
            next = next.AddDays(1);
        }

        _twoAmTimer.Interval = next - localNow;
        _twoAmTimer.Start();
    }

    private async void OnTwoAmTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnTwoAmTick;
        try
        {
            var vm = _services.GetRequiredService<DataManagementViewModel>();
            var settings = await vm.GetSettingsAsync().ConfigureAwait(true);
            if (settings.AutoBackupEnabled)
            {
                await vm.RunScheduledBackupAsync().ConfigureAwait(true);
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            RescheduleTwoAm();
        }
    }
}
