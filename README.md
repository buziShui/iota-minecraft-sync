# Iota Minecraft 客户端同步系统

Iota Minecraft Sync 用于解决 Minecraft 服务器频繁更新 Mod 后，服务端与玩家客户端文件不一致的问题。管理员通过 MSLX 面板扫描并发布服务器文件，玩家使用 Plain Craft Launcher (PCL) IO 按发布版本自动同步客户端。

[下载最新正式版](https://github.com/buziShui/iota-minecraft-sync/releases/latest) · [PCL IO 独立源码分支](https://github.com/buziShui/iota-minecraft-sync/tree/pcl)

## 项目组成

| 组件 | 运行环境 | 用途 |
| --- | --- | --- |
| MSLX Iota Sync 插件 | MSLX 1.4.7、Linux x86_64 | 扫描服务端文件、人工分类、生成不可变发布快照、管理同步码并提供下载 API |
| Plain Craft Launcher (PCL) IO | Windows 10/11 x64 | 管理同步源，在启动游戏前检查差异、备份并同步客户端文件 |

一次发布只属于一个 MSLX 服务端实例。一个面板可以管理多个实例，每个实例拥有独立版本、同步码和客户端游戏目录。

## 主要功能

- 支持 Forge、NeoForge、Fabric、Quilt。
- 支持 `mods`、`config`、`defaultconfigs`、`kubejs`、资源包、光影包等管理员选定目录。
- 先自动判断 Mod 属于“客户端必需”“可选”或“仅服务端”，再由管理员检查修改。
- 只有实例停止时才能扫描和发布，避免复制到写入中的文件。
- 发布内容保存为不可变快照，每个实例保留最近 5 个正式版本。
- 每位玩家可使用独立、长期有效的同步码；原码只显示一次，服务端仅保存 PBKDF2 加盐哈希。
- 客户端使用 SHA-256 差异检查，支持断点下载接口、更新前备份和配置冲突提示。
- 可选内容由玩家选择是否安装；玩家自行添加的未知 Mod 会保留并提示。
- 只删除以前由同步器管理、且已从新版本移除的文件。
- 首次添加同步源时，可自动创建独立实例并安装对应的 Minecraft 与加载器。
- 可自动填写服务器地址；PCL IO 正式版统一在本项目的 GitHub Releases 发布。
- 打开 PCL IO 时检查一次 GitHub 正式版；服务器更新后由玩家手动刷新或启动游戏触发检查，不后台轮询。

## 下载

前往 [GitHub Releases](https://github.com/buziShui/iota-minecraft-sync/releases) 下载：

- `MSLX-IotaSync-Plugin-版本号.zip`：服务端插件。
- `PCL-IO-版本号-windows-x64.zip`：玩家客户端。

当前服务端插件源码版本为 1.1.1；已发布版本请以 [GitHub Releases](https://github.com/buziShui/iota-minecraft-sync/releases) 为准。

## 管理员部署

### 1. 安装 MSLX 插件

1. 下载并解压服务端插件包，取得 `MSLX.Plugin.IotaSync.dll`。
2. 登录 MSLX 1.4.7，在插件管理页面上传 DLL。
3. 重启 MSLX，使插件及“客户端同步”管理页面完成加载。
4. 确认 MSLX 所在 Linux 主机能读取各 Minecraft 实例目录。

插件 ID 为 `mslx-plugin-iota-sync`，目标框架为 .NET 10，目标平台为 Linux x86_64。

### 2. 配置 PassNat / FRP 映射

额外映射 MSLX Web/API 正在监听的 HTTP 端口，而不是 Minecraft 游戏端口。玩家填写的同步源应是外部可访问的根地址，例如：

```text
https://sync.example.com
```

或：

```text
http://example.passnat.com:12345
```

客户端会自动在根地址后添加：

```text
/api/plugins/mslx-plugin-iota-sync/sync/
```

请勿把完整 API 路径重复填入客户端。建议使用 HTTPS；若只能使用 HTTP，同步码可能被同网络中的旁路监听者读取。SHA-256 只能校验文件完整性，不能代替 HTTPS 的身份认证。

### 3. 配置服务端实例

在 MSLX 中打开“客户端同步”，选择需要发布的实例并填写：

- Minecraft 版本。
- 加载器类型：Forge、NeoForge、Fabric 或 Quilt。
- 加载器版本。
- 玩家实际连接的服务器地址。
- 要纳入同步的目录；默认至少选择 `mods`，其他目录按整合包需要勾选。

不同 MSLX 实例分别配置和发布，互不覆盖。

### 4. 扫描、检查并发布

1. 停止对应 Minecraft 服务端实例。
2. 点击“刷新扫描”。
3. 检查系统自动识别的文件分类。
4. 将待确认项目改为“客户端必需”“可选”或“仅服务端”。
5. 确认差异无误后点击“发布”。

“仅服务端”文件不会提供给玩家；“可选”文件会在客户端首次遇到时询问玩家；“客户端必需”文件会进入正常同步列表。发布后形成固定快照，之后直接修改服务端目录不会悄悄改变已发布内容，必须重新扫描并发布。

### 5. 创建并分发同步码

1. 在目标实例下为玩家单独创建同步码，建议使用玩家昵称作备注。
2. 立即复制并妥善保存页面显示的原始同步码；它只显示一次。
3. 将“PassNat/FRP 外部根地址 + 同步码”私下发给对应玩家。

同步码长期有效，并绑定创建时选择的服务端实例。需要阻止某位玩家继续下载时，请在面板中撤销该玩家的同步码，不必更换其他玩家的代码。不要在截图、群公告或公开 Issue 中泄露同步码。

## 玩家使用方法

### 1. 安装 PCL IO

1. 下载并解压 `PCL-IO-版本号-windows-x64.zip`。
2. 将 `Plain Craft Launcher (PCL) IO.exe` 放入一个具有写入权限的独立文件夹。
3. 启动程序，按 PCL 默认流程配置 Java 和 Minecraft 文件夹。

PCL IO 支持 Windows 10/11 64 位，仅保留正式版更新通道。
启动器更新直接检查本项目的 GitHub Releases；MSLX 插件不保存或分发 PCL IO 可执行文件。

### 2. 添加同步源

1. 进入 `设置 → 启动设置 → PCL IO 客户端同步`。
2. 点击添加同步源。
3. 输入管理员提供的 PassNat/FRP 根地址，例如 `http://example.passnat.com:12345`。
4. 输入管理员提供的长期同步码。
5. 验证成功后，确认显示的服务器实例名称。

若本机还没有对应实例，PCL IO 会询问是否立即安装服务器指定的 Minecraft 与加载器。选择立即安装后，会创建以服务端实例名称命名的独立游戏版本，并在服务端已配置时自动填写多人游戏地址。

最多建议添加 10 个同步源。每个服务器实例对应一个独立来源和游戏版本目录。

### 3. 同步并启动

选择与同步源同名的游戏实例并点击启动。PCL IO 会：

1. 获取服务端最新发布清单。
2. 通过 SHA-256 只计算和下载发生变化的文件。
3. 对首次出现的可选内容询问是否安装。
4. 在覆盖或删除受管理文件前备份到该实例的 `.iota-backup/日期-时间/`。
5. 遇到玩家自行修改的配置时，询问“覆盖”或“保留本地”。
6. 同步完成后继续正常启动 Minecraft。

若玩家取消更新，本次游戏启动也会取消。服务端发布新版本后，玩家可手动点击刷新，或再次点击启动触发检查；程序不会在后台持续扫描。

## 文件处理规则

| 情况 | 客户端行为 |
| --- | --- |
| 服务端新增必需文件 | 下载并记录为同步器管理文件 |
| 服务端新增可选文件 | 询问玩家是否安装 |
| 已管理文件内容变化 | 备份后下载新版本 |
| 玩家修改了已管理配置 | 询问覆盖或保留本地 |
| 服务端删除已管理文件 | 文件未被玩家修改时备份并删除 |
| 玩家自行添加未知 Mod | 保留，仅提示，不强制删除 |
| 文件下载损坏 | SHA-256 校验失败，中止替换 |

同步状态保存在游戏实例内的 `.iota-sync-state.json`，备份保存在 `.iota-backup`。除非正在排查问题，不建议手动修改或删除状态文件。

## 常见问题

### 添加同步源时提示无法连接

检查 MSLX 是否运行、PassNat/FRP 映射是否指向 MSLX Web/API 端口、防火墙是否放行，以及地址是否只填写根地址。还应确认同步码没有复制空格、没有被撤销，并且属于正确实例。

### 扫描或发布按钮不可用

必须先停止 Minecraft 服务端实例。停止后重新进入页面或刷新状态，再执行扫描。

### 服务端文件改了，但玩家没有收到更新

修改目录不等于发布。管理员必须重新停止实例、扫描差异、人工确认并点击“发布”。玩家随后手动刷新或启动对应实例。

### 玩家自己的 Mod 会被删除吗

不会。同步器只管理自己以前下载并记录的文件。未知 Mod 会保留并提示；如果未知 Mod 与服务器不兼容，仍需玩家自行处理。

### 如何恢复被覆盖的配置

打开对应游戏实例目录，在 `.iota-backup` 下按时间找到备份文件，关闭游戏后复制回原位置。

### 能否共用一个同步码

技术上可以，但不推荐。每位玩家单独创建代码，泄露或停用时才能只撤销一个人的权限。

## 从源码构建

### MSLX 插件

需要 .NET 10 SDK、Node.js 与 pnpm：

```bash
cd mslx-plugin-iota-sync/Frontend
corepack pnpm install
corepack pnpm build
cd ..
dotnet build MSLX.Plugin.IotaSync.csproj -c Release
```

产物位于 `mslx-plugin-iota-sync/bin/Release/net10.0/MSLX.Plugin.IotaSync.dll`。前端必须先构建，因为页面资源会嵌入 DLL。

### PCL IO

完整源码位于 `pcl-io` 目录和仓库的 [`pcl` 分支](https://github.com/buziShui/iota-minecraft-sync/tree/pcl)。使用 Visual Studio 2022、.NET Framework 4.8 开发组件与项目所需的 .NET SDK 打开 `Plain Craft Launcher 2.sln`，选择 Release 配置构建。

## 接口与安全

同步 API 基础路径：

```text
/api/plugins/mslx-plugin-iota-sync/sync/
```

- `GET source`：验证同步源并返回绑定实例。
- `GET manifest`：取得实例的最新发布清单。
- `GET files/{path}`：下载快照文件，支持 HTTP Range。

PCL IO 的版本查询和下载直接使用 GitHub Releases，不经过 MSLX 同步 API。

客户端通过 `X-Iota-Sync-Code` 请求头提交同步码。管理接口要求 MSLX `admin` 角色。请勿将 API 暴露在没有访问控制的反向代理缓存后，也不要记录包含敏感请求头的完整调试日志。

## 仓库结构

```text
mslx-plugin-iota-sync/  MSLX 服务端插件与管理前端
pcl-io/                 Plain Craft Launcher (PCL) IO 源码
dist/                   已构建产物
docs/                   设计与协议文档
```

## 许可与声明

项目开发者署名为 BuZiShui。PCL IO 是基于 Plain Craft Launcher 的第三方二次创作，与 PCL 官方无关；原项目作者为龙腾猫跃，原作者信息及赞助入口均予以保留。使用、修改或分发前请阅读 `pcl-io/LICENCE` 及各子项目中的许可说明。

本项目主要用于个人和朋友间的 Minecraft 服务器管理。请仅同步你有权分发的 Mod、资源包、光影和其他文件，并遵守各内容作者的许可条款。
