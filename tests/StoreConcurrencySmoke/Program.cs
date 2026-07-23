using MSLX.Plugin.IotaSync;

var root = Path.Combine(Path.GetTempPath(), "iota-store-concurrency-" + Guid.NewGuid().ToString("N"));
try
{
    SyncStore.Initialize(root);
    Parallel.For(0, 100, _ => SyncStore.UpdateInstance(1, true, settings =>
    {
        var value = int.TryParse(settings.LoaderVersion, out var current) ? current : 0;
        settings.LoaderVersion = (value + 1).ToString();
    }));

    var detached = SyncStore.GetInstance(1)!;
    detached.LoaderVersion = "tampered";
    Assert(SyncStore.GetInstance(1)!.LoaderVersion == "100", "parallel instance updates are not lost");
    Assert(SyncStore.GetInstance(1)!.LoaderVersion != detached.LoaderVersion, "read snapshots cannot mutate stored state");

    Parallel.For(0, 10, index => SyncStore.CreateCode(1, $"player-{index}"));
    Assert(SyncStore.GetCodes().Count == 10, "parallel code creation is serialized");
    var deletionTarget = SyncStore.GetCodes()[0];
    Assert(SyncStore.DeleteCode(deletionTarget.Id) && !SyncStore.DeleteCode(deletionTarget.Id), "code deletion is idempotent");
    Console.WriteLine("Store concurrency smoke tests passed: 4/4");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, true);
}

static void Assert(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine($"PASS {name}");
}
