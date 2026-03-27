# 构建与打包说明

## 先决条件

- Windows 10/11，已安装 **.NET 8 SDK**（可选在仓库根添加 `global.json` 锁定补丁版本）。
- **WinUI 3 / Windows App SDK**  workload（Visual Studio 安装器中选择「使用 C++ 的桌面开发」与 WinUI 相关组件），或使用 `dotnet` CLI 单独还原（CI 即如此）。

## 本地命令

在仓库根目录执行：

```powershell
dotnet restore src/PMTool.sln
dotnet build src/PMTool.sln -c Release
dotnet test src/PMTool.sln -c Release --no-build
```

### 框架依赖发布（远程 CI 默认产物）

```powershell
dotnet publish src/PMTool.App/PMTool.App.csproj `
  -c Release `
  -p:Platform=x64 `
  -p:RuntimeIdentifier=win-x64 `
  -p:SelfContained=false `
  -p:PublishTrimmed=false
```

输出目录：`src/PMTool.App/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/`（含 `PMTool.App.exe` 及依赖）。目标机需安装 **.NET 8 桌面运行时**。

### MSIX（商店 / 旁加载）

单项目 MSIX 需在 Visual Studio 中通过「创建 App 包」或使用额外 MSBuild 目标生成 `.msix`；当前 GitHub Actions 工作流仅上传 **publish 文件夹** 作为可安装目录式分发基线。后续可加签名证书（`CN=`）、更新 `Package.appxmanifest` 的 `Identity` 与版本号，再扩展 workflow。

## CI

- 工作流：[.github/workflows/build.yml](../.github/workflows/build.yml)
- Runner：`windows-latest`；步骤：还原 → 生成 → 测试 → `dotnet publish` → 上传 Artifact `PMTool-publish-win-x64`。

## 版本与身份

- 应用包版本见 `src/PMTool.App/Package.appxmanifest` 中 `Identity Version`。
- 正式发布前请将 `Publisher` / 显示名改为贵司信息，并规划代码签名（避免 SmartScreen 拦截）。
