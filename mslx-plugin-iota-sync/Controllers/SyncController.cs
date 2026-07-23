using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MSLX.Plugin.IotaSync.Controllers;

[ApiController, AllowAnonymous]
[Route("api/plugins/mslx-plugin-iota-sync/sync")]
public sealed class SyncController : ControllerBase
{
    private bool Authenticate(out AccessCode code)
    {
        code = null!;
        var raw = Request.Headers["X-Iota-Sync-Code"].FirstOrDefault();
        return raw is not null && SyncStore.VerifyCode(raw, out code);
    }

    [HttpGet("source")]
    public IActionResult Source()
    {
        if (!Authenticate(out var code)) return Unauthorized();
        if (code.InstanceId is null && !TryRequestedInstance(out _))
        {
            var instances = global::MSLX.SDK.MSLX.Config.Servers.GetServerList()
                .Select(server => new
                {
                    instanceId = server.ID,
                    instanceName = server.Name,
                    latestRelease = SyncStore.GetInstance(server.ID)?.Releases.FirstOrDefault()
                })
                .Where(x => x.latestRelease is not null)
                .ToList();
            return Ok(new { protocol = "iota-sync/1", global = true, instances });
        }

        if (!ResolveInstance(code, out var instanceId)) return BadRequest("统一同步码需要指定 instanceId");
        var server = global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)instanceId); if (server is null) return NotFound();
        var settings = SyncStore.GetInstance(instanceId);
        return Ok(new { protocol = "iota-sync/1", global = code.InstanceId is null, instanceId, instanceName = server.Name, latestRelease = settings?.Releases.FirstOrDefault() });
    }

    [HttpGet("manifest")]
    public IActionResult Manifest()
    {
        if (!Authenticate(out var code)) return Unauthorized();
        if (!ResolveInstance(code, out var instanceId)) return BadRequest("统一同步码需要指定 instanceId");
        var release = SyncStore.GetInstance(instanceId)?.Releases.FirstOrDefault(); if (release is null) return NotFound();
        return PhysicalFile(Path.Combine(SyncStore.ReleasesRoot, instanceId.ToString(), release, "manifest.json"), "application/json");
    }

    [HttpGet("files/{**path}")]
    public IActionResult FileDownload(string path)
    {
        if (!Authenticate(out var code)) return Unauthorized();
        if (!ResolveInstance(code, out var instanceId)) return BadRequest("统一同步码需要指定 instanceId");
        var release = SyncStore.GetInstance(instanceId)?.Releases.FirstOrDefault(); if (release is null) return NotFound();
        var file = SyncStore.SafeReleaseFile(instanceId, release, path); if (!System.IO.File.Exists(file)) return NotFound();
        return PhysicalFile(file, "application/octet-stream", enableRangeProcessing: true);
    }

    private bool ResolveInstance(AccessCode code, out int instanceId)
    {
        if (code.InstanceId is int boundInstance)
        {
            instanceId = boundInstance;
            return true;
        }
        return TryRequestedInstance(out instanceId);
    }

    private bool TryRequestedInstance(out int instanceId)
    {
        var raw = Request.Query["instanceId"].FirstOrDefault()
                  ?? Request.Headers["X-Iota-Instance-Id"].FirstOrDefault();
        if (!int.TryParse(raw, out instanceId)) return false;
        return global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)instanceId) is not null;
    }
}
