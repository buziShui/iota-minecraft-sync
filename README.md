# Iota Sync × MSLX 联合管理中心

面向私有 Minecraft 服务器的统一管理与客户端同步服务。Docker 版既能独立扫描挂载目录，也能安全连接现有 MSLX Daemon，在同一个中文 Web 页面中完成服务端控制、文件管理、备份、Mod 分类、版本发布与 PCL IO 客户端分发。

> 当前分支：`docker`。MSLX 插件版请查看 [`main` 分支](https://github.com/buziShui/iota-minecraft-sync/tree/main)，PCL IO 源码请查看 [`pcl` 分支](https://github.com/buziShui/iota-minecraft-sync/tree/pcl)。

## 能做什么

- 在 Web 页面管理多个 Minecraft 服务端实例。
- 通过 MSLX 官方 API 查看实例状态，并执行启动、停止、重启和备份。
- 浏览、编辑、重命名或删除 MSLX 实例文件，API Key 始终只保存在服务端。
- 将 MSLX 实例一键关联到 Iota，同步扫描和发布前自动确认实例已经停止。
- 扫描 `mods`、配置、KubeJS、资源包和光影包目录。
- 自动初步识别客户端必需、可选、仅服务端 Mod，再由管理员确认。
- 发布不可变快照，每个实例保留最近 5 个版本。
- 为不同玩家签发和撤销长期同步码，服务端只保存 PBKDF2 哈希。
- 为 PCL IO 提供清单、文件、HTTP Range 下载和启动器更新接口。
- 使用 SHA-256 校验所有同步文件。
- Minecraft 源目录可只读挂载，服务本身不会修改服务器文件。

## 快速部署

### 1. 准备 Compose 配置

复制并修改 [`iota-sync-docker/compose.yml`](iota-sync-docker/compose.yml)：

```yaml
services:
  iota-sync:
    build:
      context: ..
      dockerfile: iota-sync-docker/Dockerfile
    image: iota-sync:1.0.0
    container_name: iota-sync
    restart: unless-stopped
    ports:
      - "8080:8080"
    extra_hosts:
      - "host.docker.internal:host-gateway"
    environment:
      TZ: Asia/Shanghai
      IOTA_ADMIN_USERNAME: "${IOTA_ADMIN_USERNAME:?请先在 .env 中设置管理员账号}"
      IOTA_ADMIN_PASSWORD: "${IOTA_ADMIN_PASSWORD:?请先在 .env 中设置管理员密码}"
      IOTA_MSLX_BASE_URL: "${IOTA_MSLX_BASE_URL:-}"
      IOTA_MSLX_PUBLIC_URL: "${IOTA_MSLX_PUBLIC_URL:-}"
      IOTA_MSLX_API_KEY: "${IOTA_MSLX_API_KEY:-}"
      IOTA_MSLX_SERVERS_ROOT: "${IOTA_MSLX_SERVERS_ROOT:-/servers}"
    volumes:
      - ./data:/data
      - /opt/minecraft/friends:/servers/friends:ro
      - /opt/minecraft/modded:/servers/modded:ro
```

冒号左侧是宿主机真实服务端目录，右侧是容器内路径。可按实例继续添加挂载。然后复制账号密码模板并修改：

```bash
cp iota-sync-docker/.env.example iota-sync-docker/.env
```

### 2. 启动

在仓库根目录执行：

```bash
docker compose -f iota-sync-docker/compose.yml up -d --build
docker compose -f iota-sync-docker/compose.yml logs -f
```

访问 `http://服务器IP:8080`，输入 `.env` 中的 `IOTA_ADMIN_USERNAME` 和 `IOTA_ADMIN_PASSWORD` 登录。登录成功后服务端会签发 30 天有效的 HttpOnly Cookie，刷新页面无需重复输入密码；退出登录会立即清除 Cookie。

### 3. 连接 MSLX（推荐，可选）

在 `.env` 中填写 MSLX Daemon：

```dotenv
IOTA_MSLX_BASE_URL=http://host.docker.internal:1027
IOTA_MSLX_PUBLIC_URL=http://192.168.1.2:1027
IOTA_MSLX_API_KEY=你的MSLX全局API密钥
IOTA_MSLX_SERVERS_ROOT=/servers
```

- `IOTA_MSLX_BASE_URL` 是 Iota 容器能访问的 Daemon 地址，必须是完整的 `http://` 或 `https://` 地址。示例通过 `host-gateway` 访问宿主机已映射的 1027 端口；也可以把两个容器加入同一 Docker 网络后使用 `http://mslx-daemon:1027`。
- `IOTA_MSLX_PUBLIC_URL` 是管理员浏览器访问 MSLX 的地址，可与内部地址不同。
- `IOTA_MSLX_API_KEY` 在 MSLX 的全局设置中生成，只由 Iota 后端携带，不会出现在浏览器请求或页面源码中。
- `IOTA_MSLX_SERVERS_ROOT` 是 MSLX 实例目录在 Iota 容器内的共同父目录。必须把 MSLX 的 `Servers` 目录只读挂载到这里，例如 `/vol1/@appshare/MSLX/Servers:/servers:ro`。

登录后进入“服务器控制”，选择实例并点击“关联到 Iota Sync”。系统会复用对应的 `/servers/{MSLX实例ID}` 目录；以后扫描和发布时将从 MSLX 实时确认实例已停止，无需人工勾选。

### 4. 添加和发布实例

1. 添加实例名称和容器内目录，例如 `/servers/friends`。
2. 填写 Minecraft 版本、加载器、加载器版本和玩家连接地址。
3. 选择需要同步的目录。
4. 停止 Minecraft 服务端，勾选“我确认服务端已经停止”。
5. 点击扫描差异，检查并修改每个文件的端侧分类。
6. 再次确认停服，点击发布正式版本。
7. 为玩家创建同步码；原始代码只显示一次，请立即复制。

未连接 MSLX 时，容器无法可靠读取宿主机或另一个容器中的进程状态，所以停服状态由管理员确认。服务端目录在 Compose 中使用 `:ro` 挂载，可避免同步服务误写源文件。MSLX 文件管理则通过 Daemon 官方 API 完成，属于明确的管理员操作。

### 5. 玩家连接

玩家在 PCL IO 中进入 `设置 → 启动设置 → PCL IO 客户端同步`，添加：

- 同步源：此服务的外部根地址，例如 `https://sync.example.com`。
- 同步码：管理员为该玩家创建的长期代码。

不要在地址后手动添加 API 路径。PCL IO 会自动请求 `/api/plugins/mslx-plugin-iota-sync/sync/`，因此与原 MSLX 插件使用相同客户端。

## PassNat / FRP

将外部 TCP HTTP(S) 端口映射到 Docker 主机的 `8080`。若映射地址为 `http://example.passnat.com:12345`，玩家就填写该完整根地址。

公网部署建议使用 Nginx、Caddy 或其他反向代理启用 HTTPS。纯 HTTP 会使管理员密码或同步码存在旁路监听风险。

## 持久数据

| 容器路径 | 内容 |
| --- | --- |
| `/data/state.json` | 实例设置、发布记录和同步码哈希 |
| `/data/releases` | 各实例不可变发布快照 |
| `/data/launcher` | PCL IO 正式版更新文件 |
| `/servers/*` | 只读挂载的 Minecraft 服务端目录 |

备份宿主机的 `iota-sync-docker/data` 即可恢复。升级或重建容器不会删除这里的数据。

## 更新

```bash
git switch docker
git pull
docker compose -f iota-sync-docker/compose.yml up -d --build
```

停止服务：

```bash
docker compose -f iota-sync-docker/compose.yml down
```

## 从源码构建镜像

```bash
docker build -f iota-sync-docker/Dockerfile -t iota-sync:1.0.0 .
```

镜像基于 ASP.NET Core 10，目标平台为 Linux amd64。每次推送 `docker` 分支时，GitHub Actions 也会构建并发布 GHCR 镜像。

## 安全建议

- `IOTA_ADMIN_PASSWORD` 至少 12 位，建议使用密码管理器生成的 20 位以上随机强密码。
- 不要把真实账号密码写入 Git；提交前应保留 Compose 中的占位符。
- 不要把 MSLX API Key 写入 Compose、截图或日志；只存放在未提交的 `.env` 中。
- 管理端会话使用签名的 HttpOnly、SameSite=Strict Cookie；修改账号或密码并重启容器后，已有会话会自动失效。
- 登录接口按来源限制为每分钟最多 5 次尝试。
- 每位玩家使用独立同步码，以便单独撤销。
- 管理页面和同步接口经公网访问时必须优先配置 HTTPS。
- 只挂载需要扫描的服务端目录，且使用只读 `:ro`。
- 定期备份 `/data`，特别是在批量撤销代码或删除实例前。

更完整的容器内部说明见 [`iota-sync-docker/README.md`](iota-sync-docker/README.md)。
