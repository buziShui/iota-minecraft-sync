# 构建环境

## MSLX 插件

使用 .NET 10 SDK：

```powershell
cd mslx-plugin-iota-sync\Frontend
corepack pnpm install
corepack pnpm build
cd ..
dotnet build .\MSLX.Plugin.IotaSync.csproj -c Release
```

## PCL IO

需要 Windows 10/11 x64、Visual Studio 2022 Build Tools、“.NET 桌面生成工具”和 .NET Framework 4.8 Developer Pack。仓库必须包含 `MeloongCore` 子模块源码。

```powershell
msbuild ".\pcl-io\Plain Craft Launcher 2.sln" /restore /p:Configuration=Release
```

PCL 源码的公开版本会关闭 PCL 官方联网更新能力；PCL IO 只检查 `buziShui/iota-minecraft-sync` 的 GitHub Releases，并引导玩家前往 GitHub 下载正式版。MSLX 插件不保存或分发 PCL IO 可执行文件。
