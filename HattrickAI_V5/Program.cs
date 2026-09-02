using System.Net.Http.Headers;
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
<section id=""deployLogBox"" style=""margin-top:14px;background:#fff;border-radius:15px;box-shadow:0 2px 9px #0002;overflow:hidden;border:1px solid #dfe5e1""><button id=""deployLogToggle"" type=""button"" style=""width:100%;border:0;background:#fff;padding:14px 16px;display:flex;align-items:center;justify-content:space-between;color:#27322d;font:800 13px Arial;cursor:pointer""><span>🚀 Deploy logları</span><span id=""deployLogArrow"">⌄</span></button><div id=""deployLogBody"" style=""display:none;border-top:1px solid #e5ebe7""><div style=""padding:9px 12px;display:flex;justify-content:space-between;align-items:center;color:#747c76;font:11px Arial""><span id=""deployLogState"">Kontrol ediliyor…</span><div style=""display:flex;gap:6px""><button id=""deployLogRefresh"" type=""button"" style=""border:1px solid #d5ddd8;background:#f7f9f7;border-radius:7px;padding:5px 8px;cursor:pointer"">↻ Yenile</button><button id=""deployManual"" type=""button"" style=""border:0;background:#2f7d4f;color:#fff;border-radius:7px;padding:6px 10px;cursor:pointer;font-weight:700"">🚀 Manuel Deploy</button></div></div><pre id=""deployLogText"" style=""margin:0;background:#0b1517;color:#d7e1dd;padding:12px;font:11px/1.55 Consolas,'Courier New',monospace;white-space:pre-wrap;max-height:320px;overflow:auto"">Deploy kayıtları bekleniyor…</pre></div></section><script>(function(){const t=document.getElementById('deployLogToggle'),b=document.getElementById('deployLogBody'),a=document.getElementById('deployLogArrow'),s=document.getElementById('deployLogState'),p=document.getElementById('deployLogText'),r=document.getElementById('deployLogRefresh'),m=document.getElementById('deployManual');if(!t)return;let open=false,last='',busy=false;function paint(d){const lines=Array.isArray(d.lines)?d.lines:[];const x=lines.join('\n');if(x!==last){last=x;p.textContent=x||'Henüz deploy kaydı yok.';p.scrollTop=p.scrollHeight}const sep=lines.reduce((i,x,idx)=>/🚀 GitHub deploy başladı/i.test(x)?idx:i,-1);const latest=sep>=0?lines.slice(sep):lines;const bad=latest.some(x=>/DEPLOY FAILED|❌ Container başlamadı|ERROR|HATA/i.test(x));const good=latest.some(x=>/🟢 DEPLOY BAŞARILI/.test(x));s.textContent=bad?'❌ Son deploy hatalı':(good?'🟢 Son deploy başarılı':'⏳ Deploy durumu kontrol ediliyor');s.style.color=bad?'#b33b32':'#267448'}async function load(){try{const q=await fetch('/api/deploy/log?ts='+Date.now(),{cache:'no-store'});if(!q.ok)throw Error('HTTP '+q.status);paint(await q.json())}catch(e){s.textContent='⚠️ Log alınamadı';s.style.color='#b33b32'}}async function manualDeploy(){if(busy)return;if(!confirm('v5 branch için manuel deploy başlatılsın mı?'))return;busy=true;m.disabled=true;m.style.opacity='.6';s.textContent='🚀 Manuel deploy başlatılıyor…';s.style.color='#267448';try{const q=await fetch('/api/deploy/manual',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'});const d=await q.json().catch(()=>({}));if(!q.ok)throw Error(d.message||('HTTP '+q.status));s.textContent='🚀 Deploy tetiklendi — loglar izleniyor';await load()}catch(e){s.textContent='❌ Deploy başlatılamadı: '+e.message;s.style.color='#b33b32'}finally{busy=false;m.disabled=false;m.style.opacity='1'}}t.onclick=()=>{open=!open;b.style.display=open?'block':'none';a.textContent=open?'⌃':'⌄';if(open)load()};r.onclick=load;m.onclick=manualDeploy;setInterval(()=>{if(open)load()},2000)})();</script>";
            const string referenceScript = @"<script>(function(){
function esc(s){return String(s??'').replace(/[&<>]/g,function(m){return({'&':'&amp;','<':'&lt;','>':'&gt;'}[m])})}
function formatDate(v){const d=new Date(v);if(Number.isNaN(d.getTime()))return String(v||'');return d.toLocaleString('tr-TR',{day:'2-digit',month:'2-digit',year:'numeric',hour:'2-digit',minute:'2-digit'})}
async function loadReference(){try{const r=await fetch('/api/v5/reference-match?ts='+Date.now(),{cache:'no-store'});if(!r.ok)return;const m=await r.json();const box=document.getElementById('oppReference'),score=document.getElementById('referenceScore'),details=document.getElementById('referenceDetails');if(!box||!score||!details)return;let result='';if(m.finished&&m.homeGoals!=null&&m.awayGoals!=null){if(m.homeGoals>m.awayGoals)result='Kazanan: '+esc(m.homeTeam)+' • Kaybeden: '+esc(m.awayTeam);else if(m.awayGoals>m.homeGoals)result='Kazanan: '+esc(m.awayTeam)+' • Kaybeden: '+esc(m.homeTeam);else result='Sonuç: Berabere';}score.innerHTML=esc(m.homeTeam)+' '+(m.homeGoals??'—')+' – '+(m.awayGoals??'—')+' '+esc(m.awayTeam);details.innerHTML='<span class=reference-type>'+esc(m.matchTypeName||'Maç')+'</span>'+formatDate(m.date)+(result?' • '+result:'');box.style.display='block'}catch(e){}}
function watch(){const runtime=document.getElementById('runtime');if(!runtime)return;let last='';const check=()=>{const text=runtime.textContent||'';if(text!==last){last=text;if(/analiz tamamlandı/i.test(text))loadReference()}};new MutationObserver(check).observe(runtime,{subtree:true,childList:true,characterData:true});check()}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',watch);else watch();
})();</script>";
            if (html.Contains(marker, StringComparison.OrdinalIgnoreCase))
                html = html.Replace(marker, box + referenceScript + marker, StringComparison.OrdinalIgnoreCase);

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

var build = Environment.GetEnvironmentVariable("V5_BUILD")
    ?? Environment.GetEnvironmentVariable("BUILD_SHA")
    ?? Environment.GetEnvironmentVariable("GITHUB_SHA")
    ?? "dev";
if (build.Length > 7) build = build[..7];

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "HattrickAI V5", build }));
app.MapGet("/api/v5/build", () => Results.Ok(new { build }));
app.MapGet("/api/v5/status", (ChppV5 chpp) => Results.Ok(new { connected = chpp.Connected, configured = !string.IsNullOrWhiteSpace(builder.Configuration["CHPP_CONSUMER_SECRET"]) }));
app.MapGet("/api/v5/motor-logs", (HttpContext http) =>
{
    var log = MotorRunLogStore.GetLatest(http.Session.Id);
    return log is null ? Results.Ok(new { available = false }) : Results.Ok(new { available = true, log });
});
app.MapGet("/api/deploy/log", () =>
{
    const string logPath = "/app/deploy.log";
    if (!File.Exists(logPath)) return Results.Ok(new { lines = Array.Empty<string>(), updated = false });
    return Results.Ok(new { lines = File.ReadLines(logPath).TakeLast(150).ToArray(), updated = true });
});

app.MapPost("/api/deploy/manual", async (HttpContext http, ChppV5 chpp, CancellationToken ct) =>
{
    if (!chpp.Connected) return Results.Unauthorized();
    var token = builder.Configuration["GITHUB_ACTIONS_TOKEN"]?.Trim();
    if (string.IsNullOrWhiteSpace(token)) return Results.Problem("GITHUB_ACTIONS_TOKEN Azure Environment Variables içinde tanımlı değil.", statusCode: 503);
    try
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HattrickAI-V5");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var content = new StringContent(JsonSerializer.Serialize(new { @ref = "v5" }), System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.github.com/repos/dincer552/ho-ai/actions/workflows/v5-build.yml/dispatches", content, ct);
        if (!response.IsSuccessStatusCode) return Results.Problem($"GitHub workflow_dispatch başarısız ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(ct)}", statusCode: 502);
        return Results.Ok(new { ok = true, message = "v5 deploy workflow tetiklendi." });
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return Results.StatusCode(499); }
    catch (Exception ex) { return Results.Problem(ex.Message, statusCode: 502); }
});

app.MapPost("/api/v5/questionnaire", (HttpContext http, QuestionnaireRequest request) =>
{
    if (!Enum.TryParse<CoachStyle>(request.CoachStyle, true, out var coach)) return Results.BadRequest(new { message = "Teknik direktör seçimi geçersiz." });
    if (!Enum.TryParse<TeamSpiritLevel>(request.TeamSpirit, true, out var spirit)) return Results.BadRequest(new { message = "Takım ruhu seçimi geçersiz." });
    if (!Enum.TryParse<TeamAttitude>(request.MatchImportance, true, out var attitude)) return Results.BadRequest(new { message = "Maç önemi geçersiz." });
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
    var runId = MotorRunLogStore.Start(http.Session.Id);
    try
    {
        using var logScope = MotorRunLogContext.Push(runId);
        MotorRunLogStore.StartMotor(runId, "M3", "CHPP verileri hazırlanıyor");
        var questionnaire = new MatchQuestionnaire(
            Enum.TryParse<CoachStyle>(http.Session.GetString("v5.coach"), true, out var coach) ? coach : CoachStyle.Neutral,
            Enum.TryParse<TeamSpiritLevel>(http.Session.GetString("v5.spirit"), true, out var spirit) ? spirit : TeamSpiritLevel.Composed,
            Enum.TryParse<TeamAttitude>(http.Session.GetString("v5.attitude"), true, out var attitude) ? attitude : TeamAttitude.Normal);
        var result = await service.RunAsync(build, questionnaire, ct);
        MotorRunLogStore.Finish(runId, true, "Analiz tamamlandı");
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        MotorRunLogStore.Finish(runId, false, ex.Message);
        return Results.Problem(ex.Message, statusCode: 502);
    }
});

app.MapGet("/api/v5/offline-export", async (HttpContext http, AnalysisService service, ChppV5 chpp, CancellationToken ct) =>
{
    if (!chpp.Connected) return Results.Unauthorized();
    try
    {
        var questionnaire = new MatchQuestionnaire(
            Enum.TryParse<CoachStyle>(http.Session.GetString("v5.coach"), true, out var coach) ? coach : CoachStyle.Neutral,
            Enum.TryParse<TeamSpiritLevel>(http.Session.GetString("v5.spirit"), true, out var spirit) ? spirit : TeamSpiritLevel.Composed,
            Enum.TryParse<TeamAttitude>(http.Session.GetString("v5.attitude"), true, out var attitude) ? attitude : TeamAttitude.Normal);
        var exporter = new OfflineExportService(chpp, service);
        return Results.Ok(await exporter.ExportAsync(build, questionnaire, ct));
    }
    catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
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
        if (string.IsNullOrWhiteSpace(builder.Configuration["CHPP_CONSUMER_SECRET"])) return Results.Redirect("/?error=" + Uri.EscapeDataString("CHPP_CONSUMER_SECRET Azure Environment Variables içinde tanımlı değil."));
        var proto = http.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? http.Request.Scheme;
        var callback = $"{proto}://{http.Request.Host}/auth/chpp/callback";
        return Results.Redirect(await chpp.StartAsync(callback, ct));
    }
    catch (Exception ex) { return Results.Redirect("/?error=" + Uri.EscapeDataString(ex.Message)); }
});

app.MapGet("/auth/chpp/callback", async (HttpContext http, ChppV5 chpp, CancellationToken ct) =>
{
    var verifier = http.Request.Query["oauth_verifier"].ToString();
    if (string.IsNullOrWhiteSpace(verifier)) return Results.Redirect("/?error=" + Uri.EscapeDataString("CHPP doğrulama kodu alınamadı."));
    try { await chpp.FinishAsync(verifier, ct); return Results.Redirect("/?connected=1"); }
    catch (Exception ex) { return Results.Redirect("/?error=" + Uri.EscapeDataString(ex.Message)); }
});

app.MapPost("/auth/chpp/logout", (ChppV5 chpp) => { chpp.Disconnect(); return Results.Ok(new { ok = true }); });
app.Run();

public sealed record QuestionnaireRequest(string CoachStyle, string TeamSpirit, string MatchImportance);
