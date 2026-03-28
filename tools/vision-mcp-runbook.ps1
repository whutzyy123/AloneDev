<#!
  AloneDev · 视觉 MCP（user-Windows-Vision）配套脚本
  - 在 Cursor / 本机终端运行（与 IDE 同上下文，避免 MCP 子进程 NuGet 锁或权限问题）。
  - MCP 侧：用 Windows-Vision「PowerShell」工具调用本脚本，或逐步执行下方「常用片段」。
  - 仓库已含 `nuget.config`：`globalPackagesFolder` 指向 `.nuget/packages`，可减少与 `%USERPROFILE%\.nuget` 下已锁定 DLL 的冲突。
  - 降级：若仍出现 `RemoteControl.dll` Access denied，关闭多余 `dotnet`/`dotnet watch`/VS 后重试；或单次设置 `$env:NUGET_PACKAGES='D:\path\to\empty-cache'` 再 restore；NETSDK1064 等同理在 **Cursor 内置终端** 还原/构建。

  常用顺序：Build → Start-App →（人工/MCP Wait+Snapshot+Click）
  仅启动（已本地构建）：`-SkipBuild` 或 `-StartOnly`
#>

param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$BuildOnly,
    [switch]$StartOnly,
    [switch]$SkipBuild,
    [switch]$Kill,
    [int]$StartupWaitSeconds = 3
)

$ErrorActionPreference = 'Stop'

$sln = Join-Path $RepoRoot 'src/PMTool.sln'
function Get-PublishedExe {
    $candidates = @(Get-ChildItem -Path (Join-Path $RepoRoot 'src/PMTool.App/bin') -Filter 'PMTool.App.exe' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending)
    if ($candidates.Count -eq 0) {
        throw "未找到 PMTool.App.exe，请先 dotnet build（期望路径在 src/PMTool.App/bin 下）。"
    }
    $candidates[0].FullName
}

if ($Kill) {
    Get-Process -Name 'PMTool.App' -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host '已结束 PMTool.App 进程。'
    exit 0
}

if (-not $StartOnly -and -not $SkipBuild) {
    Write-Host "dotnet build: $sln"
    dotnet build $sln -c Debug
    if ($LASTEXITCODE -ne 0) {
        throw "构建失败（退出码 $LASTEXITCODE）。若仅在 MCP 内失败，请到 Cursor 内置终端执行同一命令（NuGet/RemoteControl.dll 权限见仓库说明）。"
    }
}

if ($BuildOnly) {
    Write-Host 'BuildOnly：跳过启动。'
    exit 0
}

$exe = Get-PublishedExe
$binDir = Split-Path $exe -Parent
Write-Host "Start-Process: $exe"
Write-Host "WorkingDirectory: $binDir"

$proc = Start-Process -FilePath $exe -WorkingDirectory $binDir -PassThru
Write-Host "PID: $($proc.Id)；等待 ${StartupWaitSeconds}s 便于 UI 加载…"
Start-Sleep -Seconds $StartupWaitSeconds
Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, MainWindowTitle | Format-Table
