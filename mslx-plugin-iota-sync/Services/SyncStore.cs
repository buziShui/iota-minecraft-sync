using System.IO.Compression;
using System.Security.Cryptography;
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
        lock (Gate)
        {
            _root = Path.GetFullPath(root);
            Directory.CreateDirectory(ReleasesRoot);
            var file = Path.Combine(_root, "state.json");
            if (File.Exists(file)) _state = JsonSerializer.Deserialize<PluginState>(File.ReadAllText(file), Json) ?? new();
        }
    }

    public static string ReleasesRoot => Path.Combine(_root, "releases");
    public static void Save()
    {
        lock (Gate)
        {
            var file = Path.Combine(_root, "state.json");
            var temporary = file + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_state, Json));
            File.Move(temporary, file, true);
        }
    }

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
            var fabric = FindEntry(zip, "fabric.mod.json");
            if (fabric is not null)
            {
                var result = DetectJsonEnvironment(fabric, false);
                if (result is not null) return result.Value;
            }

            var quilt = FindEntry(zip, "quilt.mod.json");
            if (quilt is not null)
            {
                var result = DetectJsonEnvironment(quilt, true);
                if (result is not null) return result.Value;
            }

            foreach (var name in new[] { "META-INF/mods.toml", "META-INF/neoforge.mods.toml" })
            {
                var entry = FindEntry(zip, name); if (entry is null) continue;
                var text = ReadEntry(entry);
                var compact = string.Concat(text.Where(x => !char.IsWhiteSpace(x))).ToLowerInvariant();
                if (compact.Contains("clientsideonly=true") || compact.Contains("displaytest=\"ignore_server_version\""))
                    return (FileSide.Optional, "Forge/NeoForge 元数据声明仅客户端");
                if (compact.Contains("serversideonly=true"))
                    return (FileSide.ServerOnly, "Forge/NeoForge 元数据声明仅服务端");
            }
        }
        catch { return (FileSide.Unreviewed, "无法读取 JAR 元数据，请人工确认"); }
        return (FileSide.Unreviewed, "未发现明确的端侧声明，请人工确认");
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive zip, string name) =>
        zip.Entries.FirstOrDefault(x => string.Equals(x.FullName, name, StringComparison.OrdinalIgnoreCase));

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static (FileSide side, string reason)? DetectJsonEnvironment(ZipArchiveEntry entry, bool quilt)
    {
        using var document = JsonDocument.Parse(ReadEntry(entry));
        var root = document.RootElement;
        JsonElement environment = default;
        bool found;
        if (quilt)
            found = root.TryGetProperty("minecraft", out var minecraft) && minecraft.ValueKind == JsonValueKind.Object && minecraft.TryGetProperty("environment", out environment);
        else
            found = root.TryGetProperty("environment", out environment);
        if (!found || environment.ValueKind != JsonValueKind.String) return null;
        return environment.GetString()?.ToLowerInvariant() switch
        {
            "client" => (FileSide.Optional, $"{(quilt ? "Quilt" : "Fabric")} 元数据声明仅客户端"),
            "server" or "dedicated_server" => (FileSide.ServerOnly, $"{(quilt ? "Quilt" : "Fabric")} 元数据声明仅服务端"),
            _ => null
        };
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
