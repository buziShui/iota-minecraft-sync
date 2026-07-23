using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MSLX.Plugin.IotaSync;

/// <summary>
/// Reads the standard metadata embedded in a mod JAR and determines where it is intended to run.
/// Ambiguous or contradictory metadata is deliberately left for administrator review.
/// </summary>
public static partial class ModSideDetector
{
    private const long MaxMetadataBytes = 4 * 1024 * 1024;

    public static (FileSide side, ModRuntimeSide runtimeSide, string reason) Detect(string path)
    {
        if (!path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            return (FileSide.Required, ModRuntimeSide.Unknown, "同步目录文件");

        try
        {
            using var zip = ZipFile.OpenRead(path);

            var fabric = FindEntry(zip, "fabric.mod.json");
            if (fabric is not null)
                return DetectJsonEnvironment(fabric, false);

            var quilt = FindEntry(zip, "quilt.mod.json");
            if (quilt is not null)
                return DetectJsonEnvironment(quilt, true);

            var neoForge = FindEntry(zip, "META-INF/neoforge.mods.toml");
            if (neoForge is not null)
                return DetectToml(ReadEntry(neoForge), "NeoForge", "neoforge");

            var forge = FindEntry(zip, "META-INF/mods.toml");
            if (forge is not null)
                return DetectToml(ReadEntry(forge), "Forge", "forge");

            var manifest = FindEntry(zip, "META-INF/MANIFEST.MF");
            if (manifest is not null)
            {
                var result = DetectManifest(ReadEntry(manifest));
                if (result is not null) return result.Value;
            }
        }
        catch (Exception exception) when (exception is IOException
                                          or InvalidDataException
                                          or UnauthorizedAccessException
                                          or JsonException)
        {
            return (FileSide.Unreviewed, ModRuntimeSide.Unknown, "无法读取 JAR 元数据，请人工确认");
        }

        return (FileSide.Unreviewed, ModRuntimeSide.Unknown, "未发现明确的端侧声明，请人工确认");
    }

    private static (FileSide side, ModRuntimeSide runtimeSide, string reason) DetectJsonEnvironment(ZipArchiveEntry entry, bool quilt)
    {
        using var document = JsonDocument.Parse(ReadEntry(entry));
        var environment = FindJsonEnvironment(document.RootElement, quilt);
        var loader = quilt ? "Quilt" : "Fabric";

        // Fabric Loader and Quilt Loader both treat an omitted environment as universal.
        if (environment is null)
            return (FileSide.Required, ModRuntimeSide.Both, $"{loader} 未声明 environment，按规范默认客户端与服务端");

        return environment.Trim().ToLowerInvariant() switch
        {
            "client" => (FileSide.Required, ModRuntimeSide.Client, $"{loader} environment 声明仅客户端"),
            "server" or "dedicated_server" => (FileSide.ServerOnly, ModRuntimeSide.Server, $"{loader} environment 声明仅服务端"),
            "*" or "both" or "universal" => (FileSide.Required, ModRuntimeSide.Both, $"{loader} environment 声明客户端与服务端"),
            _ => (FileSide.Unreviewed, ModRuntimeSide.Unknown, $"{loader} environment 值无法识别，请人工确认")
        };
    }

    private static string? FindJsonEnvironment(JsonElement root, bool quilt)
    {
        if (!quilt)
            return TryReadString(root, "environment");

        var environment = TryReadNestedString(root, "minecraft", "environment");
        if (environment is not null) return environment;

        // Support QMJ variants which put the Minecraft block below quilt_loader.
        if (root.TryGetProperty("quilt_loader", out var loader) && loader.ValueKind == JsonValueKind.Object)
        {
            environment = TryReadNestedString(loader, "minecraft", "environment");
            if (environment is not null) return environment;

            if (loader.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
                return TryReadNestedString(metadata, "minecraft", "environment");
        }

        return null;
    }

    private static (FileSide side, ModRuntimeSide runtimeSide, string reason) DetectToml(string text, string loaderName, string loaderModId)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(StripTomlComment).ToList();
        var uncommented = string.Join("\n", lines);

        if (ClientOnlyFlagRegex().IsMatch(uncommented))
            return (FileSide.Required, ModRuntimeSide.Client, $"{loaderName} 元数据声明仅客户端");
        if (ServerOnlyFlagRegex().IsMatch(uncommented))
            return (FileSide.ServerOnly, ModRuntimeSide.Server, $"{loaderName} 元数据声明仅服务端");

        var dependencies = ParseDependencySides(lines);

        // A dependency on Forge/NeoForge itself is the strongest side signal used by the
        // metadata convention. Minecraft is the next-best standard signal.
        var loaderSides = dependencies
            .Where(x => x.Required && x.ModId.Equals(loaderModId, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Side)
            .ToList();
        var minecraftSides = dependencies
            .Where(x => x.Required && x.ModId.Equals("minecraft", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Side)
            .ToList();
        var standardSides = loaderSides.Concat(minecraftSides).ToList();
        var standardSource = loaderSides.Count > 0 && minecraftSides.Count > 0
            ? "加载器/Minecraft 依赖"
            : loaderSides.Count > 0
                ? $"{loaderModId} 加载器依赖"
                : "Minecraft 依赖";
        var result = EvaluateSides(standardSides, loaderName, standardSource);
        if (result is not null) return result.Value;

        // Some projects omit the loader dependency but consistently annotate all required
        // dependencies. Only accept this fallback when every declaration agrees.
        var requiredSides = dependencies.Where(x => x.Required).Select(x => x.Side).ToList();
        result = EvaluateSides(requiredSides, loaderName, "依赖端侧声明");
        if (result is not null) return result.Value;

        if (IgnoreServerVersionRegex().IsMatch(uncommented))
            return (FileSide.Unreviewed, ModRuntimeSide.Unknown, $"{loaderName} displayTest 不足以确定运行端，请人工确认");

        return dependencies.Count > 0
            ? (FileSide.Unreviewed, ModRuntimeSide.Unknown, $"{loaderName} 端侧声明存在冲突，请人工确认")
            : (FileSide.Unreviewed, ModRuntimeSide.Unknown, $"{loaderName} 未发现明确端侧声明，请人工确认");
    }

    private static List<DependencySide> ParseDependencySides(IEnumerable<string> lines)
    {
        var result = new List<DependencySide>();
        DependencyBuilder? current = null;

        foreach (var line in lines)
        {
            var table = ArrayTableRegex().Match(line);
            if (table.Success)
            {
                Commit(current, result);
                var name = table.Groups["name"].Value.Trim();
                current = name.StartsWith("dependencies.", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("dependencies", StringComparison.OrdinalIgnoreCase)
                    ? new DependencyBuilder()
                    : null;
                continue;
            }

            if (current is null) continue;
            var pair = TomlPairRegex().Match(line);
            if (!pair.Success) continue;

            var key = pair.Groups["key"].Value;
            var value = UnquoteTomlValue(pair.Groups["value"].Value);
            if (key.Equals("modId", StringComparison.OrdinalIgnoreCase))
                current.ModId = value;
            else if (key.Equals("side", StringComparison.OrdinalIgnoreCase))
                current.Side = NormalizeSide(value);
            else if (key.Equals("mandatory", StringComparison.OrdinalIgnoreCase)
                     || key.Equals("required", StringComparison.OrdinalIgnoreCase))
                current.Required = !value.Equals("false", StringComparison.OrdinalIgnoreCase);
            else if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
                current.Required = !value.Equals("optional", StringComparison.OrdinalIgnoreCase)
                                   && !value.Equals("incompatible", StringComparison.OrdinalIgnoreCase)
                                   && !value.Equals("discouraged", StringComparison.OrdinalIgnoreCase);
        }

        Commit(current, result);
        return result;
    }

    private static void Commit(DependencyBuilder? dependency, ICollection<DependencySide> result)
    {
        if (dependency is not null
            && !string.IsNullOrWhiteSpace(dependency.ModId)
            && dependency.Side is not null)
            result.Add(new DependencySide(dependency.ModId, dependency.Side, dependency.Required));
    }

    private static (FileSide side, ModRuntimeSide runtimeSide, string reason)? EvaluateSides(
        IReadOnlyCollection<string> sides,
        string loaderName,
        string source)
    {
        if (sides.Count == 0) return null;
        var distinct = sides.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinct.Count != 1)
            return (FileSide.Unreviewed, ModRuntimeSide.Unknown, $"{loaderName} {source}存在冲突，请人工确认");

        return distinct[0] switch
        {
            "CLIENT" => (FileSide.Required, ModRuntimeSide.Client, $"{loaderName} {source}声明仅客户端"),
            "SERVER" => (FileSide.ServerOnly, ModRuntimeSide.Server, $"{loaderName} {source}声明仅服务端"),
            "BOTH" => (FileSide.Required, ModRuntimeSide.Both, $"{loaderName} {source}声明客户端与服务端"),
            _ => (FileSide.Unreviewed, ModRuntimeSide.Unknown, $"{loaderName} {source}无法识别，请人工确认")
        };
    }

    private static (FileSide side, ModRuntimeSide runtimeSide, string reason)? DetectManifest(string text)
    {
        if (ManifestClientOnlyRegex().IsMatch(text))
            return (FileSide.Required, ModRuntimeSide.Client, "JAR Manifest 声明仅客户端");
        if (ManifestServerOnlyRegex().IsMatch(text))
            return (FileSide.ServerOnly, ModRuntimeSide.Server, "JAR Manifest 声明仅服务端");
        return null;
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive zip, string name) =>
        zip.Entries.FirstOrDefault(x => string.Equals(x.FullName, name, StringComparison.OrdinalIgnoreCase));

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxMetadataBytes)
            throw new InvalidDataException("Mod metadata is unexpectedly large.");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static string? TryReadString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(property, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? TryReadNestedString(JsonElement element, string container, string property)
    {
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(container, out var nested)
               && nested.ValueKind == JsonValueKind.Object
            ? TryReadString(nested, property)
            : null;
    }

    private static string StripTomlComment(string line)
    {
        var inBasicString = false;
        var inLiteralString = false;
        var escaped = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\' && inBasicString)
            {
                escaped = true;
                continue;
            }

            if (character == '"' && !inLiteralString)
                inBasicString = !inBasicString;
            else if (character == '\'' && !inBasicString)
                inLiteralString = !inLiteralString;
            else if (character == '#' && !inBasicString && !inLiteralString)
                return line[..index];
        }

        return line;
    }

    private static string UnquoteTomlValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2
            && ((trimmed[0] == '"' && trimmed[^1] == '"')
                || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
            return trimmed[1..^1].Trim();
        return trimmed;
    }

    private static string? NormalizeSide(string side)
    {
        return side.Trim().ToUpperInvariant() switch
        {
            "CLIENT" => "CLIENT",
            "SERVER" => "SERVER",
            "BOTH" => "BOTH",
            _ => null
        };
    }

    private sealed record DependencySide(string ModId, string Side, bool Required);

    private sealed class DependencyBuilder
    {
        public string ModId { get; set; } = "";
        public string? Side { get; set; }
        public bool Required { get; set; } = true;
    }

    [GeneratedRegex(@"(?im)^\s*clientSideOnly\s*=\s*true\s*$")]
    private static partial Regex ClientOnlyFlagRegex();

    [GeneratedRegex(@"(?im)^\s*serverSideOnly\s*=\s*true\s*$")]
    private static partial Regex ServerOnlyFlagRegex();

    [GeneratedRegex(@"(?im)^\s*displayTest\s*=\s*[""']IGNORE_SERVER_VERSION[""']\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex IgnoreServerVersionRegex();

    [GeneratedRegex(@"^\s*\[\[\s*(?<name>[^\]]+?)\s*\]\]\s*$")]
    private static partial Regex ArrayTableRegex();

    [GeneratedRegex(@"^\s*(?<key>[A-Za-z][A-Za-z0-9_-]*)\s*=\s*(?<value>.+?)\s*$")]
    private static partial Regex TomlPairRegex();

    [GeneratedRegex(@"(?im)^\s*FMLClientSideOnly\s*:\s*true\s*$")]
    private static partial Regex ManifestClientOnlyRegex();

    [GeneratedRegex(@"(?im)^\s*FMLServerSideOnly\s*:\s*true\s*$")]
    private static partial Regex ManifestServerOnlyRegex();
}
