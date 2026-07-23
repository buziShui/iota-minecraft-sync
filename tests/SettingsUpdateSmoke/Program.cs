using MSLX.Plugin.IotaSync;

var scanned = new ScanFile("mods/example.jar", 4, "hash", FileSide.Unreviewed, "unknown", FileSide.Unreviewed);
var settings = InstanceSettings.CreateDefault(7);
settings.LastScan = [scanned];
settings.Releases = ["20260723120000"];

settings.ApplyEditable(new InstanceSettingsUpdate
{
    InstanceId = 7,
    MinecraftVersion = " 1.21.1 ",
    Loader = " neoforge ",
    Directories = [new SyncDirectory("mods", true)],
    Overrides = new Dictionary<string, FileSide> { [scanned.Path] = FileSide.Required }
});

Assert(settings.LastScan.Count == 1, "scan result is preserved");
Assert(settings.Releases.SequenceEqual(["20260723120000"]), "release history is preserved");
Assert(settings.LastScan[0].Side == FileSide.Required, "override is applied server-side");
Assert(settings.MinecraftVersion == "1.21.1" && settings.Loader == "neoforge", "editable values are normalized");
Console.WriteLine("Settings update smoke tests passed: 4/4");

static void Assert(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine($"PASS {name}");
}
