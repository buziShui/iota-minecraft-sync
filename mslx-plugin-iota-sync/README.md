# MSLX Iota 客户端同步插件

当前源码版本为 1.2.2，适配 MSLX 1.4.7，目标运行环境为 Linux x86_64。插件 ID：`mslx-plugin-iota-sync`。

1.1.0 将 Docker 管理版中实用的操作方式同步到插件：提供总览指标、单实例工作区、服务端 Mod / 同步 Mod 双列表、搜索、全选与批量分类、正式版本记录和统一的同步码管理。MSLX 已有的实例与管理员权限直接复用，不再重复提供独立登录。PCL IO 不由插件上传或分发，统一从 [GitHub Releases](https://github.com/buziShui/iota-minecraft-sync/releases/latest) 下载。

1.1.1 修复管理页使用原生 `fetch` 导致请求未携带 MSLX 登录凭证、所有管理操作返回 401 的问题。前端现在统一使用宿主提供的 `mslx-request` 实例。

1.1.2 修复 MSLX 集合填充式反序列化导致同步目录在每次保存后重复、同一文件被重复扫描的问题。加载旧状态、保存配置和生成扫描结果时都会按路径去重；主要卡片同时接入宿主 `design-card` 透明主题样式。

1.1.3 将同步目录从基础配置中移出，新增独立的“同步目录”栏目，以目录卡片集中展示用途、启用状态和当前扫描文件数，并提供独立保存与重新扫描操作。

1.1.4 参考 ModSideChecker 扩展 Mod 端侧识别：支持 Fabric/Quilt 的 `environment` 默认值与显式声明，结构化读取 Forge/NeoForge 的加载器、Minecraft 和必需依赖 `side`，并识别 Manifest 端侧标记。只有一致证据才自动分类，冲突或未知值继续要求人工确认；重新扫描时旧的“待人工确认”结果会自动使用新规则重判，管理员已经确认的分类保持不变。

1.2.0 新增适用于全部已发布实例的统一同步码，同时保留原有单实例同步码；已撤销记录可以从管理页永久删除。同步源支持任意可访问的 HTTP/HTTPS 根地址，包括 PassNat、FRP、端口映射、反向代理、内网地址和公网域名。前端继续以 Native ESM `pluginConfig` 动态注入。

1.2.1 修复普通 HTTP 页面中浏览器禁用 Clipboard API 时无法复制同步码的问题。复制操作会优先使用安全上下文剪贴板接口，并自动回退到隐藏文本选择复制。

1.2.2 加固发布路径和并发状态安全，发布过程改为原子快照；配置保存不再覆盖扫描与发布记录；Mod 运行端侧与必需/可选下发策略分离；同步码撤回和删除使用独立接口。PCL IO 会先验证清单再保存同步源。

## 构建

需要 .NET 10 SDK 与 Node.js。前端必须先构建，因为产物会嵌入插件 DLL：

```bash
cd Frontend
corepack pnpm install
corepack pnpm build
cd ..
dotnet build MSLX.Plugin.IotaSync.csproj -c Release
```

产物：`bin/Release/net10.0/MSLX.Plugin.IotaSync.dll`。在 MSLX 的插件管理页面上传该 DLL，然后重启 MSLX。

## 使用流程

1. 打开“客户端同步”，在左侧选择实例。第一次打开时，插件会对所有已停止实例自动扫描一次。
2. 为实例填写 Minecraft 版本、加载器、加载器版本和玩家实际使用的服务器地址。
3. 勾选需要同步的目录。默认只有 `mods`；也可选择 `config`、`defaultconfigs`、`kubejs`、资源包和光影包。
4. 停止 Minecraft 实例，点击“刷新扫描”。运行中的实例禁止扫描和发布。
5. 在“Mod 分类”中检查自动识别结果。可以搜索、全选结果或多选批量移动，把全部“待人工确认”文件改为客户端必需、可选或仅服务端。
6. 点击发布。发布内容复制为不可变快照，每个实例保留最近 5 个版本。
7. 打开“同步码管理”，为每位玩家单独创建一个长期同步码。原始同步码只显示一次，服务端仅保存 PBKDF2 加盐哈希。
8. 将同步服务根地址和同步码交给玩家。

## 同步服务地址

客户端只要求能够访问 MSLX Web/API 所在端口，不限定使用哪一种穿透或代理方案。支持 PassNat、FRP、端口映射、反向代理、内网地址和公网域名。客户端请求路径统一位于：

```text
/api/plugins/mslx-plugin-iota-sync/sync/
```

同步码通过 `X-Iota-Sync-Code` 请求头发送。纯 HTTP 无法防止同网络中的旁路监听；本系统仍使用 SHA-256 阻止下载损坏，但 SHA-256 不能代替 HTTPS 的身份认证。

## API 摘要

- `GET sync/source`：验证同步源并返回绑定实例。
- `GET sync/manifest`：取得最新发布清单。
- `GET sync/files/{path}`：下载快照文件，支持 HTTP Range。

统一同步码调用 `sync/source` 时会先返回已发布实例列表；选择实例后，在三个同步接口中附加 `?instanceId=实例编号`，或使用 `X-Iota-Instance-Id` 请求头。单实例码保持原有调用方式。

PCL IO 在启动时直接检查 GitHub Releases，插件不提供客户端程序的上传和下载接口。

管理 API 全部要求 MSLX `admin` 角色；同步 API 支持实例独立的长期同步码和可选择实例的统一同步码。
