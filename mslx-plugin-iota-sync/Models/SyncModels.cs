namespace MSLX.Plugin.IotaSync;

public enum FileSide { Required, Optional, ServerOnly, Unreviewed }

public sealed record SyncDirectory(string Path, bool Enabled);
public sealed record ScanFile(string Path, long Size, string Sha256, FileSide Side, string Reason);
public sealed record SyncManifest(string Protocol, int InstanceId, string InstanceName, string ReleaseId,
    DateTimeOffset PublishedAt, string MinecraftVersion, string Loader, string LoaderVersion,
    string? ServerAddress, IReadOnlyList<ScanFile> Files);

public sealed class InstanceSettings
{
    public int InstanceId { get; set; }
    public List<SyncDirectory> Directories { get; set; } =
        [new("mods", true), new("config", false), new("defaultconfigs", false), new("kubejs", false),
         new("resourcepacks", false), new("shaderpacks", false)];
    public Dictionary<string, FileSide> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string MinecraftVersion { get; set; } = "";
    public string Loader { get; set; } = "";
    public string LoaderVersion { get; set; } = "";
    public string? ServerAddress { get; set; }
    public List<ScanFile> LastScan { get; set; } = [];
    public List<string> Releases { get; set; } = [];
}

public sealed class AccessCode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int InstanceId { get; set; }
    public string Name { get; set; } = "";
    public string Salt { get; set; } = "";
    public string Hash { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PluginState
{
    public Dictionary<int, InstanceSettings> Instances { get; set; } = [];
    public List<AccessCode> Codes { get; set; } = [];
}
