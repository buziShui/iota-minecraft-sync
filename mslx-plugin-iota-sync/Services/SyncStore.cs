using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MSLX.Plugin.IotaSync;

public static class SyncStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static string _root = "";
    private static PluginState _state = new();
    public static PluginState State { get { lock (Gate) return _state; } }

    public static void Initialize(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(ReleasesRoot);
        var file = Path.Combine(_root, "state.json");
        if (File.Exists(file)) _state = JsonSerializer.Deserialize<PluginState>(File.ReadAllText(file), Json) ?? new();
    }

    public static string ReleasesRoot => Path.Combine(_root, "releases");
    public static string LauncherRoot => Path.Combine(_root, "launcher");
    public static void Save() { lock (Gate) File.WriteAllText(Path.Combine(_root, "state.json"), JsonSerializer.Serialize(_state, Json)); }

    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static (FileSide side, string reason) DetectSide(string path)
    {
        if (!path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) return (FileSide.Required, "同步目录文件");
        try
        {
            using var zip = ZipFile.OpenRead(path);
            foreach (var name in new[] { "fabric.mod.json", "META-INF/mods.toml", "META-INF/neoforge.mods.toml" })
            {
                var entry = zip.GetEntry(name); if (entry is null) continue;
                using var reader = new StreamReader(entry.Open());
                var text = reader.ReadToEnd();
                if (text.Contains("\"environment\"", StringComparison.OrdinalIgnoreCase) && text.Contains("client", StringComparison.OrdinalIgnoreCase)) return (FileSide.Optional, "元数据声明客户端环境");
                if (text.Contains("serverSideOnly", StringComparison.OrdinalIgnoreCase) || text.Contains("dedicated_server", StringComparison.OrdinalIgnoreCase)) return (FileSide.ServerOnly, "元数据疑似仅服务端");
            }
        }
        catch { return (FileSide.Unreviewed, "无法读取 JAR 元数据，请人工确认"); }
        return (FileSide.Unreviewed, "未发现明确的端侧声明，请人工确认");
    }

    public static bool VerifyCode(string raw, out AccessCode code)
    {
        code = null!;
        foreach (var candidate in State.Codes.Where(x => x.Enabled))
        {
            var hash = Rfc2898DeriveBytes.Pbkdf2(raw, Convert.FromBase64String(candidate.Salt), 120_000, HashAlgorithmName.SHA256, 32);
            if (CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(candidate.Hash))) { code = candidate; return true; }
        }
        return false;
    }

    public static (AccessCode model, string raw) CreateCode(int instanceId, string name)
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var salt = RandomNumberGenerator.GetBytes(16);
        var model = new AccessCode { InstanceId = instanceId, Name = name, Salt = Convert.ToBase64String(salt), Hash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(raw, salt, 120_000, HashAlgorithmName.SHA256, 32)) };
        State.Codes.Add(model); Save(); return (model, raw);
    }

    public static string SafeReleaseFile(int instanceId, string releaseId, string relative)
    {
        var root = Path.GetFullPath(Path.Combine(ReleasesRoot, instanceId.ToString(), releaseId));
        var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("非法文件路径");
        return path;
    }
}
