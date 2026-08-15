using HattrickAI.CHPP;
using HattrickAI.HOEngine;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options=>{options.Cookie.Name="hattrickai.session";options.Cookie.HttpOnly=true;options.Cookie.SameSite=SameSiteMode.Lax;options.Cookie.SecurePolicy=CookieSecurePolicy.None;options.IdleTimeout=TimeSpan.FromHours(8);});
builder.Services.AddSingleton<ChppSessionTokenStore>();
builder.Services.AddScoped<ChppOAuthClient>(sp=>{var credentials=ChppSettings.Load(builder.Configuration);var store=sp.GetRequiredService<ChppSessionTokenStore>();return new ChppOAuthClient(credentials,store,requestedScopes:ChppSettings.RequestedScopes);});
builder.Services.Configure<JsonOptions>(options=>{options.SerializerOptions.PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.CamelCase;options.SerializerOptions.DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull;});

var app=builder.Build();
var port=Environment.GetEnvironmentVariable("PORT");
if(int.TryParse(port,out var parsedPort))app.Urls.Add($"http://0.0.0.0:{parsedPort}");else app.Urls.Add("http://0.0.0.0:10000");
app.UseSession();app.UseDefaultFiles();app.UseStaticFiles();
app.MapGet("/health",()=>Results.Ok(new{ok=true,service="HattrickAI Web"}));
app.MapGet("/auth/chpp/start",async(HttpContext http)=>{try{var oauth=http.RequestServices.GetRequiredService<ChppOAuthClient>();var proto=http.Request.Headers["X-Forwarded-Proto"].FirstOrDefault()??http.Request.Scheme;var callback=$"{proto}://{http.Request.Host}/auth/chpp/callback";var authorizeUrl=await oauth.BeginAuthorizationAsync(callback,http.RequestAborted);return Results.Redirect(authorizeUrl);}catch(Exception ex){Console.Error.WriteLine($"CHPP START ERROR: {ex}");return Results.Problem(detail:$"CHPP bağlantısı başlatılamadı. {ex.Message}",statusCode:500,title:"CHPP OAuth başlatma hatası");}});
app.MapGet("/auth/chpp/callback",async(HttpContext http)=>{var verifier=http.Request.Query["oauth_verifier"].ToString();if(string.IsNullOrWhiteSpace(verifier))return Results.BadRequest("CHPP oauth_verifier bulunamadı.");try{var oauth=http.RequestServices.GetRequiredService<ChppOAuthClient>();await oauth.CompleteAuthorizationAsync(verifier,http.RequestAborted);return Results.Redirect("/?connected=1");}catch(Exception ex){Console.Error.WriteLine($"CHPP CALLBACK ERROR: {ex}");return Results.Problem(detail:$"CHPP bağlantısı tamamlanamadı. {ex.Message}",statusCode:500,title:"CHPP OAuth callback hatası");}});
app.MapPost("/auth/chpp/logout",async(ChppOAuthClient oauth)=>{try{await oauth.InvalidateStoredAccessTokenAsync();}catch{}return Results.Ok(new{ok=true});});
app.MapGet("/api/status",async(ChppOAuthClient oauth)=>{try{return Results.Ok(new{connected=await oauth.ValidateStoredAccessTokenAsync()});}catch{return Results.Ok(new{connected=false});}});
app.MapGet("/api/team",async(ChppOAuthClient oauth)=>Results.Ok(await new ChppTeamDataService(oauth).LoadOwnTeamAsync()));
app.MapGet("/api/fixtures",async(ChppOAuthClient oauth)=>{var team=await new ChppTeamDataService(oauth).LoadOwnTeamAsync();var fixtures=await new ChppMatchDataService(oauth).LoadUpcomingFixturesAsync(team.TeamId);return Results.Ok(new{teamId=team.TeamId,teamName=team.TeamName,fixtures});});

app.MapGet("/api/fixture-view/{matchId:int}",async(int matchId,int? recentIndex,ChppOAuthClient oauth)=>
{
    var teamService=new ChppTeamDataService(oauth);var matchService=new ChppMatchDataService(oauth);var own=await teamService.LoadOwnTeamAsync();
    var fixtures=await matchService.LoadUpcomingFixturesAsync(own.TeamId);var fixture=fixtures.FirstOrDefault(x=>x.MatchId==matchId);if(fixture is null)return Results.NotFound(new{message="Maç bulunamadı."});
    var selected=await matchService.LoadSelectedMatchAsync(fixture,own.TeamId);var opponent=await teamService.LoadTeamAsync(selected.OpponentTeamId,selected.OpponentTeamName);var isHome=fixture.IsOwnHome(own.TeamId);
    var historyIndex=Math.Clamp(recentIndex??0,0,Math.Max(0,selected.RecentMatches.Count-1));var selectedHistory=selected.RecentMatches.Count>0?selected.RecentMatches[historyIndex]:null;var simulationOpponent=selectedHistory?.OpponentTeam??selected.OpponentRatings;
    var recommendation=new RecommendationEngine().Recommend(own.Players,simulationOpponent,1200,isHome);if(recommendation is null)return Results.BadRequest(new{message="Kendi kadron için en iyi 11 oluşturulamadı."});
    var opponentFormation=recommendation.Formation;var opponentLineup=new BestLineupEngine().FindBestLineupForFormation(opponent.Players,opponentFormation);if(opponentLineup.Count!=11)opponentLineup=opponent.Players.Where(p=>!p.Injured&&!p.Suspended).Take(11).ToList();
    TeamRatings opponentRatings=opponentLineup.Count==11?new LineupRatingEngine().Calculate(opponentLineup,opponentFormation):simulationOpponent.Ratings;
    return Results.Ok(new{fixture,isHome,selectedRecentIndex=historyIndex,selectedOpponentMatch=selectedHistory is null?null:new{selectedHistory.Fixture,selectedHistory.OpponentTeam.TeamName,selectedHistory.OpponentTeam.TacticType,selectedHistory.OpponentTeam.TacticLevel,selectedHistory.OpponentTeam.Ratings},ownTeam=new{teamId=own.TeamId,teamName=own.TeamName},opponentTeam=new{teamId=opponent.TeamId,teamName=opponent.TeamName},ownLineup=BuildLineupView(recommendation.Lineup,recommendation.Formation,recommendation.BehaviourProfile,recommendation.Ratings),opponentLineup=BuildLineupView(opponentLineup,opponentFormation,null,opponentRatings),ownRatings=recommendation.Ratings,opponentRatings,formation=recommendation.Formation,tactic=new{recommendation.TacticName,recommendation.TacticType,recommendation.TacticLevel},recommendation=new{recommendation.Explanation,recommendation.SelectionScore},recentMatches=selected.RecentMatches.Select(m=>new{m.Fixture,opponent=new{m.OpponentTeam.TeamName,m.OpponentTeam.Ratings,m.OpponentTeam.TacticType,m.OpponentTeam.TacticLevel}})});
});

app.MapPost("/api/simulate",(SimulationRequest request)=>{var result=new SimulationEngine().Run(request.Home,request.Away,request.Simulations);return Results.Ok(new{result.Simulations,result.HomeWinPercentage,result.DrawPercentage,result.AwayWinPercentage,result.AverageHomeGoals,result.AverageAwayGoals,MostLikelyScore=result.GetMostLikelyScore()});});
app.MapPost("/api/recommend",(RecommendationRequest request)=>{if(request.Players is null||request.Players.Count<11)return Results.BadRequest(new{message="En az 11 oyuncu gerekli."});var result=new RecommendationEngine().Recommend(request.Players,request.Opponent,Math.Clamp(request.Simulations,100,10000),request.IsHome);if(result is null)return Results.BadRequest(new{message="Kadronun en iyi 11'i oluşturulamadı."});return Results.Ok(new{result.Formation,result.TacticName,result.TacticType,result.TacticLevel,result.Ratings,result.Simulation,result.SelectionScore,result.Explanation,Lineup=result.Lineup.Select(p=>new{p.PlayerId,p.Name,p.Age,p.Form,p.Stamina,p.Experience})});});
app.MapFallbackToFile("index.html");app.Run();

static object BuildLineupView(List<PlayerData> lineup,string formation,IReadOnlyDictionary<int,PlayerBehaviour>? behaviours,TeamRatings ratings)
{
    var roles=LineupRatingEngine.GetRoles(formation);var ratingEngine=new LineupRatingEngine();
    var players=lineup.Count==11?lineup.Select((p,i)=>(object)new{p.PlayerId,p.Name,p.Form,p.Stamina,p.Experience,role=RoleLabel(roles[i].ToString()),roleKey=roles[i].ToString(),rating=Math.Round(ratingEngine.GetPlayerPositionRating(p,roles[i],behaviours!=null&&behaviours.TryGetValue(i,out var b)?b:PlayerBehaviour.Normal),2),behaviour=behaviours!=null&&behaviours.TryGetValue(i,out var behaviour)?behaviour.ToString():"Normal"}).ToArray():Array.Empty<object>();
    return new{formation,ratings,playerCount=lineup.Count,players};
}
static string RoleLabel(string role)=>role switch{"Goalkeeper"=>"KL","LeftDefender"=>"SLB","CentralDefender"=>"STP","RightDefender"=>"SGB","LeftMidfielder"=>"OS","CentralMidfielder"=>"OM","RightMidfielder"=>"OS","LeftWinger"=>"K","RightWinger"=>"K","LeftForward"=>"SF","CentralForward"=>"SF","RightForward"=>"SF",_=>""};
public sealed record SimulationRequest(TeamRatings Home,TeamRatings Away,int Simulations=1000);
public sealed record RecommendationRequest(List<PlayerData> Players,TeamData Opponent,int Simulations=1000,bool IsHome=true);