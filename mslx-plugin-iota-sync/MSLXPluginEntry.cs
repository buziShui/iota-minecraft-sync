using MSLX.SDK;
using MSLX.SDK.Interfaces;

namespace MSLX.Plugin.IotaSync;

public sealed class MSLXPluginEntry : IPlugin
{
    public static MSLXPluginEntry Instance { get; private set; } = null!;
    public string Id => "mslx-plugin-iota-sync";
    public string Name => "Iota 客户端同步";
    public string Description => "用双列表批量整理 Mod，为多个 MSLX 实例发布可校验、可回滚的客户端同步版本。";
    public string Version => "1.2.2";
    public string Icon => "icon.png";
    public string MinSDKVersion => "1.4.7";
    public string Developer => "BuZiShui";
    public string AuthorUrl => "https://github.com/buziShui";
    public string PluginUrl => "https://github.com/buziShui/iota-minecraft-sync";

    public void OnLoad()
    {
        Instance = this;
        Directory.CreateDirectory(this.Config().GetDataPath());
        SyncStore.Initialize(this.Config().GetDataPath());
        SDK.MSLX.Logger.Info("Iota 客户端同步插件已加载；首次打开管理页时将执行一次安全扫描。");
    }

    public void OnUnload() => SDK.MSLX.Logger.Info("Iota 客户端同步插件已卸载。");
}
