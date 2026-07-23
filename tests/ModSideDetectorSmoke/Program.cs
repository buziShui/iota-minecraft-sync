using System.IO.Compression;
using MSLX.Plugin.IotaSync;

if (args.Length > 0)
{
    foreach (var path in args)
    {
        var result = ModSideDetector.Detect(path);
        Console.WriteLine($"{Path.GetFileName(path)}\t{result.runtimeSide}\t{result.side}\t{result.reason}");
    }
    return;
}

var cases = new[]
{
    new DetectorCase("Fabric client", "fabric.mod.json", """{"schemaVersion":1,"id":"sample","environment":"client"}""", FileSide.Required, ModRuntimeSide.Client),
    new DetectorCase("Fabric server", "fabric.mod.json", """{"schemaVersion":1,"id":"sample","environment":"server"}""", FileSide.ServerOnly, ModRuntimeSide.Server),
    new DetectorCase("Fabric universal", "fabric.mod.json", """{"schemaVersion":1,"id":"sample","environment":"*"}""", FileSide.Required, ModRuntimeSide.Both),
    new DetectorCase("Fabric default universal", "fabric.mod.json", """{"schemaVersion":1,"id":"sample"}""", FileSide.Required, ModRuntimeSide.Both),
    new DetectorCase("Quilt client", "quilt.mod.json", """{"schema_version":1,"minecraft":{"environment":"client"}}""", FileSide.Required, ModRuntimeSide.Client),
    new DetectorCase("NeoForge client", "META-INF/neoforge.mods.toml", """
        modLoader="javafml"
        # side="SERVER"
        [[dependencies.appleskin]]
        modId="neoforge"
        type="required"
        side="CLIENT"
        """, FileSide.Required, ModRuntimeSide.Client),
    new DetectorCase("NeoForge server", "META-INF/neoforge.mods.toml", """
        modLoader="javafml"
        [[dependencies.sample]]
        modId="neoforge"
        type="required"
        side="SERVER"
        """, FileSide.ServerOnly, ModRuntimeSide.Server),
    new DetectorCase("NeoForge both", "META-INF/neoforge.mods.toml", """
        modLoader="javafml"
        [[dependencies.sample]]
        modId="neoforge"
        type="required"
        side="BOTH"
        [[dependencies.sample]]
        modId="minecraft"
        type="required"
        side="BOTH"
        """, FileSide.Required, ModRuntimeSide.Both),
    new DetectorCase("Forge optional dependency ignored", "META-INF/mods.toml", """
        modLoader="javafml"
        [[dependencies.sample]]
        modId="forge"
        mandatory=true
        side="BOTH"
        [[dependencies.sample]]
        modId="clienthelper"
        mandatory=false
        side="CLIENT"
        """, FileSide.Required, ModRuntimeSide.Both),
    new DetectorCase("Forge conflicting loader declarations", "META-INF/mods.toml", """
        modLoader="javafml"
        [[dependencies.first]]
        modId="forge"
        mandatory=true
        side="CLIENT"
        [[dependencies.second]]
        modId="forge"
        mandatory=true
        side="SERVER"
        """, FileSide.Unreviewed, ModRuntimeSide.Unknown),
    new DetectorCase("Forge conflicting standard dependencies", "META-INF/mods.toml", """
        modLoader="javafml"
        [[dependencies.sample]]
        modId="forge"
        mandatory=true
        side="BOTH"
        [[dependencies.sample]]
        modId="minecraft"
        mandatory=true
        side="CLIENT"
        """, FileSide.Unreviewed, ModRuntimeSide.Unknown),
    new DetectorCase("Manifest server", "META-INF/MANIFEST.MF", """
        Manifest-Version: 1.0
        FMLServerSideOnly: true
        """, FileSide.ServerOnly, ModRuntimeSide.Server),
    new DetectorCase("Unknown metadata", "META-INF/example.txt", "no side metadata", FileSide.Unreviewed, ModRuntimeSide.Unknown)
};

var root = Path.Combine(Path.GetTempPath(), "iota-mod-side-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    foreach (var test in cases)
    {
        var jar = Path.Combine(root, test.Name.Replace(' ', '-') + ".jar");
        using (var zip = ZipFile.Open(jar, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry(test.Entry);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(test.Content);
        }

        var result = ModSideDetector.Detect(jar);
        if (result.side != test.ExpectedPolicy || result.runtimeSide != test.ExpectedRuntime)
            throw new InvalidOperationException(
                $"{test.Name}: expected {test.ExpectedRuntime}/{test.ExpectedPolicy}, received {result.runtimeSide}/{result.side} ({result.reason})");

        Console.WriteLine($"PASS {test.Name}: {result.runtimeSide}/{result.side} — {result.reason}");
    }

    Console.WriteLine($"All {cases.Length} mod side detection cases passed.");
}
finally
{
    Directory.Delete(root, true);
}

internal sealed record DetectorCase(
    string Name,
    string Entry,
    string Content,
    FileSide ExpectedPolicy,
    ModRuntimeSide ExpectedRuntime);
