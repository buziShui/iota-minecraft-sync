using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSLX.SDK.Interfaces;
using MSLX.SDK.IServices;

namespace MSLX.Plugin.IotaSync.Controllers;

[ApiController, Authorize(Roles = "admin")]
[Route("api/plugins/mslx-plugin-iota-sync/admin")]
public sealed class AdminController(IMCServerService servers) : ControllerBase
{
    private static int _initialScanDone;
    [HttpGet("instances")]
    public IActionResult Instances()
    {
        if (Interlocked.Exchange(ref _initialScanDone, 1) == 0)
            foreach (var instance in global::MSLX.SDK.MSLX.Config.Servers.GetServerList().Where(x => !servers.IsServerRunning((uint)x.ID))) Scan(instance.ID);
        var list = global::MSLX.SDK.MSLX.Config.Servers.GetServerList().Select(s => new {
            id = s.ID, s.Name, s.Base, running = servers.IsServerRunning((uint)s.ID),
            settings = SyncStore.GetInstance(s.ID)
        });
        return Ok(list);
    }

    [HttpPut("instances/{id:int}")]
    public IActionResult Settings(int id, [FromBody] InstanceSettingsUpdate input) => WithInstanceLock(id, () =>
    {
        if ((input.InstanceId.HasValue && id != input.InstanceId.Value)
            || global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)id) is null)
            return BadRequest("实例不存在");
        var settings = SyncStore.UpdateInstance(id, true, value => value.ApplyEditable(input));
        return Ok(settings);
    });

    [HttpPost("instances/{id:int}/scan")]
    public IActionResult Scan(int id) => WithInstanceLock(id, () =>
    {
        if (servers.IsServerRunning((uint)id)) return Conflict("实例运行中，禁止扫描");
        var server = global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)id); if (server is null) return NotFound();
        var settings = SyncStore.GetInstance(id) ?? InstanceSettings.CreateDefault(id);
        settings.Directories = InstanceSettings.NormalizeDirectories(settings.Directories);
        var result = new Dictionary<string, ScanFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in settings.Directories.Where(x => x.Enabled))
        {
            var dir = Path.GetFullPath(Path.Combine(server.Base, item.Path));
            var baseDir = Path.GetFullPath(server.Base) + Path.DirectorySeparatorChar;
            if (!dir.StartsWith(baseDir, StringComparison.Ordinal) || !Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(server.Base, file).Replace('\\', '/');
                var detected = SyncStore.DetectSide(file);
                var side = settings.Overrides.TryGetValue(relative, out var overridden)
                           && overridden != FileSide.Unreviewed
                    ? overridden
                    : detected.side;
                result[relative] = new(relative, new FileInfo(file).Length, SyncStore.Sha256File(file), side, detected.reason, detected.side, detected.runtimeSide);
            }
        }
        var scan = InstanceSettings.NormalizeScanFiles(result.Values);
        SyncStore.UpdateInstance(id, true, value => value.LastScan = scan);
        return Ok(scan);
    });

    [HttpPost("instances/{id:int}/publish")]
    public IActionResult Publish(int id) => WithInstanceLock(id, () =>
    {
        if (servers.IsServerRunning((uint)id)) return Conflict("实例运行中，禁止发布");
        var server = global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)id); if (server is null) return NotFound();
        var settings = SyncStore.GetInstance(id);
        if (settings is null || settings.LastScan.Count == 0) return BadRequest("请先扫描并确认文件分类");
        if (settings.LastScan.Any(x => x.Side == FileSide.Unreviewed)) return BadRequest("仍有未确认端侧的 Mod");

        var sources = new List<(ScanFile File, string Source)>();
        foreach (var file in settings.LastScan.Where(x => x.Side != FileSide.ServerOnly))
        {
            string source;
            try
            {
                source = SyncStore.SafeServerFile(server.Base, file.Path);
            }
            catch (InvalidOperationException error)
            {
                return BadRequest(error.Message);
            }
            if (!System.IO.File.Exists(source) || SyncStore.Sha256File(source) != file.Sha256) return Conflict($"文件已变化，请重新扫描：{file.Path}");
            sources.Add((file, source));
        }

        var releaseId = SyncStore.CreateReleaseId();
        var instanceReleaseRoot = Path.Combine(SyncStore.ReleasesRoot, id.ToString());
        var releaseRoot = Path.Combine(instanceReleaseRoot, releaseId);
        var temporaryRoot = Path.Combine(instanceReleaseRoot, $".{releaseId}.tmp-{Guid.NewGuid():N}");
        var manifest = new SyncManifest("iota-sync/1", id, server.Name, releaseId, DateTimeOffset.UtcNow,
            settings.MinecraftVersion, settings.Loader, settings.LoaderVersion, settings.ServerAddress,
            settings.LastScan.Where(x => x.Side != FileSide.ServerOnly).ToList());

        try
        {
            Directory.CreateDirectory(temporaryRoot);
            foreach (var (file, source) in sources)
            {
                var target = SyncStore.SafeChildFile(temporaryRoot, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                System.IO.File.Copy(source, target, false);
            }
            System.IO.File.WriteAllText(
                Path.Combine(temporaryRoot, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            Directory.Move(temporaryRoot, releaseRoot);
        }
        catch
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
            throw;
        }

        var previousReleases = settings.Releases.ToList();
        var expiredReleases = previousReleases.Skip(4).ToList();
        try
        {
            SyncStore.UpdateInstance(id, false, value =>
                value.Releases = new[] { releaseId }.Concat(previousReleases).Distinct(StringComparer.Ordinal).Take(5).ToList());
        }
        catch
        {
            if (Directory.Exists(releaseRoot)) Directory.Delete(releaseRoot, true);
            throw;
        }

        foreach (var old in expiredReleases)
        {
            var oldRoot = Path.Combine(instanceReleaseRoot, old);
            if (Directory.Exists(oldRoot)) Directory.Delete(oldRoot, true);
        }
        return Ok(manifest);
    });

    [HttpPost("instances/{id:int}/codes")]
    public IActionResult CreateCode(int id, [FromBody] CodeRequest request)
    {
        if (global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)id) is null) return NotFound();
        return CreateCodeCore(id, request);
    }

    [HttpPost("codes")]
    public IActionResult CreateCode([FromBody] CodeRequest request)
    {
        if (request.InstanceId is int instanceId
            && global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)instanceId) is null)
            return BadRequest("实例不存在");
        return CreateCodeCore(request.InstanceId, request);
    }

    [HttpGet("codes")]
    public IActionResult Codes() => Ok(SyncStore.GetCodes()
        .OrderByDescending(x => x.CreatedAt)
        .Select(x => new { x.Id, x.InstanceId, global = x.InstanceId is null, x.Name, x.Enabled, x.CreatedAt }));

    [HttpPost("codes/{codeId}/revoke")]
    public IActionResult Revoke(string codeId)
    {
        return SyncStore.RevokeCode(codeId) ? NoContent() : NotFound();
    }

    [HttpDelete("codes/{codeId}")]
    public IActionResult Delete(string codeId)
    {
        return SyncStore.DeleteCode(codeId) ? NoContent() : NotFound();
    }

    private static IActionResult WithInstanceLock(int instanceId, Func<IActionResult> action)
    {
        lock (SyncStore.InstanceGate(instanceId)) return action();
    }

    private IActionResult CreateCodeCore(int? instanceId, CodeRequest request)
    {
        var name = string.IsNullOrWhiteSpace(request.Name) ? "未命名玩家" : request.Name.Trim();
        if (name.Length > 64) return BadRequest("同步码备注不能超过 64 个字符");
        var created = SyncStore.CreateCode(instanceId, name);
        return Ok(new
        {
            created.model.Id,
            created.model.Name,
            created.model.InstanceId,
            global = created.model.InstanceId is null,
            created.model.CreatedAt,
            code = created.raw
        });
    }

    public sealed record CodeRequest(string Name, int? InstanceId = null);
}
