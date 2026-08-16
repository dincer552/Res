using HattrickAI.CHPP;
using HattrickAI.HOEngine;

namespace HattrickAI.Web;

public static class LineupViewHelpers
{
    public static object BuildLineupView(List<PlayerData> lineup, string formation, IReadOnlyDictionary<int, PlayerBehaviour>? behaviours, TeamRatings ratings)
    {
        var roles = LineupRatingEngine.GetRoles(formation);
        var ratingEngine = new LineupRatingEngine();
        var players = lineup.Count == 11
            ? lineup.Select((p, i) => (object)new
            {
                p.PlayerId, p.Name, p.Form, p.Stamina, p.Experience,
                role = RoleLabel(roles[i].ToString()), roleKey = roles[i].ToString(),
                rating = Math.Round(ratingEngine.GetPlayerPositionRating(p, roles[i], behaviours != null && behaviours.TryGetValue(i, out var b) ? b : PlayerBehaviour.Normal), 2),
                behaviour = behaviours != null && behaviours.TryGetValue(i, out var behaviour) ? behaviour.ToString() : "Normal"
            }).ToArray()
            : Array.Empty<object>();
        return new { formation, ratings, playerCount = lineup.Count, players };
    }

    public static object BuildHistoricalLineupView(IReadOnlyList<ChppLineupPlayer> players, string formation, TeamRatings ratings)
    {
        var roles = LineupRatingEngine.GetRoles(formation);
        var result = players.Select((p, i) => (object)new
        {
            p.PlayerId, p.Name,
            role = RoleLabel(roles[i].ToString()), roleKey = roles[i].ToString(),
            rating = Math.Round(p.RatingStars, 2), historicalMatchRating = Math.Round(p.RatingStars, 2),
            behaviour = MapBehaviour(p.Behaviour).ToString()
        }).ToArray();
        return new { formation, ratings, playerCount = result.Length, players = result, source = "HO_TEAM_ANALYZER_HISTORICAL_MATCH_RATINGS" };
    }

    public static PlayerBehaviour MapBehaviour(int behaviour) => behaviour switch
    {
        1 => PlayerBehaviour.Offensive,
        2 => PlayerBehaviour.Defensive,
        3 => PlayerBehaviour.TowardsMiddle,
        4 => PlayerBehaviour.TowardsWing,
        _ => PlayerBehaviour.Normal
    };

    public static string RoleLabel(string role) => role switch
    {
        "Goalkeeper" => "KL", "LeftDefender" => "SLB", "CentralDefender" => "STP", "RightDefender" => "SGB",
        "LeftMidfielder" => "OS", "CentralMidfielder" => "OM", "RightMidfielder" => "OS",
        "LeftWinger" => "K", "RightWinger" => "K", "LeftForward" => "SF", "CentralForward" => "SF", "RightForward" => "SF",
        _ => ""
    };
}
