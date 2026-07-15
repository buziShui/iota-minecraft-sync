# Iota Sync 独立 Docker 版

这是完全脱离 MSLX 的独立同步服务，自带中文 Web 管理页，并保持 PCL IO 的 `iota-sync/1` 客户端协议兼容。

## 快速部署

1. 复制 `iota-sync-docker/.env.example` 为 `iota-sync-docker/.env`，设置管理员账号，并将 `IOTA_ADMIN_PASSWORD` 改为至少 12 位、不可猜测的强密码。
2. 将每个 Minecraft 服务端目录以只读方式挂载到 `/servers` 下。例如宿主机 `/opt/minecraft/friends` 映射为容器内 `/servers/friends`。
3. 启动：

```bash
docker compose -f iota-sync-docker/compose.yml up -d --build
```

4. 浏览器访问 `http://服务器地址:8080`，输入管理员账号和密码。登录状态由 30 天有效的 HttpOnly Cookie 保持。
5. 添加实例时填写名称和容器内路径（例如 `/servers/friends`），配置 MC/加载器版本及玩家连接地址。
6. 停止 Minecraft 服务端，勾选停服确认，扫描并人工检查分类。
7. 再次勾选停服确认并发布，随后为玩家创建长期同步码。
8. PCL IO 中的同步源填写此服务的外部根地址及同步码。

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
- Minecraft 目录只读挂载；服务只读取源文件并把发布快照写入 `/data`。
- 独立容器无法可靠检测宿主机 Minecraft 进程，扫描与发布前必须由管理员确认已经停服。
