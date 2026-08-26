using HattrickAI.CHPP;
using HattrickAI.V3;
using HattrickAI.V3.Core;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = "hattrickai.v3";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services.AddSingleton<ChppSessionTokenStore>();
builder.Services.AddScoped<ChppOAuthClient>(sp =>
{
    var credentials = ChppSettings.Load(builder.Configuration);
    var store = sp.GetRequiredService<ChppSessionTokenStore>();
    return new ChppOAuthClient(credentials, store, requestedScopes: string.Empty);
});
builder.Services.AddScoped<V3AnalysisService>();

var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT");
app.Urls.Add($"http://0.0.0.0:{(int.TryParse(port, out var p) ? p : 10000)}");
app.UseSession();
app.UseDefaultFiles();
app.UseStaticFiles();

var build = Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT")
    ?? Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT_SHA")
    ?? "dev";

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "HattrickAI V3", build }));
app.MapGet("/api/v3/build", () => Results.Ok(new { build }));

app.MapGet("/api/v3/status", async (ChppOAuthClient oauth, CancellationToken ct) =>
{
    try { return Results.Ok(new { connected = await oauth.ValidateStoredAccessTokenAsync(ct) }); }
    catch (Exception ex) { return Results.Ok(new { connected = false, error = ex.Message }); }
});

app.MapGet("/api/v3/analysis", async (V3AnalysisService service, ChppOAuthClient oauth, CancellationToken ct) =>
{
    if (!await oauth.ValidateStoredAccessTokenAsync(ct))
        return Results.Unauthorized();
    try { return Results.Ok(await service.RunAsync(ct)); }
    catch (Exception ex) { return Results.Problem(ex.Message, statusCode: 502); }
});

app.MapGet("/auth/chpp/start", async (HttpContext http, ChppOAuthClient oauth) =>
{
    try
    {
        var proto = http.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? http.Request.Scheme;
        var callback = $"{proto}://{http.Request.Host}/auth/chpp/callback";
        return Results.Redirect(await oauth.BeginAuthorizationAsync(callback, http.RequestAborted));
    }
    catch (Exception ex)
    {
        return Results.Redirect("/?error=" + Uri.EscapeDataString(ex.Message));
    }
});

app.MapGet("/auth/chpp/callback", async (HttpContext http, ChppOAuthClient oauth) =>
{
    var verifier = http.Request.Query["oauth_verifier"].ToString();
    if (string.IsNullOrWhiteSpace(verifier))
        return Results.Redirect("/?error=" + Uri.EscapeDataString("CHPP onay kodu alınamadı."));
    try
    {
        await oauth.CompleteAuthorizationAsync(verifier, http.RequestAborted);
        return Results.Redirect("/?connected=1");
    }
    catch (Exception ex)
    {
        return Results.Redirect("/?error=" + Uri.EscapeDataString(ex.Message));
    }
});

app.MapPost("/auth/chpp/logout", async (ChppOAuthClient oauth) =>
{
    try { await oauth.InvalidateStoredAccessTokenAsync(); } catch { }
    return Results.Ok(new { ok = true });
});

app.Run();
