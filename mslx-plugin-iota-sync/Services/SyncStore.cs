using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace MSLX.Plugin.IotaSync;

public static class SyncStore
{
    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<int, object> InstanceGates = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static string _root = "";
    private static PluginState _state = new();

    public static void Initialize(string root)
    {
        lock (Gate)
        {
            _root = Path.GetFullPath(root);
            Directory.CreateDirectory(ReleasesRoot);
            var file = Path.Combine(_root, "state.json");
            if (File.Exists(file)) _state = JsonSerializer.Deserialize<PluginState>(File.ReadAllText(file), Json) ?? new();
            NormalizeState();
            SaveUnsafe();
        }
    }

    public static string ReleasesRoot => Path.Combine(_root, "releases");
    public static object InstanceGate(int instanceId) => InstanceGates.GetOrAdd(instanceId, _ => new object());

    public static InstanceSettings? GetInstance(int instanceId)
    {
        lock (Gate)
        {
            return _state.Instances.TryGetValue(instanceId, out var settings) ? Clone(settings) : null;
        }
    }

    public static InstanceSettings UpdateInstance(int instanceId, bool create, Action<InstanceSettings> update)
    {
        lock (Gate)
        {
            var existed = _state.Instances.TryGetValue(instanceId, out var current);
            if (!existed && !create) throw new KeyNotFoundException($"实例 {instanceId} 尚未配置");
            var updated = existed ? Clone(current!) : InstanceSettings.CreateDefault(instanceId);
            update(updated);
            updated.InstanceId = instanceId;
            updated.Directories = InstanceSettings.NormalizeDirectories(updated.Directories);
            updated.LastScan = InstanceSettings.NormalizeScanFiles(updated.LastScan);
            updated.Releases = (updated.Releases ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Take(5).ToList();
            _state.Instances[instanceId] = updated;
            try
            {
                SaveUnsafe();
            }
            catch
            {
                if (existed) _state.Instances[instanceId] = current!;
                else _state.Instances.Remove(instanceId);
                throw;
            }
            return Clone(updated);
        }
    }

    public static IReadOnlyList<AccessCode> GetCodes()
    {
        lock (Gate) return _state.Codes.Select(Clone).ToList();
    }

    private static void NormalizeState()
    {
        foreach (var (instanceId, settings) in _state.Instances)
        {
            settings.InstanceId = instanceId;
            settings.Directories = InstanceSettings.NormalizeDirectories(settings.Directories);
            settings.LastScan = InstanceSettings.NormalizeScanFiles(settings.LastScan);
            settings.Overrides = new Dictionary<string, FileSide>(
                settings.Overrides ?? new Dictionary<string, FileSide>(),
                StringComparer.OrdinalIgnoreCase);
            settings.Releases = (settings.Releases ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToList();
        }
    }

    private static void SaveUnsafe()
    {
        var file = Path.Combine(_root, "state.json");
        var temporary = file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_state, Json));
        File.Move(temporary, file, true);
    }

    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static (FileSide side, ModRuntimeSide runtimeSide, string reason) DetectSide(string path)
        => ModSideDetector.Detect(path);

    public static bool VerifyCode(string raw, out AccessCode code)
    {
        code = null!;
        foreach (var candidate in GetCodes().Where(x => x.Enabled))
        {
            var hash = Rfc2898DeriveBytes.Pbkdf2(raw, Convert.FromBase64String(candidate.Salt), 120_000, HashAlgorithmName.SHA256, 32);
            if (CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(candidate.Hash))) { code = candidate; return true; }
        }
        return false;
    }

    public static (AccessCode model, string raw) CreateCode(int? instanceId, string name)
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var salt = RandomNumberGenerator.GetBytes(16);
        var model = new AccessCode { InstanceId = instanceId, Name = name, Salt = Convert.ToBase64String(salt), Hash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(raw, salt, 120_000, HashAlgorithmName.SHA256, 32)) };
        lock (Gate)
        {
            _state.Codes.Add(model);
            try { SaveUnsafe(); }
            catch { _state.Codes.Remove(model); throw; }
        }
        return (Clone(model), raw);
    }

    public static bool RevokeCode(string codeId)
    {
        lock (Gate)
        {
            var code = _state.Codes.FirstOrDefault(x => x.Id == codeId);
            if (code is null) return false;
            var previous = code.Enabled;
            code.Enabled = false;
            try { SaveUnsafe(); }
            catch { code.Enabled = previous; throw; }
            return true;
        }
    }

    public static bool DeleteCode(string codeId)
    {
        lock (Gate)
        {
            var index = _state.Codes.FindIndex(x => x.Id == codeId);
            if (index < 0) return false;
            var code = _state.Codes[index];
            _state.Codes.RemoveAt(index);
            try { SaveUnsafe(); }
            catch { _state.Codes.Insert(index, code); throw; }
            return true;
        }
    }

    public static string SafeReleaseFile(int instanceId, string releaseId, string relative)
    {
        var root = Path.GetFullPath(Path.Combine(ReleasesRoot, instanceId.ToString(), releaseId));
        return SafeChildFile(root, relative);
    }

    public static string SafeChildFile(string root, string relative)
    {
        root = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("非法文件路径");
        return path;
    }

    public static string CreateReleaseId() =>
        $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{RandomNumberGenerator.GetHexString(6).ToLowerInvariant()}";

    public static string SafeServerFile(string serverRoot, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new InvalidOperationException("非法服务端文件路径");

        var root = Path.GetFullPath(serverRoot);
        var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var relativeToRoot = Path.GetRelativePath(root, path);
        if (relativeToRoot.Equals("..", StringComparison.Ordinal)
            || relativeToRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relativeToRoot))
            throw new InvalidOperationException("服务端文件路径超出实例目录");

        // Do not allow a symlink/reparse-point component to redirect publication
        // outside the stopped server instance after the lexical boundary check.
        var current = root;
        foreach (var segment in relativeToRoot.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current) || Directory.Exists(current))
            {
                var attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("服务端文件路径不能包含符号链接");
            }
        }

        return path;
    }

    private static InstanceSettings Clone(InstanceSettings source) => new()
    {
        InstanceId = source.InstanceId,
        Directories = source.Directories.ToList(),
        Overrides = new Dictionary<string, FileSide>(source.Overrides, StringComparer.OrdinalIgnoreCase),
        MinecraftVersion = source.MinecraftVersion,
        Loader = source.Loader,
        LoaderVersion = source.LoaderVersion,
        ServerAddress = source.ServerAddress,
        LastScan = source.LastScan.ToList(),
        Releases = source.Releases.ToList()
    };

    private static AccessCode Clone(AccessCode source) => new()
    {
        Id = source.Id,
        InstanceId = source.InstanceId,
        Name = source.Name,
        Salt = source.Salt,
        Hash = source.Hash,
        Enabled = source.Enabled,
        CreatedAt = source.CreatedAt
    };
}
