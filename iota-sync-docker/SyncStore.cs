using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace IotaSync.Docker;

public sealed class SyncStore
{
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public string Root { get; }
    public string ReleasesRoot => Path.Combine(Root, "releases");
    public string LauncherRoot => Path.Combine(Root, "launcher");
    public AppState State { get; private set; }

    public SyncStore(IConfiguration configuration)
    {
        Root = Path.GetFullPath(configuration["IOTA_DATA"] ?? "/data");
        Directory.CreateDirectory(Root); Directory.CreateDirectory(ReleasesRoot);
        var stateFile = Path.Combine(Root, "state.json");
        State = File.Exists(stateFile) ? JsonSerializer.Deserialize<AppState>(File.ReadAllText(stateFile), _json) ?? new() : new();
    }

    public void Save() { lock (_gate) AtomicWrite(Path.Combine(Root, "state.json"), JsonSerializer.Serialize(State, _json)); }
    public void WriteJson(string path, object value) => AtomicWrite(path, JsonSerializer.Serialize(value, _json));
    private static void AtomicWrite(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); var temp = path + ".tmp";
        File.WriteAllText(temp, content); File.Move(temp, path, true);
    }

    public static string Sha256File(string path)
    { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }

    public static (FileSide Side, string Reason) DetectSide(string path)
    {
        if (!path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) return (FileSide.Required, "同步目录文件");
        try
        {
            using var zip = ZipFile.OpenRead(path);
            foreach (var name in new[] { "fabric.mod.json", "META-INF/mods.toml", "META-INF/neoforge.mods.toml" })
            {
                var entry = zip.GetEntry(name); if (entry is null) continue;
                using var reader = new StreamReader(entry.Open()); var text = reader.ReadToEnd();
                if (text.Contains("\"environment\"", StringComparison.OrdinalIgnoreCase) && text.Contains("client", StringComparison.OrdinalIgnoreCase)) return (FileSide.Optional, "元数据声明客户端环境");
                if (text.Contains("serverSideOnly", StringComparison.OrdinalIgnoreCase) || text.Contains("dedicated_server", StringComparison.OrdinalIgnoreCase)) return (FileSide.ServerOnly, "元数据疑似仅服务端");
            }
        }
        catch { return (FileSide.Unreviewed, "无法读取 JAR 元数据，请人工确认"); }
        return (FileSide.Unreviewed, "未发现明确端侧声明，请人工确认");
    }

    public (AccessCode Model, string Raw) CreateCode(int instanceId, string name)
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(); var salt = RandomNumberGenerator.GetBytes(16);
        var model = new AccessCode { InstanceId = instanceId, Name = name, Salt = Convert.ToBase64String(salt), Hash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(raw, salt, 120_000, HashAlgorithmName.SHA256, 32)) };
        State.Codes.Add(model); Save(); return (model, raw);
    }

    public bool VerifyCode(string raw, out AccessCode code)
    {
        code = null!;
        foreach (var candidate in State.Codes.Where(x => x.Enabled))
        {
            var hash = Rfc2898DeriveBytes.Pbkdf2(raw, Convert.FromBase64String(candidate.Salt), 120_000, HashAlgorithmName.SHA256, 32);
            if (CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(candidate.Hash))) { code = candidate; return true; }
        }
        return false;
    }

    public string SafeReleaseFile(int instanceId, string releaseId, string relative)
    {
        var root = Path.GetFullPath(Path.Combine(ReleasesRoot, instanceId.ToString(), releaseId));
        var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("非法文件路径");
        return path;
    }
}
