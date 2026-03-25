# Starloom

Starloom 是一个面向 FFXIV 的 Dalamud 插件，用于处理收藏品提交、工票兑换以及部分制作后的自动化流程。

## 开发环境

- 目标框架：`net10.0-windows7.0`
- 平台：`x64`
- 主要开发环境：Windows

这个项目不是独立自包含的普通 .NET 项目。除了少量 NuGet 包之外，核心依赖全部通过 Windows 下的 XIVLauncher 或 XIVLauncherCN 开发环境提供。

## 依赖来源

NuGet 依赖只有：

- `DalamudPackager`
- `ECommons`

其余核心引用都不是从仓库内或 NuGet 恢复，而是直接从 Windows 的 AppData 路径读取。默认会按当前环境选择基础目录：

- Windows：`%AppData%`
- WSL：`/mnt/c/Users/<USER>/AppData/Roaming`

在此基础上，项目会按下面顺序查找依赖：

1. `XIVLauncherCN/addon/Hooks/dev/`
2. `XIVLauncher/addon/Hooks/dev/`

这里需要存在的 DLL 包括：

- `Dalamud.dll`
- `Newtonsoft.Json.dll`
- `FFXIVClientStructs.dll`
- `Dalamud.Bindings.ImGui.dll`
- `Lumina.dll`
- `Lumina.Excel.dll`
- `Dalamud.Common.dll`
- `InteropGenerator.Runtime.dll`

如果这些文件不在上述 Windows 路径下，项目将无法正常编译。

## 构建输出

项目使用普通的本地构建输出目录：

- `bin\\Debug\\`
- `bin\\Release\\`

## 重要说明

- 这个仓库默认假设你在 Windows 或 WSL 下开发，并且本机已经安装好 XIVLauncher 或 XIVLauncherCN 的开发环境。
- 如果当前环境既没有 `%AppData%`，也没有 `/mnt/c/Users/<USER>/AppData/Roaming` 下的相关目录，`dotnet build` 会因为找不到 Dalamud 相关程序集而失败。
- 如果需要在特殊环境下编译，请显式传入 `DalamudLibPath`，覆盖默认依赖路径解析逻辑。

## 示例

可以通过 MSBuild 属性手动指定依赖目录：

```powershell
dotnet build -c Debug `
  -p:DalamudLibPath="C:\Users\<User>\AppData\Roaming\XIVLauncher\addon\Hooks\dev\" `
```

如果你的环境使用的是国服启动器目录，则把路径替换为 `XIVLauncherCN` 对应目录即可。
