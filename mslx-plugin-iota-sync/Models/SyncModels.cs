namespace MSLX.Plugin.IotaSync;

// FileSide is the delivery policy kept under the legacy JSON field name "side"
// for compatibility with released PCL IO clients.
public enum FileSide { Required, Optional, ServerOnly, Unreviewed }
public enum ModRuntimeSide { Unknown, Client, Server, Both }

public sealed record SyncDirectory(string Path, bool Enabled);
public sealed record ScanFile(
    string Path,
    long Size,
    string Sha256,
    FileSide Side,
    string Reason,
    FileSide? DetectedSide = null,
    ModRuntimeSide RuntimeSide = ModRuntimeSide.Unknown);
public sealed record SyncManifest(string Protocol, int InstanceId, string InstanceName, string ReleaseId,
    DateTimeOffset PublishedAt, string MinecraftVersion, string Loader, string LoaderVersion,
    string? ServerAddress, IReadOnlyList<ScanFile> Files);

public sealed class InstanceSettingsUpdate
{
    public int? InstanceId { get; set; }
    public List<SyncDirectory> Directories { get; set; } = [];
    public Dictionary<string, FileSide> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string MinecraftVersion { get; set; } = "";
    public string Loader { get; set; } = "";
    public string LoaderVersion { get; set; } = "";
    public string? ServerAddress { get; set; }
}

public sealed class InstanceSettings
{
    public static readonly string[] SupportedDirectoryPaths =
        ["mods", "config", "defaultconfigs", "kubejs", "resourcepacks", "shaderpacks"];

    public int InstanceId { get; set; }
    // 必须保持空集合。MSLX 宿主启用了集合填充式 JSON 反序列化，
    // 若在属性初始化器中放默认值，每次 PUT 都会把客户端数组追加到默认值后面。
    public List<SyncDirectory> Directories { get; set; } = [];
    public Dictionary<string, FileSide> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string MinecraftVersion { get; set; } = "";
    public string Loader { get; set; } = "";
    public string LoaderVersion { get; set; } = "";
    public string? ServerAddress { get; set; }
    public List<ScanFile> LastScan { get; set; } = [];
    public List<string> Releases { get; set; } = [];

    public static InstanceSettings CreateDefault(int instanceId) => new()
    {
        InstanceId = instanceId,
        Directories = CreateDefaultDirectories()
    };

    public static List<SyncDirectory> CreateDefaultDirectories() =>
        SupportedDirectoryPaths.Select((path, index) => new SyncDirectory(path, index == 0)).ToList();

    public static List<SyncDirectory> NormalizeDirectories(IEnumerable<SyncDirectory>? directories)
    {
        var source = directories?
            .Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .GroupBy(x => x.Path.Trim(), StringComparer.OrdinalIgnoreCase)
            // 集合填充式反序列化会把客户端的新值追加到旧默认值之后，应保留最后一次选择。
            .ToDictionary(x => x.Key, x => x.Last().Enabled, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        if (source.Count == 0) return CreateDefaultDirectories();
        return SupportedDirectoryPaths
            .Select(path => new SyncDirectory(path, source.GetValueOrDefault(path)))
            .ToList();
    }

    public static List<ScanFile> NormalizeScanFiles(IEnumerable<ScanFile>? files) =>
        files?
            .Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .OrderBy(x => x.Path, StringComparer.Ordinal)
            .ToList()
        ?? [];

    public void ApplyEditable(InstanceSettingsUpdate input)
    {
        Directories = NormalizeDirectories(input.Directories);
        Overrides = new Dictionary<string, FileSide>(
            (input.Overrides ?? new Dictionary<string, FileSide>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && Enum.IsDefined(x.Value)),
            StringComparer.OrdinalIgnoreCase);
        MinecraftVersion = input.MinecraftVersion?.Trim() ?? "";
        Loader = input.Loader?.Trim() ?? "";
        LoaderVersion = input.LoaderVersion?.Trim() ?? "";
        ServerAddress = string.IsNullOrWhiteSpace(input.ServerAddress) ? null : input.ServerAddress.Trim();

        LastScan = NormalizeScanFiles(LastScan.Select(file =>
        {
            var detectedSide = file.DetectedSide ?? file.Side;
            var side = Overrides.TryGetValue(file.Path, out var overridden) && overridden != FileSide.Unreviewed
                ? overridden
                : detectedSide;
            return file with { Side = side };
        }));
    }
}

public sealed class AccessCode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    // null means that the same code may select any published instance.
    public int? InstanceId { get; set; }
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
