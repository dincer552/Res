using System.Text;
using HattrickAI.V5.Core;

const string EmbeddedConsumerKey = "4CzYYAnSg7SSHkQyDVMLIV";

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddScoped<RatingTestService>();

var app = builder.Build();
var portText = Environment.GetEnvironmentVariable("PORT");
var port = int.TryParse(portText, out var parsed) ? parsed : 10000;
app.Urls.Add($"http://0.0.0.0:{port}");
app.UseSession();

// V5 ana sayfada CHPP bağlantısı başarılıysa bölgesel rating testine geçiş göster.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" || context.Request.Path == "/index.html")
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next();
            if (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
            {
                buffer.Position = 0;
                using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                var html = await reader.ReadToEndAsync();
                var chpp = context.RequestServices.GetRequiredService<ChppV5>();

                if (chpp.Connected && !html.Contains("id=\"ratingTestLink\"", StringComparison.Ordinal))
                {
                    const string marker = "<article class=\"lineup-card\">";
                    const string testPanel = "<section class=\"panel analysis\" id=\"ratingTestLink\"><div class=\"panel-head\"><div><div class=\"eyebrow\">GELİŞTİRME / TEST</div><div class=\"panel-title\">Bölgesel Rating Testi</div></div></div><div class=\"analysis\"><a class=\"analyze\" href=\"/rating-test.html\" style=\"display:flex;align-items:center;justify-content:center;text-decoration:none\">TEST EKRANINI AÇ</a></div></section>";
                    html = html.Replace(marker, testPanel + "\n" + marker, StringComparison.Ordinal);
                }

                var bytes = Encoding.UTF8.GetBytes(html);
                context.Response.Headers.Remove("Content-Length");
                context.Response.Headers.Remove("ETag");
                context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                context.Response.Body = originalBody;
                await originalBody.WriteAsync(bytes);
            }
            else
            {
                context.Response.Body = originalBody;
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBody);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
    else
    {
        await next();
    }
});

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
app.MapGet("/api/v5/analysis", async (AnalysisService service, ChppV5 chpp, CancellationToken ct) =>
{
    if (!chpp.Connected) return Results.Unauthorized();
    try { return Results.Ok(await service.RunAsync(build, ct)); }
    catch (Exception ex) { return Results.Problem(ex.Message, statusCode: 502); }
});
app.MapGet("/api/v5/reference-match", async (ReferenceMatchService service, ChppV5 chpp, CancellationToken ct) =>
{
    if (!chpp.Connected) return Results.Unauthorized();
    try { return Results.Ok(await service.GetAsync(ct)); }
    catch (Exception ex) { return Results.Problem(ex.Message, statusCode: 502); }
});
app.MapGet("/api/v5/rating-test", async (RatingTestService service, ChppV5 chpp, CancellationToken ct) =>
{
    if (!chpp.Connected) return Results.Unauthorized();
    try { return Results.Ok(await service.RunAsync(ct)); }
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
