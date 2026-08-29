using System.Text.Json;
using HattrickAI.V5.Core;

const string EmbeddedConsumerKey = "4CzYYAnSg7SSHkQyDVMLIV";

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "hattrickai.v5";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services.AddScoped<ChppV5>(sp =>
{
    var configuredKey = builder.Configuration["CHPP_CONSUMER_KEY"];
    var key = string.IsNullOrWhiteSpace(configuredKey) ? EmbeddedConsumerKey : configuredKey.Trim();
    var secret = builder.Configuration["CHPP_CONSUMER_SECRET"]?.Trim() ?? string.Empty;
    return new ChppV5(new Credentials(key, secret), sp.GetRequiredService<IHttpContextAccessor>());
});
builder.Services.AddScoped<AnalysisService>();
builder.Services.AddScoped<ReferenceMatchService>();

var app = builder.Build();
var portText = Environment.GetEnvironmentVariable("PORT");
var port = int.TryParse(portText, out var parsed) ? parsed : 10000;
app.Urls.Add($"http://0.0.0.0:{port}");
app.UseSession();
app.UseDefaultFiles();
app.UseStaticFiles();

var build = Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT")
    ?? Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT_SHA")
    ?? "dev";

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "HattrickAI V5", build }));
app.MapGet("/api/v5/build", () => Results.Ok(new { build }));
app.MapGet("/api/v5/status", (ChppV5 chpp) => Results.Ok(new
{
    connected = chpp.Connected,
    configured = !string.IsNullOrWhiteSpace(builder.Configuration["CHPP_CONSUMER_SECRET"])
}));
app.MapGet("/api/deploy/log", () =>
{
    const string logPath = "/app/deploy.log";
    if (!File.Exists(logPath))
        return Results.Ok(new { lines = Array.Empty<string>(), updated = false });

    var lines = File.ReadLines(logPath).TakeLast(150).ToArray();
    return Results.Ok(new { lines, updated = true });
});

app.MapPost("/api/v5/questionnaire", (HttpContext http, QuestionnaireRequest request) =>
{
    if (!Enum.TryParse<CoachStyle>(request.CoachStyle, true, out var coach))
        return Results.BadRequest(new { message = "Teknik direktör seçimi geçersiz." });
    if (!Enum.TryParse<TeamSpiritLevel>(request.TeamSpirit, true, out var spirit))
        return Results.BadRequest(new { message = "Takım ruhu seçimi geçersiz." });
    if (!Enum.TryParse<TeamAttitude>(request.MatchImportance, true, out var attitude))
        return Results.BadRequest(new { message = "Maç önemi seçimi geçersiz." });

    http.Session.SetString("v5.coach", coach.ToString());
    http.Session.SetString("v5.spirit", spirit.ToString());
    http.Session.SetString("v5.attitude", attitude.ToString());
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/v5/questionnaire", (HttpContext http) =>
{
    var questionnaire = new MatchQuestionnaire(
        Enum.TryParse<CoachStyle>(http.Session.GetString("v5.coach"), true, out var coach) ? coach : CoachStyle.Neutral,
        Enum.TryParse<TeamSpiritLevel>(http.Session.GetString("v5.spirit"), true, out var spirit) ? spirit : TeamSpiritLevel.Composed,
        Enum.TryParse<TeamAttitude>(http.Session.GetString("v5.attitude"), true, out var attitude) ? attitude : TeamAttitude.Normal);
    return Results.Ok(questionnaire);
});

app.MapGet("/api/v5/analysis", async (HttpContext http, AnalysisService service, ChppV5 chpp, CancellationToken ct) =>
{
    if (!chpp.Connected) return Results.Unauthorized();
    try
    {
        var questionnaire = new MatchQuestionnaire(
            Enum.TryParse<CoachStyle>(http.Session.GetString("v5.coach"), true, out var coach) ? coach : CoachStyle.Neutral,
            Enum.TryParse<TeamSpiritLevel>(http.Session.GetString("v5.spirit"), true, out var spirit) ? spirit : TeamSpiritLevel.Composed,
            Enum.TryParse<TeamAttitude>(http.Session.GetString("v5.attitude"), true, out var attitude) ? attitude : TeamAttitude.Normal);
        return Results.Ok(await service.RunAsync(build, questionnaire, ct));
    }
    catch (Exception ex) { return Results.Problem(ex.Message, statusCode: 502); }
});

app.MapGet("/api/v5/reference-match", async (ReferenceMatchService service, ChppV5 chpp, CancellationToken ct) =>
{
    if (!chpp.Connected) return Results.Unauthorized();
    try { return Results.Ok(await service.GetAsync(ct)); }
    catch (Exception ex) { return Results.Problem(ex.Message, statusCode: 502); }
});

app.MapGet("/auth/chpp/start", async (HttpContext http, ChppV5 chpp, CancellationToken ct) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration["CHPP_CONSUMER_SECRET"]))
            return Results.Redirect("/?error=" + Uri.EscapeDataString("CHPP_CONSUMER_SECRET Render Environment Variables içinde tanımlı değil."));
        var proto = http.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? http.Request.Scheme;
        var callback = $"{proto}://{http.Request.Host}/auth/chpp/callback";
        return Results.Redirect(await chpp.StartAsync(callback, ct));
    }
    catch (Exception ex) { return Results.Redirect("/?error=" + Uri.EscapeDataString(ex.Message)); }
});

app.MapGet("/auth/chpp/callback", async (HttpContext http, ChppV5 chpp, CancellationToken ct) =>
{
    var verifier = http.Request.Query["oauth_verifier"].ToString();
    if (string.IsNullOrWhiteSpace(verifier))
        return Results.Redirect("/?error=" + Uri.EscapeDataString("CHPP doğrulama kodu alınamadı."));
    try
    {
        await chpp.FinishAsync(verifier, ct);
        return Results.Redirect("/?connected=1");
    }
    catch (Exception ex) { return Results.Redirect("/?error=" + Uri.EscapeDataString(ex.Message)); }
});

app.MapPost("/auth/chpp/logout", (ChppV5 chpp) => { chpp.Disconnect(); return Results.Ok(new { ok = true }); });
app.Run();

public sealed record QuestionnaireRequest(string CoachStyle, string TeamSpirit, string MatchImportance);
