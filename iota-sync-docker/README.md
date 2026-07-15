# Iota Sync × MSLX Docker 版

这是可独立运行、也可连接现有 MSLX Daemon 的同步服务，自带 MSLX 风格中文 Web 管理页，并保持 PCL IO 的 `iota-sync/1` 客户端协议兼容。

## 快速部署

1. 复制 `iota-sync-docker/.env.example` 为 `iota-sync-docker/.env`，设置管理员账号，并将 `IOTA_ADMIN_PASSWORD` 改为至少 12 位、不可猜测的强密码。
2. 将每个 Minecraft 服务端目录以只读方式挂载到 `/servers` 下。例如宿主机 `/opt/minecraft/friends` 映射为容器内 `/servers/friends`。
3. 启动：

```bash
docker compose -f iota-sync-docker/compose.yml up -d --build
```

4. 浏览器访问 `http://服务器地址:8080`，输入管理员账号和密码。登录状态由 30 天有效的 HttpOnly Cookie 保持。
5. 推荐在 `.env` 中配置 MSLX 连接参数，然后在“服务器控制”中关联实例。
6. 停止 Minecraft 服务端，扫描并人工检查分类；关联后系统会通过 MSLX 自动校验停服状态。
7. 发布正式版本，随后为玩家创建长期同步码。
8. PCL IO 中的同步源填写此服务的外部根地址及同步码。

## MSLX 集成

Iota 后端使用 MSLX 官方 API 读取实例状态、玩家、备份和文件，并代理启动、停止、重启、备份及文件管理操作。MSLX API Key 不会返回给前端。实例目录仍以只读方式挂载给 Iota 的扫描与发布模块：

```yaml
environment:
  IOTA_MSLX_BASE_URL: "${IOTA_MSLX_BASE_URL:-}"
  IOTA_MSLX_PUBLIC_URL: "${IOTA_MSLX_PUBLIC_URL:-}"
  IOTA_MSLX_API_KEY: "${IOTA_MSLX_API_KEY:-}"
  IOTA_MSLX_SERVERS_ROOT: "${IOTA_MSLX_SERVERS_ROOT:-/servers}"
volumes:
  - /vol1/@appshare/MSLX/Servers:/servers:ro
```

若两个容器不在同一 Docker 网络，可在 Compose 中增加 `host.docker.internal:host-gateway`，并把内部地址写成 `http://host.docker.internal:1027`。如果不配置 MSLX，所有原有同步功能仍可使用，但扫描与发布前需要手工确认停服。

## PassNat / FRP

将外部 HTTP(S) 端口映射到 Docker 主机的 `8080` 端口。客户端只填写根地址，例如 `https://sync.example.com`，不要附加 API 路径。公网使用强烈建议在反向代理处启用 HTTPS。

## 数据与备份

- `/data/state.json`：实例配置和同步码哈希。
- `/data/releases`：每个实例最近 5 个不可变发布快照。
- `/data/launcher`：PCL IO 更新文件。
- `/servers/*`：服务端目录，推荐只读挂载。

备份整个 `/data` 即可恢复同步服务。同步码原文不保存在服务端。

## 更新

```bash
git pull
docker compose -f iota-sync-docker/compose.yml up -d --build
```

持久数据位于宿主机的 `iota-sync-docker/data`，重建容器不会丢失。

## 安全注意

- 管理页通过账号密码登录；密码只用于服务端验证，不保存在浏览器本机存储中。
- 登录会话使用签名的 HttpOnly、SameSite=Strict Cookie。修改账号或密码并重启容器后，已有会话会自动失效。
- 登录接口按来源限制为每分钟最多 5 次尝试。
- 不要把管理员密码或玩家同步码写入公开仓库、日志或群聊截图。
- MSLX API Key 只放入未提交的 `.env`，不要写入公开仓库或浏览器配置。
- Minecraft 目录只读挂载；服务只读取源文件并把发布快照写入 `/data`。
- 未连接 MSLX 时，独立容器无法可靠检测宿主机 Minecraft 进程，扫描与发布前必须由管理员确认已经停服。
