using HattrickAI.CHPP;
using HattrickAI.HOEngine;
using HattrickAI.Web;
using Microsoft.AspNetCore.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using static HattrickAI.Web.LineupViewHelpers;

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
builder.Services.AddSingleton<PostgresHistoricalCache>();
builder.Services.AddScoped<ChppOAuthClient>(sp =>
{
    var credentials = ChppSettings.Load(builder.Configuration);
    var store = sp.GetRequiredService<ChppSessionTokenStore>();
    return new ChppOAuthClient(credentials, store, requestedScopes: ChppSettings.RequestedScopes);
});
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"UNHANDLED REQUEST ERROR: {ex}");
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new { message = $"Sunucu hatası: {ex.Message}", errorType = ex.GetType().Name, chppTrace = ChppRequestTrace.Current?.ToResponse() });
        }
    }
});
var port = Environment.GetEnvironmentVariable("PORT");
app.Urls.Add($"http://0.0.0.0:{(int.TryParse(port, out var p) ? p : 10000)}");
app.UseSession();

app.Use(async (context, next) =>
{
    var rewriteVersion = context.Request.Path == "/" || context.Request.Path == "/index.html" || context.Request.Path == "/selection-fix.js";
    if (!rewriteVersion) { await next(); return; }
    var originalBody = context.Response.Body;
    await using var buffer = new MemoryStream();
    context.Response.Body = buffer;
    try
    {
        await next();
        if (context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true || context.Response.ContentType?.Contains("javascript", StringComparison.OrdinalIgnoreCase) == true)
        {
            buffer.Position = 0;
            using var reader = new StreamReader(buffer);
            var text = await reader.ReadToEndAsync();
            text = text.Replace("__HATTRICKAI_VERSION__", AppVersion.Display, StringComparison.Ordinal);
            context.Response.ContentLength = null;
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            context.Response.Body = originalBody;
            await context.Response.WriteAsync(text);
        }
        else { buffer.Position = 0; context.Response.Body = originalBody; await buffer.CopyToAsync(originalBody); }
    }
    finally { context.Response.Body = originalBody; }
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", (PostgresHistoricalCache cache) => Results.Ok(new { ok = true, service = "HattrickAI Web", version = AppVersion.Display, historicalCache = cache.IsConfigured ? "POSTGRES" : "UNCONFIGURED" }));
app.MapGet("/api/version", () => Results.Ok(new { version = AppVersion.Display, source = AppVersion.SourceFileName }));

app.MapGet("/auth/chpp/start", async (HttpContext http) =>
{
    try
    {
        var oauth = http.RequestServices.GetRequiredService<ChppOAuthClient>();
        var proto = http.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? http.Request.Scheme;
        return Results.Redirect(await oauth.BeginAuthorizationAsync($"{proto}://{http.Request.Host}/auth/chpp/callback", http.RequestAborted));
    }
    catch (Exception ex) { return Results.Problem($"CHPP bağlantısı başlatılamadı. {ex.Message}", statusCode: 500); }
});
app.MapGet("/auth/chpp/callback", async (HttpContext http) =>
{
    var verifier = http.Request.Query["oauth_verifier"].ToString();
    if (string.IsNullOrWhiteSpace(verifier)) return Results.BadRequest("CHPP oauth_verifier bulunamadı.");
    try { await http.RequestServices.GetRequiredService<ChppOAuthClient>().CompleteAuthorizationAsync(verifier, http.RequestAborted); return Results.Redirect("/?connected=1"); }
    catch (Exception ex) { return Results.Problem($"CHPP bağlantısı tamamlanamadı. {ex.Message}", statusCode: 500); }
});
app.MapPost("/auth/chpp/logout", async (ChppOAuthClient oauth) => { try { await oauth.InvalidateStoredAccessTokenAsync(); } catch { } return Results.Ok(new { ok = true }); });
app.MapGet("/api/status", async (ChppOAuthClient oauth) => { try { return Results.Ok(new { connected = await oauth.ValidateStoredAccessTokenAsync() }); } catch { return Results.Ok(new { connected = false }); } });
app.MapGet("/api/team", async (ChppOAuthClient oauth) => Results.Ok(await new ChppTeamDataService(oauth).LoadOwnTeamAsync()));
app.MapGet("/api/training", async (ChppOAuthClient oauth) =>
{
    try { return Results.Ok(await new ChppTrainingDataService(oauth).LoadOwnTrainingAsync()); }
    catch (Exception ex) { return Results.Problem($"Antrenman bilgisi alınamadı. {ex.Message}", statusCode: 502); }
});

app.MapGet("/api/cup-lineup/latest", async (HttpContext http, ChppOAuthClient oauth, PostgresHistoricalCache cache) =>
{
    using var traceScope = ChppRequestTrace.Begin("cup-lineup-latest", 0, null);
    var trace = ChppRequestTrace.Current!;
    var teamService = new ChppTeamDataService(oauth);
    var matchService = new ChppMatchDataService(oauth);
    var lineupService = new ChppMatchLineupService(oauth);
    var own = await teamService.LoadOwnTeamAsync();
    var pointerKey = $"cup-latest:{own.TeamId}";
    var cached = await cache.GetSelectedMatchAsync(pointerKey, http.RequestAborted);
    ChppFixture? fixture = cached?.Fixture;
    TeamData? teamData = cached?.OwnTeamRatings;
    IReadOnlyList<ChppLineupPlayer>? players = null;
    var source = "POSTGRES_CACHE";
    if (fixture is null || !fixture.IsStandardCup)
    {
        fixture = await matchService.LoadLatestStandardCupFixtureAsync(own.TeamId, http.RequestAborted);
        if (fixture is null) return Results.NotFound(new { message = "Standart kupa maçı bulunamadı.", chppTrace = trace.ToResponse() });
        teamData = await matchService.LoadTeamDataFromHistoricalMatchAsync(fixture, own.TeamId, http.RequestAborted);
        var pointer = new ChppSelectedMatch(fixture, fixture.OpponentTeamId(own.TeamId), fixture.OpponentName(own.TeamId), teamData, teamData, Array.Empty<ChppOpponentMatch>());
        await cache.SetSelectedMatchAsync(pointerKey, pointer, own.TeamId, fixture.OpponentTeamId(own.TeamId), http.RequestAborted);
        source = "CHPP_DOWNLOADED_AND_CACHED";
    }
    var lineupKey = $"cup-lineup:{fixture.MatchId}:{own.TeamId}";
    players = await cache.GetLineupAsync(lineupKey, http.RequestAborted);
    if (players is null || players.Count != 11)
    {
        players = await lineupService.LoadAsync(fixture.MatchId, own.TeamId, http.RequestAborted);
        if (players.Count == 11) await cache.SetLineupAsync(lineupKey, fixture.MatchId, own.TeamId, players, http.RequestAborted);
        source = "CHPP_DOWNLOADED_AND_CACHED";
    }
    if (players.Count != 11) return Results.BadRequest(new { message = "Son kupa maçının gerçek 11'i alınamadı.", playerCount = players.Count, chppTrace = trace.ToResponse() });
    var formation = HistoricalFormationMapper.InferFormation(players);
    if (string.IsNullOrWhiteSpace(formation)) return Results.BadRequest(new { message = "Kupa formasyonu çözülemedi.", chppTrace = trace.ToResponse() });
    var record = new CupLineupRecord(fixture, own.TeamId, own.TeamName, teamData!, formation, players);
    return Results.Ok(new { record, cache = source, playerCount = players.Count, chppTrace = trace.ToResponse() });
});

app.MapGet("/api/fixtures", async (ChppOAuthClient oauth) =>
{
    var team = await new ChppTeamDataService(oauth).LoadOwnTeamAsync();
    var fixtures = await new ChppMatchDataService(oauth).LoadUpcomingFixturesAsync(team.TeamId);
    return Results.Ok(new { teamId = team.TeamId, teamName = team.TeamName, fixtures });
});

app.MapGet("/api/fixture-view/{matchId:int}", async (HttpContext http, int matchId, int? recentIndex, bool? refresh, ChppOAuthClient oauth, PostgresHistoricalCache cache) =>
{
    using var traceScope = ChppRequestTrace.Begin("fixture-view", matchId, recentIndex);
    var trace = ChppRequestTrace.Current!;
    var teamService = new ChppTeamDataService(oauth);
    var matchService = new ChppMatchDataService(oauth);
    var lineupService = new ChppMatchLineupService(oauth);
    var own = await teamService.LoadOwnTeamAsync();
    TrainingRecommendationProfile? training = null;
    try { training = await new ChppTrainingDataService(oauth).LoadOwnTrainingAsync(http.RequestAborted); }
    catch (Exception ex) { Console.WriteLine($"TRAINING LOAD FAILED: {ex.Message}"); }

    var fixtures = await matchService.LoadUpcomingFixturesAsync(own.TeamId);
    var fixture = fixtures.FirstOrDefault(x => x.MatchId == matchId);
    if (fixture is null) return Results.NotFound(new { message = "Maç bulunamadı.", chppTrace = trace.ToResponse() });
    var opponentId = fixture.OpponentTeamId(own.TeamId);
    var historyKey = $"opponent-history:{own.TeamId}:{opponentId}";
    var selected = await cache.GetSelectedMatchAsync(historyKey, http.RequestAborted);
    var historySource = "POSTGRES_CACHE";
    if (selected is null || refresh == true)
    {
        selected = await matchService.LoadSelectedMatchAsync(fixture, own.TeamId, http.RequestAborted);
        await cache.SetSelectedMatchAsync(historyKey, selected, own.TeamId, opponentId, http.RequestAborted);
        historySource = "CHPP_DOWNLOADED_AND_CACHED";
    }
    var opponentTeamId = selected.OpponentTeamId > 0 ? selected.OpponentTeamId : opponentId;
    var opponentTeamName = !string.IsNullOrWhiteSpace(selected.OpponentTeamName) ? selected.OpponentTeamName : fixture.OpponentName(own.TeamId);
    var isHome = fixture.IsOwnHome(own.TeamId);
    var historyIndex = Math.Clamp(recentIndex ?? 0, 0, Math.Max(0, selected.RecentMatches.Count - 1));
    var selectedHistory = selected.RecentMatches.Count > 0 ? selected.RecentMatches[historyIndex] : null;
    if (selectedHistory is null) return Results.BadRequest(new { message = "Rakibin seçilmiş geçmiş maçı bulunamadı.", ratingSource = "POSTGRES_HISTORY_EMPTY", chppTrace = trace.ToResponse() });
    var lineupKey = $"lineup:{selectedHistory.Fixture.MatchId}:{opponentTeamId}";
    var historicalPlayers = await cache.GetLineupAsync(lineupKey, http.RequestAborted);
    var lineupSource = "POSTGRES_CACHE";
    if (historicalPlayers is null || historicalPlayers.Count != 11)
    {
        historicalPlayers = await lineupService.LoadAsync(selectedHistory.Fixture.MatchId, opponentTeamId, http.RequestAborted);
        if (historicalPlayers.Count == 11) await cache.SetLineupAsync(lineupKey, selectedHistory.Fixture.MatchId, opponentTeamId, historicalPlayers, http.RequestAborted);
        lineupSource = "CHPP_DOWNLOADED_AND_CACHED";
    }
    if (historicalPlayers.Count != 11) return Results.BadRequest(new { message = "Rakibin geçmiş maç kadrosu alınamadı.", ratingSource = "CHPP_MATCH_LINEUP_INCOMPLETE", playerCount = historicalPlayers.Count, chppTrace = trace.ToResponse() });
    var opponentFormation = HistoricalFormationMapper.InferFormation(historicalPlayers);
    if (string.IsNullOrWhiteSpace(opponentFormation)) return Results.BadRequest(new { message = "Rakibin geçmiş maç formasyonu çözülemedi.", chppTrace = trace.ToResponse() });

    var baseAnalysisKey = PostgresHistoricalCache.AnalysisKey(matchId, selectedHistory.Fixture.MatchId, own.TeamId, isHome, own.Players);
    var trainingKey = training == null ? "no-training" : $"tr{training.TrainingType}-exp{string.Join("-", training.FormationExperience.OrderBy(x => x.Key).Select(x => $"{x.Key.Replace("-", "", StringComparison.Ordinal)}{x.Value}"))}";
    var analysisKey = $"{baseAnalysisKey}:{trainingKey}";
    var cachedAnalysis = await cache.GetAnalysisAsync(analysisKey, http.RequestAborted);
    if (!string.IsNullOrWhiteSpace(cachedAnalysis)) return Results.Content(cachedAnalysis, "application/json");

    var simulationOpponent = new TeamData(selectedHistory.OpponentTeam.TeamName, selectedHistory.OpponentTeam.Ratings, selectedHistory.OpponentTeam.TacticType, selectedHistory.OpponentTeam.TacticLevel);
    var recommendation = new RecommendationEngine().Recommend(
        own.Players,
        simulationOpponent,
        10000,
        isHome,
        training?.TrainingType ?? -1,
        training?.FormationExperience);
    if (recommendation is null) return Results.BadRequest(new { message = "Kendi kadron için en iyi 11 oluşturulamadı.", chppTrace = trace.ToResponse() });
    var opponentLineupView = BuildHistoricalLineupView(historicalPlayers, opponentFormation, selectedHistory.OpponentTeam.Ratings);
    var response = new
    {
        fixture, isHome, selectedRecentIndex = historyIndex,
        cache = new { mode = "CACHE_FIRST", history = historySource, lineup = lineupSource, analysis = "COMPUTED_AND_CACHED", database = cache.IsConfigured ? "POSTGRES" : "UNCONFIGURED" },
        training = training == null ? null : new
        {
            training.TrainingType,
            training.TrainingName,
            training.TrainingLevel,
            training.StaminaTrainingPart,
            training.FormationExperience,
            preferredFormations = new[] { "4-3-3", "4-5-1", "3-5-2", "5-3-2", "3-4-3", "5-4-1" }
                .OrderByDescending(f => RecommendationEngine.TrainingFormationFit(training.TrainingType, f))
                .ThenByDescending(f => training.Experience(f))
                .ToArray()
        },
        selectedOpponentMatch = new { selectedHistory.Fixture, selectedHistory.OpponentTeam.TeamName, selectedHistory.OpponentTeam.TacticType, selectedHistory.OpponentTeam.TacticLevel, actualMatchRatings = selectedHistory.OpponentTeam.Ratings, ratingSource = "HO_TEAM_ANALYZER_HISTORICAL_MATCH_RATINGS" },
        ownTeam = new { teamId = own.TeamId, teamName = own.TeamName },
        opponentTeam = new { teamId = opponentTeamId, teamName = opponentTeamName },
        ownLineup = BuildLineupView(recommendation.Lineup, recommendation.Formation, recommendation.BehaviourProfile, recommendation.Ratings),
        opponentLineup = opponentLineupView, ownRatings = recommendation.Ratings, opponentRatings = selectedHistory.OpponentTeam.Ratings,
        formation = recommendation.Formation,
        tactic = new { recommendation.TacticName, recommendation.TacticType, recommendation.TacticLevel },
        recommendation = new { recommendation.Explanation, recommendation.SelectionScore, recommendation.TrainingFit, recommendation.FormationExperience, recommendation.TrainingName, recommendation.TrainingPriority },
        recentMatches = selected.RecentMatches.Select(m => new { m.Fixture, opponent = new { m.OpponentTeam.TeamName, actualMatchRatings = m.OpponentTeam.Ratings, m.OpponentTeam.TacticType, m.OpponentTeam.TacticLevel } }),
        chppTrace = trace.ToResponse()
    };
    var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    await cache.SetAnalysisAsync(analysisKey, matchId, selectedHistory.Fixture.MatchId, own.TeamId, json, http.RequestAborted);
    return Results.Content(json, "application/json");
});

app.MapPost("/api/simulate", (SimulationRequest request) =>
{
    const int simulationCount = 10000;
    var engine = new SimulationEngine();
    var result = engine.Run(request.Home, request.Away, simulationCount, request.HomeTacticType, request.HomeTacticLevel, request.AwayTacticType, request.AwayTacticLevel);
    var sectors = engine.CompareSectors(request.Home, request.Away);
    return Results.Ok(new { result.Simulations, result.HomeWinPercentage, result.DrawPercentage, result.AwayWinPercentage, result.AverageHomeGoals, result.AverageAwayGoals, MostLikelyNormalScore = result.GetMostLikelyNormalScore(), ScoreDistribution = result.GetScoreDistribution(), SectorComparison = sectors, ScoreModel = "HO! ActionGenerator / MatchResult", SectorModel = "HO! BaseActionGenerator.compare / linear chance" });
});

app.MapPost("/api/recommend", async (RecommendationRequest request, ChppOAuthClient oauth) =>
{
    if (request.Players is null || request.Players.Count < 11) return Results.BadRequest(new { message = "En az 11 oyuncu gerekli." });

    TrainingRecommendationProfile? training = null;
    try { training = await new ChppTrainingDataService(oauth).LoadOwnTrainingAsync(); }
    catch (Exception ex) { Console.WriteLine($"TRAINING LOAD FAILED /api/recommend: {ex.Message}"); }

    var result = new RecommendationEngine().Recommend(
        request.Players,
        request.Opponent,
        10000,
        request.IsHome,
        training?.TrainingType ?? -1,
        training?.FormationExperience);
    if (result is null) return Results.BadRequest(new { message = "Kadronun en iyi 11'i oluşturulamadı." });
    var roles = LineupRatingEngine.GetRoles(result.Formation);
    var ratingEngine = new LineupRatingEngine();
    var lineup = result.Lineup.Select((p, i) =>
    {
        var behaviour = result.BehaviourProfile.TryGetValue(i, out var b) ? b : PlayerBehaviour.Normal;
        return new
        {
            p.PlayerId,
            p.Name,
            p.Age,
            p.Form,
            p.Stamina,
            p.Experience,
            roleKey = roles[i].ToString(),
            rating = Math.Round(ratingEngine.GetPlayerPositionRating(p, roles[i], behaviour), 2),
            behaviour = behaviour.ToString()
        };
    }).ToArray();
    return Results.Ok(new { result.Formation, result.TacticName, result.TacticType, result.TacticLevel, result.Ratings, result.Simulation, result.SelectionScore, result.Explanation, result.TrainingFit, result.FormationExperience, result.TrainingName, result.TrainingPriority, Lineup = lineup });
});
app.MapFallbackToFile("index.html");
app.Run();