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
        var server = global::MSLX.SDK.MSLX.Config.Servers.GetServer((uint)code.InstanceId); if (server is null) return NotFound();
        var settings = SyncStore.State.Instances.GetValueOrDefault(code.InstanceId);
        return Ok(new { protocol = "iota-sync/1", instanceId = code.InstanceId, instanceName = server.Name, latestRelease = settings?.Releases.FirstOrDefault() });
    }

    [HttpGet("manifest")]
    public IActionResult Manifest()
    {
        if (!Authenticate(out var code)) return Unauthorized();
        var release = SyncStore.State.Instances.GetValueOrDefault(code.InstanceId)?.Releases.FirstOrDefault(); if (release is null) return NotFound();
        return PhysicalFile(Path.Combine(SyncStore.ReleasesRoot, code.InstanceId.ToString(), release, "manifest.json"), "application/json");
    }

    [HttpGet("files/{**path}")]
    public IActionResult FileDownload(string path)
    {
        if (!Authenticate(out var code)) return Unauthorized();
        var release = SyncStore.State.Instances.GetValueOrDefault(code.InstanceId)?.Releases.FirstOrDefault(); if (release is null) return NotFound();
        var file = SyncStore.SafeReleaseFile(code.InstanceId, release, path); if (!System.IO.File.Exists(file)) return NotFound();
        return PhysicalFile(file, "application/octet-stream", enableRangeProcessing: true);
    }

    [HttpGet("launcher")]
    public IActionResult LauncherInfo()
    {
        if (!Authenticate(out _)) return Unauthorized(); return SyncStore.State.Launcher is null ? NotFound() : Ok(SyncStore.State.Launcher);
    }

    [HttpGet("launcher/file")]
    public IActionResult LauncherFile()
    {
        if (!Authenticate(out _)) return Unauthorized(); var release = SyncStore.State.Launcher; if (release is null) return NotFound();
        return PhysicalFile(Path.Combine(SyncStore.LauncherRoot, release.FileName), "application/octet-stream", release.FileName, enableRangeProcessing: true);
    }
}
