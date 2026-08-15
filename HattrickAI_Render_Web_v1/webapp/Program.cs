using HattrickAI.CHPP;
using HattrickAI.HOEngine;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "hattrickai.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services.AddSingleton<ChppSessionTokenStore>();
builder.Services.AddScoped<ChppOAuthClient>(sp =>
{
    var credentials = ChppSettings.Load(builder.Configuration);
    var store = sp.GetRequiredService<ChppSessionTokenStore>();
    return new ChppOAuthClient(credentials, store, requestedScopes: ChppSettings.RequestedScopes);
});
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

var port = Environment.GetEnvironmentVariable("PORT");
if (int.TryParse(port, out var parsedPort))
    app.Urls.Add($"http://0.0.0.0:{parsedPort}");
else
    app.Urls.Add("http://0.0.0.0:10000");

app.UseSession();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "HattrickAI Web" }));

app.MapGet("/auth/chpp/start", async (HttpContext http, ChppOAuthClient oauth) =>
{
    try
    {
        var proto = http.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? http.Request.Scheme;
        var callback = $"{proto}://{http.Request.Host}/auth/chpp/callback";
        var authorizeUrl = await oauth.BeginAuthorizationAsync(callback, http.RequestAborted);
        return Results.Redirect(authorizeUrl);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

app.MapGet("/auth/chpp/callback", async (HttpContext http, ChppOAuthClient oauth) =>
{
    var verifier = http.Request.Query["oauth_verifier"].ToString();
    var token = http.Request.Query["oauth_token"].ToString();
    if (string.IsNullOrWhiteSpace(verifier))
        return Results.BadRequest("CHPP oauth_verifier bulunamadı. CHPP uygulamasının callback adresini bu sitenin /auth/chpp/callback adresi olarak tanımlayın.");

    try
    {
        await oauth.CompleteAuthorizationAsync(verifier, http.RequestAborted);
        return Results.Redirect("/?connected=1");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

app.MapPost("/auth/chpp/logout", async (ChppOAuthClient oauth) =>
{
    try { await oauth.InvalidateStoredAccessTokenAsync(); } catch { }
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/status", async (ChppOAuthClient oauth) =>
{
    try
    {
        var connected = await oauth.ValidateStoredAccessTokenAsync();
        return Results.Ok(new { connected });
    }
    catch
    {
        return Results.Ok(new { connected = false });
    }
});

app.MapGet("/api/team", async (ChppOAuthClient oauth) =>
{
    var service = new ChppTeamDataService(oauth);
    var snapshot = await service.LoadOwnTeamAsync();
    return Results.Ok(snapshot);
});

app.MapGet("/api/fixtures", async (ChppOAuthClient oauth) =>
{
    var team = await new ChppTeamDataService(oauth).LoadOwnTeamAsync();
    var fixtures = await new ChppMatchDataService(oauth).LoadUpcomingFixturesAsync(team.TeamId);
    return Results.Ok(new { teamId = team.TeamId, teamName = team.TeamName, fixtures });
});

app.MapGet("/api/fixture/{matchId:int}", async (int matchId, ChppOAuthClient oauth) =>
{
    var team = await new ChppTeamDataService(oauth).LoadOwnTeamAsync();
    var fixtures = await new ChppMatchDataService(oauth).LoadUpcomingFixturesAsync(team.TeamId);
    var fixture = fixtures.FirstOrDefault(x => x.MatchId == matchId);
    if (fixture is null) return Results.NotFound(new { message = "Maç bulunamadı." });
    var selected = await new ChppMatchDataService(oauth).LoadSelectedMatchAsync(fixture, team.TeamId);
    return Results.Ok(selected);
});

app.MapPost("/api/simulate", (SimulationRequest request) =>
{
    var engine = new SimulationEngine();
    var result = engine.Run(request.Home, request.Away, request.Simulations);
    return Results.Ok(new
    {
        result.Simulations,
        result.HomeWinPercentage,
        result.DrawPercentage,
        result.AwayWinPercentage,
        result.AverageHomeGoals,
        result.AverageAwayGoals,
        MostLikelyScore = result.GetMostLikelyScore()
    });
});

app.MapPost("/api/recommend", (RecommendationRequest request) =>
{
    if (request.Players is null || request.Players.Count < 11)
        return Results.BadRequest(new { message = "En az 11 oyuncu gerekli." });

    var engine = new RecommendationEngine();
    var result = engine.Recommend(request.Players, request.Opponent, Math.Clamp(request.Simulations, 100, 10000), request.IsHome);
    if (result is null)
        return Results.BadRequest(new { message = "Kadronun en iyi 11'i oluşturulamadı." });

    return Results.Ok(new
    {
        result.Formation,
        result.TacticName,
        result.TacticType,
        result.TacticLevel,
        result.Ratings,
        result.Simulation,
        result.SelectionScore,
        result.Explanation,
        Lineup = result.Lineup.Select(p => new { p.PlayerId, p.Name, p.Age, p.Form, p.Stamina, p.Experience })
    });
});

app.MapFallbackToFile("index.html");

app.Run();

public sealed record SimulationRequest(TeamRatings Home, TeamRatings Away, int Simulations = 1000);
public sealed record RecommendationRequest(List<PlayerData> Players, TeamData Opponent, int Simulations = 1000, bool IsHome = true);
