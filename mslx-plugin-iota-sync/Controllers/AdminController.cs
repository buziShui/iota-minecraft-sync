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
            settings = SyncStore.State.Instances.GetValueOrDefault(s.ID)
        });
        return Ok(list);
    }

    [HttpPut("instances/{id:int}")]
    public IActionResult Settings(int id, [FromBody] InstanceSettings input)
    {
        if (id != input.InstanceId || global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)id) is null) return BadRequest("实例不存在");
        SyncStore.State.Instances[id] = input; SyncStore.Save(); return Ok(input);
    }

    [HttpPost("instances/{id:int}/scan")]
    public IActionResult Scan(int id)
    {
        if (servers.IsServerRunning((uint)id)) return Conflict("实例运行中，禁止扫描");
        var server = global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)id); if (server is null) return NotFound();
        if (!SyncStore.State.Instances.TryGetValue(id, out var settings)) settings = SyncStore.State.Instances[id] = new() { InstanceId = id };
        var result = new List<ScanFile>();
        foreach (var item in settings.Directories.Where(x => x.Enabled))
        {
            var dir = Path.GetFullPath(Path.Combine(server.Base, item.Path));
            var baseDir = Path.GetFullPath(server.Base) + Path.DirectorySeparatorChar;
            if (!dir.StartsWith(baseDir, StringComparison.Ordinal) || !Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(server.Base, file).Replace('\\', '/');
                var detected = SyncStore.DetectSide(file);
                result.Add(new(relative, new FileInfo(file).Length, SyncStore.Sha256File(file), settings.Overrides.GetValueOrDefault(relative, detected.side), detected.reason));
            }
        }
        settings.LastScan = result.OrderBy(x => x.Path, StringComparer.Ordinal).ToList(); SyncStore.Save(); return Ok(settings.LastScan);
    }

    [HttpPost("instances/{id:int}/publish")]
    public IActionResult Publish(int id)
    {
        if (servers.IsServerRunning((uint)id)) return Conflict("实例运行中，禁止发布");
        var server = global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)id); if (server is null) return NotFound();
        if (!SyncStore.State.Instances.TryGetValue(id, out var settings) || settings.LastScan.Count == 0) return BadRequest("请先扫描并确认文件分类");
        if (settings.LastScan.Any(x => x.Side == FileSide.Unreviewed)) return BadRequest("仍有未确认端侧的 Mod");
        var releaseId = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var releaseRoot = Path.Combine(SyncStore.ReleasesRoot, id.ToString(), releaseId); Directory.CreateDirectory(releaseRoot);
        foreach (var file in settings.LastScan.Where(x => x.Side != FileSide.ServerOnly))
        {
            var source = Path.GetFullPath(Path.Combine(server.Base, file.Path));
            if (!System.IO.File.Exists(source) || SyncStore.Sha256File(source) != file.Sha256) return Conflict($"文件已变化，请重新扫描：{file.Path}");
            var target = SyncStore.SafeReleaseFile(id, releaseId, file.Path); Directory.CreateDirectory(Path.GetDirectoryName(target)!); System.IO.File.Copy(source, target, true);
        }
        var manifest = new SyncManifest("iota-sync/1", id, server.Name, releaseId, DateTimeOffset.UtcNow,
            settings.MinecraftVersion, settings.Loader, settings.LoaderVersion, settings.ServerAddress,
            settings.LastScan.Where(x => x.Side != FileSide.ServerOnly).ToList());
        System.IO.File.WriteAllText(Path.Combine(releaseRoot, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        settings.Releases.Insert(0, releaseId);
        foreach (var old in settings.Releases.Skip(5).ToList()) { Directory.Delete(Path.Combine(SyncStore.ReleasesRoot, id.ToString(), old), true); settings.Releases.Remove(old); }
        SyncStore.Save(); return Ok(manifest);
    }

    [HttpPost("instances/{id:int}/codes")]
    public IActionResult CreateCode(int id, [FromBody] CodeRequest request)
    {
        if (global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)id) is null) return NotFound();
        var name = string.IsNullOrWhiteSpace(request.Name) ? "未命名玩家" : request.Name.Trim();
        if (name.Length > 64) return BadRequest("同步码备注不能超过 64 个字符");
        var created = SyncStore.CreateCode(id, name); return Ok(new { created.model.Id, created.model.Name, created.model.InstanceId, created.model.CreatedAt, code = created.raw });
    }

    [HttpGet("codes")]
    public IActionResult Codes() => Ok(SyncStore.State.Codes
        .OrderByDescending(x => x.CreatedAt)
        .Select(x => new { x.Id, x.InstanceId, x.Name, x.Enabled, x.CreatedAt }));

    [HttpDelete("codes/{codeId}")]
    public IActionResult Revoke(string codeId)
    {
        var code = SyncStore.State.Codes.FirstOrDefault(x => x.Id == codeId); if (code is null) return NotFound();
        code.Enabled = false; SyncStore.Save(); return NoContent();
    }

    public sealed record CodeRequest(string Name);
}
