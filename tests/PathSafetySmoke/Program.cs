using MSLX.Plugin.IotaSync;

var root = Path.Combine(Path.GetTempPath(), "iota-path-safety-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "mods"));
var file = Path.Combine(root, "mods", "example.jar");
File.WriteAllText(file, "test");

try
{
    AssertEqual(file, SyncStore.SafeServerFile(root, "mods/example.jar"), "normal relative path");
    AssertRejected("../secret.txt", "parent traversal");
    AssertRejected(Path.Combine(Path.GetPathRoot(root)!, "secret.txt"), "absolute path");
    AssertRejected("mods/../../secret.txt", "nested traversal");
    var releaseIds = Enumerable.Range(0, 1000).Select(_ => SyncStore.CreateReleaseId()).ToHashSet(StringComparer.Ordinal);
    if (releaseIds.Count != 1000) throw new Exception("release IDs must be unique");
    Console.WriteLine("PASS unique release IDs");
    Console.WriteLine("Path and release safety smoke tests passed: 5/5");
}
finally
{
    Directory.Delete(root, true);
}

void AssertRejected(string relative, string name)
{
    try
    {
        SyncStore.SafeServerFile(root, relative);
        throw new Exception($"{name}: expected rejection");
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine($"PASS {name}");
    }
}

static void AssertEqual(string expected, string actual, string name)
{
    if (!string.Equals(Path.GetFullPath(expected), Path.GetFullPath(actual), StringComparison.Ordinal))
        throw new Exception($"{name}: expected {expected}, got {actual}");
    Console.WriteLine($"PASS {name}");
}
