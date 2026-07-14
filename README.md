# Plain Craft Launcher (PCL) IO

这是 BuZiShui 基于 Plain Craft Launcher 独立进行的第三方二次创作版本，为 MSLX Iota 同步插件提供启动前客户端同步能力。

原项目作者：龙腾猫跃。请优先支持原作者：[赞助 PCL](https://meloong.com/afd/a/LTCat)。

本项目保留原仓库的 `LICENCE`，分发和修改前请完整阅读。该衍生版本与 PCL 官方无关。

## 当前同步能力

- 自定义添加 `HTTP 地址 + 长期同步码`。
- 点击启动时获取发布清单并按 SHA-256 差分同步。
- 只删除此前由同步器管理、且远端已经移除的文件。
- 保留玩家自行添加的 Mod。
- 本地配置发生冲突时由玩家选择覆盖或保留。
- 更新前复制到游戏实例内的 `.iota-backup` 目录。
- 每个服务器使用独立版本目录。

客户端源管理入口位于 `设置 → 启动设置 → PCL IO 客户端同步`。
