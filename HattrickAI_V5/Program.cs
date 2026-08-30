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

// Ana sayfaya deploy loglarını açılır/kapanır kutu olarak enjekte et.
// Böylece mevcut V5 index tasarımı değiştirilmeden Azure VM'deki /app/deploy.log
// API üzerinden canlı loglar ana sayfanın en altında gösterilir.
app.Use(async (context, next) =>
{
    if (context.Request.Method == "GET" && context.Request.Path == "/")
    {
        var indexPath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
        if (File.Exists(indexPath))
        {
            var html = await File.ReadAllTextAsync(indexPath, context.RequestAborted);
            const string marker = "</main>";
            const string box = @"
<section id=""deployLogBox"" style=""margin-top:14px;background:#fff;border-radius:15px;box-shadow:0 2px 9px #0002;overflow:hidden;border:1px solid #dfe5e1""><button id=""deployLogToggle"" type=""button"" style=""width:100%;border:0;background:#fff;padding:14px 16px;display:flex;align-items:center;justify-content:space-between;color:#27322d;font:800 13px Arial;cursor:pointer""><span>🚀 Deploy logları</span><span id=""deployLogArrow"">⌄</span></button><div id=""deployLogBody"" style=""display:none;border-top:1px solid #e5ebe7""><div style=""padding:9px 12px;display:flex;justify-content:space-between;align-items:center;color:#747c76;font:11px Arial""><span id=""deployLogState"">Kontrol ediliyor…</span><button id=""deployLogRefresh"" type=""button"" style=""border:1px solid #d5ddd8;background:#f7f9f7;border-radius:7px;padding:5px 8px;cursor:pointer"">↻ Yenile</button></div><pre id=""deployLogText"" style=""margin:0;background:#0b1517;color:#d7e1dd;padding:12px;font:11px/1.55 Consolas,'Courier New',monospace;white-space:pre-wrap;max-height:320px;overflow:auto"">Deploy kayıtları bekleniyor…</pre></div></section><script>(function(){const t=document.getElementById('deployLogToggle'),b=document.getElementById('deployLogBody'),a=document.getElementById('deployLogArrow'),s=document.getElementById('deployLogState'),p=document.getElementById('deployLogText'),r=document.getElementById('deployLogRefresh');if(!t)return;let open=false,last='';function paint(d){const lines=Array.isArray(d.lines)?d.lines:[];const x=lines.join('\n');if(x!==last){last=x;p.textContent=x||'Henüz deploy kaydı yok.';p.scrollTop=p.scrollHeight}const bad=lines.some(x=>/FAILED|HATA|ERROR|❌|failed/i.test(x));s.textContent=bad?'❌ Son deploy hatalı':(/BAŞARILI|🟢/.test(lines.at(-1)||'')?'🟢 Son deploy başarılı':'⏳ Deploy durumu kontrol ediliyor');s.style.color=bad?'#b33b32':'#267448'}async function load(){try{const q=await fetch('/api/deploy/log?ts='+Date.now(),{cache:'no-store'});if(!q.ok)throw Error('HTTP '+q.status);paint(await q.json())}catch(e){s.textContent='⚠️ Log alınamadı';s.style.color='#b33b32'}}t.onclick=()=>{open=!open;b.style.display=open?'block':'none';a.textContent=open?'⌃':'⌄';if(open)load()};r.onclick=load;setInterval(()=>{if(open)load()},2000)})();</script>";
            if (html.Contains(marker, StringComparison.OrdinalIgnoreCase))
                html = html.Replace(marker, box + marker, StringComparison.OrdinalIgnoreCase);

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsync(html, context.RequestAborted);
            return;
        }
    }

    await next();
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
