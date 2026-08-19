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
                // GetPlayerPositionRating is the internal lineup-selection score.
                // Normalize only the UI value to the same human-readable scale
                // used by historical match ratings; selection logic is unchanged.
                rating = Math.Round(ProjectedPlayerRating(ratingEngine, p, roles[i], behaviours != null && behaviours.TryGetValue(i, out var b) ? b : PlayerBehaviour.Normal), 2),
                behaviour = behaviours != null && behaviours.TryGetValue(i, out var behaviour) ? behaviour.ToString() : "Normal"
            }).ToArray()
            : Array.Empty<object>();
        return new { formation, ratings, playerCount = lineup.Count, players };
    }

    private static double ProjectedPlayerRating(LineupRatingEngine ratingEngine, PlayerData player, PlayerRole role, PlayerBehaviour behaviour)
    {
        // The internal position score is on a larger scale than the player
        // match/star display. Keep the original score for player selection and
        // convert only the value rendered on the lineup card.
        return Math.Clamp(ratingEngine.GetPlayerPositionRating(player, role, behaviour) / 2.5, 0.0, 10.0);
    }

    public static object BuildHistoricalLineupView(IReadOnlyList<ChppLineupPlayer> players, string formation, TeamRatings ratings)
    {
        // Historical CHPP lineup order is not guaranteed to match the canonical
        // formation slot order. Use each player's actual PositionCode instead of
        // assigning roles by array index; otherwise a valid 3-4-3/4-4-2 etc. can
        // render as a single horizontal line on the pitch.
        var result = players.Select(p =>
        {
            var role = HistoricalFormationMapper.HistoricalRole(p, formation);
            return (object)new
            {
                p.PlayerId,
                p.Name,
                role = RoleLabel(role.ToString()),
                roleKey = role.ToString(),
                rating = Math.Round(p.RatingStars, 2),
                historicalMatchRating = Math.Round(p.RatingStars, 2),
                behaviour = MapBehaviour(p.Behaviour).ToString()
            };
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
        "LeftWinger" => "K", "RightWinger" => "K",
        "LeftForward" => "SF", "CentralForward" => "SF", "RightForward" => "SF",
        _ => ""
    };
}
