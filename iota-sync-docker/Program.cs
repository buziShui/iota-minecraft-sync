using IotaSync.Docker;
using Microsoft.AspNetCore.Http.Features;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = "wwwroot"
});
builder.Services.Configure<FormOptions>(x => x.MultipartBodyLengthLimit = 200_000_000);
builder.Services.AddSingleton<SyncStore>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("admin-login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
var app = builder.Build();
const string adminCookieName = "iota_admin_session";
var adminUsername = builder.Configuration["IOTA_ADMIN_USERNAME"]?.Trim();
var adminPassword = builder.Configuration["IOTA_ADMIN_PASSWORD"];
if (string.IsNullOrWhiteSpace(adminUsername))
    throw new InvalidOperationException("必须设置 IOTA_ADMIN_USERNAME");
if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 12)
    throw new InvalidOperationException("必须设置至少 12 个字符的 IOTA_ADMIN_PASSWORD");

var adminUsernameHash = SHA256.HashData(Encoding.UTF8.GetBytes(adminUsername));
var adminPasswordHash = SHA256.HashData(Encoding.UTF8.GetBytes(adminPassword));
var adminSessionKey = SHA256.HashData(Encoding.UTF8.GetBytes($"iota-admin-session-v1\n{adminUsername}\n{adminPassword}"));

bool SecureCredentialEquals(string? supplied, byte[] expectedHash)
{
    var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied ?? string.Empty));
    return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
}

string CreateSessionToken(DateTimeOffset expiresAt)
{
    var expiresUnix = expiresAt.ToUnixTimeSeconds();
    var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    var payload = $"{adminUsername}\n{expiresUnix}\n{nonce}";
    var signature = HMACSHA256.HashData(adminSessionKey, Encoding.UTF8.GetBytes(payload));
    return $"{expiresUnix}.{nonce}.{Convert.ToHexString(signature).ToLowerInvariant()}";
}

bool IsSessionValid(string? token)
{
    if (string.IsNullOrWhiteSpace(token)) return false;
    var parts = token.Split('.', 3);
    if (parts.Length != 3 || parts[1].Length != 32 || parts[2].Length != 64 ||
        !long.TryParse(parts[0], out var expiresUnix)) return false;

    try
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUnix);
        if (expiresAt <= now || expiresAt > now.AddDays(31)) return false;

        var payload = $"{adminUsername}\n{expiresUnix}\n{parts[1]}";
        var expected = HMACSHA256.HashData(adminSessionKey, Encoding.UTF8.GetBytes(payload));
        var supplied = Convert.FromHexString(parts[2]);
        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }
    catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
    {
        return false;
    }
}

app.UseDefaultFiles(); app.UseStaticFiles(); app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isAuthEndpoint = path == "/api/admin/login" || path == "/api/admin/logout";
    if (path.StartsWithSegments("/api/admin") && !isAuthEndpoint &&
        !IsSessionValid(context.Request.Cookies[adminCookieName]))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "登录已失效，请重新登录" });
        return;
    }
    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/admin/login", (AdminLoginRequest input, HttpContext context) =>
{
    var usernameMatches = SecureCredentialEquals(input.Username?.Trim(), adminUsernameHash);
    var passwordMatches = SecureCredentialEquals(input.Password, adminPasswordHash);
    if (!usernameMatches || !passwordMatches)
        return Results.Json(new { message = "账号或密码错误" }, statusCode: StatusCodes.Status401Unauthorized);

    var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
    context.Response.Cookies.Append(adminCookieName, CreateSessionToken(expiresAt), new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = context.Request.IsHttps,
        IsEssential = true,
        Path = "/",
        Expires = expiresAt,
        MaxAge = TimeSpan.FromDays(30)
    });
    return Results.Ok(new { username = adminUsername });
}).RequireRateLimiting("admin-login");

app.MapPost("/api/admin/logout", (HttpContext context) =>
{
    context.Response.Cookies.Delete(adminCookieName, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = context.Request.IsHttps,
        IsEssential = true,
        Path = "/"
    });
    return Results.NoContent();
});

app.MapGet("/api/admin/instances", (SyncStore store) => Results.Ok(store.State.Instances.Values.OrderBy(x => x.InstanceId)));
app.MapPost("/api/admin/instances", (CreateInstance input, SyncStore store) =>
{
    var root = Path.GetFullPath(input.RootPath);
    if (!Directory.Exists(root)) return Results.BadRequest("容器内实例目录不存在，请检查 volumes 挂载路径");
    var id = store.State.NextInstanceId++;
    var item = new InstanceSettings { InstanceId = id, Name = input.Name.Trim(), RootPath = root };
    store.State.Instances[id] = item; store.Save(); return Results.Ok(item);
});
app.MapPut("/api/admin/instances/{id:int}", (int id, InstanceSettings input, SyncStore store) =>
{
    if (id != input.InstanceId || !store.State.Instances.ContainsKey(id)) return Results.NotFound();
    input.RootPath = Path.GetFullPath(input.RootPath); store.State.Instances[id] = input; store.Save(); return Results.Ok(input);
});
app.MapDelete("/api/admin/instances/{id:int}", (int id, SyncStore store) =>
{
    if (!store.State.Instances.Remove(id)) return Results.NotFound();
    foreach (var code in store.State.Codes.Where(x => x.InstanceId == id)) code.Enabled = false;
    store.Save(); return Results.NoContent();
});
app.MapPost("/api/admin/instances/{id:int}/scan", (int id, SyncStore store) =>
{
    if (!store.State.Instances.TryGetValue(id, out var item)) return Results.NotFound();
    if (!item.StopConfirmed) return Results.Conflict("请先停止 Minecraft 服务端并勾选停服确认");
    var root = Path.GetFullPath(item.RootPath); if (!Directory.Exists(root)) return Results.BadRequest("实例目录不存在");
    var files = new List<ScanFile>();
    foreach (var dirSetting in item.Directories.Where(x => x.Enabled))
    {
        var dir = Path.GetFullPath(Path.Combine(root, dirSetting.Path));
        if (!dir.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !Directory.Exists(dir)) continue;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/'); var detected = SyncStore.DetectSide(file);
            files.Add(new(relative, new FileInfo(file).Length, SyncStore.Sha256File(file), item.Overrides.GetValueOrDefault(relative, detected.Side), detected.Reason));
        }
    }
    item.LastScan = files.OrderBy(x => x.Path, StringComparer.Ordinal).ToList(); item.StopConfirmed = false; store.Save(); return Results.Ok(item.LastScan);
});
app.MapPost("/api/admin/instances/{id:int}/publish", (int id, SyncStore store) =>
{
    if (!store.State.Instances.TryGetValue(id, out var item)) return Results.NotFound();
    if (!item.StopConfirmed) return Results.Conflict("请再次确认 Minecraft 服务端已停止");
    if (item.LastScan.Count == 0) return Results.BadRequest("请先扫描");
    if (item.LastScan.Any(x => string.IsNullOrWhiteSpace(x.Path) || string.IsNullOrWhiteSpace(x.Sha256))) return Results.BadRequest("扫描数据无效，请重新扫描");
    if (item.LastScan.Any(x => x.Side == FileSide.Unreviewed)) return Results.BadRequest("仍有待人工确认的 Mod");
    var releaseId = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss"); var releaseRoot = Path.Combine(store.ReleasesRoot, id.ToString(), releaseId); Directory.CreateDirectory(releaseRoot);
    foreach (var file in item.LastScan.Where(x => x.Side != FileSide.ServerOnly))
    {
        var source = Path.GetFullPath(Path.Combine(item.RootPath, file.Path));
        if (!File.Exists(source) || SyncStore.Sha256File(source) != file.Sha256) { Directory.Delete(releaseRoot, true); return Results.Conflict($"文件已变化，请重新扫描：{file.Path}"); }
        var target = store.SafeReleaseFile(id, releaseId, file.Path); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target, true);
    }
    var manifest = new SyncManifest("iota-sync/1", id, item.Name, releaseId, DateTimeOffset.UtcNow, item.MinecraftVersion, item.Loader, item.LoaderVersion, item.ServerAddress, item.LastScan.Where(x => x.Side != FileSide.ServerOnly).ToList());
    store.WriteJson(Path.Combine(releaseRoot, "manifest.json"), manifest); item.Releases.Insert(0, releaseId);
    foreach (var old in item.Releases.Skip(5).ToList()) { Directory.Delete(Path.Combine(store.ReleasesRoot, id.ToString(), old), true); item.Releases.Remove(old); }
    item.StopConfirmed = false; store.Save(); return Results.Ok(manifest);
});
app.MapGet("/api/admin/codes", (SyncStore store) => Results.Ok(store.State.Codes.Select(x => new { x.Id, x.InstanceId, x.Name, x.Enabled, x.CreatedAt })));
app.MapPost("/api/admin/instances/{id:int}/codes", (int id, CodeRequest input, SyncStore store) =>
{
    if (!store.State.Instances.ContainsKey(id)) return Results.NotFound(); var result = store.CreateCode(id, input.Name);
    return Results.Ok(new { result.Model.Id, result.Model.Name, result.Model.InstanceId, code = result.Raw });
});
app.MapDelete("/api/admin/codes/{codeId}", (string codeId, SyncStore store) =>
{
    var code = store.State.Codes.FirstOrDefault(x => x.Id == codeId); if (code is null) return Results.NotFound(); code.Enabled = false; store.Save(); return Results.NoContent();
});
app.MapGet("/api/admin/launcher", (SyncStore store) => Results.Ok(store.State.Launcher));
app.MapPost("/api/admin/launcher", async (HttpRequest request, SyncStore store) =>
{
    var form = await request.ReadFormAsync(); var file = form.Files["file"]; var version = form["version"].ToString();
    if (file is null || string.IsNullOrWhiteSpace(version)) return Results.BadRequest("缺少版本或 EXE 文件");
    Directory.CreateDirectory(store.LauncherRoot); var name = $"pcl-io-{version}.exe"; var path = Path.Combine(store.LauncherRoot, name);
    await using (var output = File.Create(path)) await file.CopyToAsync(output);
    store.State.Launcher = new(version, form["notes"].ToString(), name, file.Length, SyncStore.Sha256File(path), DateTimeOffset.UtcNow); store.Save(); return Results.Ok(store.State.Launcher);
});

var sync = app.MapGroup("/api/plugins/mslx-plugin-iota-sync/sync");
sync.AddEndpointFilter(async (context, next) =>
{
    var http = context.HttpContext; var store = http.RequestServices.GetRequiredService<SyncStore>(); var raw = http.Request.Headers["X-Iota-Sync-Code"].FirstOrDefault();
    if (raw is null || !store.VerifyCode(raw, out var code)) return Results.Unauthorized(); http.Items["code"] = code; return await next(context);
});
sync.MapGet("/source", (HttpContext http, SyncStore store) =>
{
    var code = (AccessCode)http.Items["code"]!; if (!store.State.Instances.TryGetValue(code.InstanceId, out var item)) return Results.NotFound();
    return Results.Ok(new { protocol = "iota-sync/1", instanceId = item.InstanceId, instanceName = item.Name, latestRelease = item.Releases.FirstOrDefault() });
});
sync.MapGet("/manifest", (HttpContext http, SyncStore store) =>
{
    var code = (AccessCode)http.Items["code"]!; var release = store.State.Instances.GetValueOrDefault(code.InstanceId)?.Releases.FirstOrDefault();
    return release is null ? Results.NotFound() : Results.File(Path.Combine(store.ReleasesRoot, code.InstanceId.ToString(), release, "manifest.json"), "application/json");
});
sync.MapGet("/files/{**path}", (string path, HttpContext http, SyncStore store) =>
{
    var code = (AccessCode)http.Items["code"]!; var release = store.State.Instances.GetValueOrDefault(code.InstanceId)?.Releases.FirstOrDefault(); if (release is null) return Results.NotFound();
    var file = store.SafeReleaseFile(code.InstanceId, release, path); return File.Exists(file) ? Results.File(file, "application/octet-stream", enableRangeProcessing: true) : Results.NotFound();
});
sync.MapGet("/launcher", (SyncStore store) => store.State.Launcher is null ? Results.NotFound() : Results.Ok(store.State.Launcher));
sync.MapGet("/launcher/file", (SyncStore store) => store.State.Launcher is null ? Results.NotFound() : Results.File(Path.Combine(store.LauncherRoot, store.State.Launcher.FileName), "application/octet-stream", store.State.Launcher.FileName, enableRangeProcessing: true));

app.MapFallbackToFile("index.html"); app.Run();
public sealed class CreateInstance
{
    public string Name { get; set; } = "";
    public string RootPath { get; set; } = "";
}
public sealed class CodeRequest { public string Name { get; set; } = ""; }
public sealed class AdminLoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
