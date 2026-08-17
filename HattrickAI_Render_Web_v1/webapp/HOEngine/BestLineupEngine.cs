using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.HOEngine;

public sealed class BestLineupEngine
{
    private readonly LineupRatingEngine _ratingEngine = new();

    public string LastFormationName { get; private set; } = "4-4-2";
    public IReadOnlyDictionary<int, PlayerBehaviour> LastBehaviourProfile { get; private set; } =
        new Dictionary<int, PlayerBehaviour>();

    public static IReadOnlyList<string> SupportedFormations { get; } =
        new[] { "4-4-2", "4-3-3", "3-5-2", "4-5-1", "5-4-1", "5-3-2", "3-4-3" };

    public List<PlayerData> FindBestLineup(List<PlayerData> players)
    {
        if (players == null || players.Count < 11)
            return new();

        List<PlayerData>? best = null;
        double bestScore = double.MinValue;

        foreach (string formation in SupportedFormations)
        {
            var lineup = FindBestLineupForFormation(players, formation);
            if (lineup.Count != 11)
                continue;

            var ratings = _ratingEngine.Calculate(
                lineup,
                formation,
                new TeamMatchContext
                {
                    SlotBehaviours = LastBehaviourProfile
                });

            double formationScore = OverallScore(ratings);
            if (formationScore > bestScore)
            {
                bestScore = formationScore;
                best = lineup;
                LastFormationName = formation;
            }
        }

        return best ?? players.Where(p => !p.Injured && !p.Suspended).Take(11).ToList();
    }

    public List<PlayerData> FindBestLineupForFormation(List<PlayerData> players, string formation)
    {
        if (players == null || players.Count < 11 || !SupportedFormations.Contains(formation))
            return new();

        var available = players
            .Where(p => !p.Injured && !p.Suspended)
            .ToList();

        if (available.Count < 11)
            return new();

        var roles = LineupRatingEngine.GetRoles(formation);
        var orders = BuildSlotOrders(roles);

        List<PlayerData>? bestLineup = null;
        Dictionary<int, PlayerBehaviour>? bestBehaviours = null;
        double bestScore = double.MinValue;

        foreach (var order in orders)
        {
            var remaining = available.ToList();
            var lineup = new PlayerData[11];
            var behaviours = new Dictionary<int, PlayerBehaviour>();
            bool failed = false;

            foreach (int slot in order)
            {
                PlayerRole role = roles[slot];
                PlayerData? bestPlayer = null;
                PlayerBehaviour bestBehaviour = PlayerBehaviour.Normal;
                double bestPositionScore = double.MinValue;

                foreach (var player in remaining)
                {
                    foreach (var behaviour in BehavioursFor(role))
                    {
                        double rawRating = _ratingEngine.GetPlayerPositionRating(
                            player,
                            role,
                            behaviour);

                        double positionFit = PositionFit(player, role);
                        double positionScore = rawRating * positionFit;

                        if (positionScore > bestPositionScore)
                        {
                            bestPositionScore = positionScore;
                            bestPlayer = player;
                            bestBehaviour = behaviour;
                        }
                    }
                }

                if (bestPlayer == null)
                {
                    failed = true;
                    break;
                }

                lineup[slot] = bestPlayer;
                behaviours[slot] = bestBehaviour;
                remaining.Remove(bestPlayer);
            }

            if (failed || lineup.Any(p => p == null))
                continue;

            var result = lineup.ToList();
            var ratings = _ratingEngine.Calculate(
                result,
                formation,
                new TeamMatchContext { SlotBehaviours = behaviours });

            double lineupScore = OverallScore(ratings);
            if (lineupScore > bestScore)
            {
                bestScore = lineupScore;
                bestLineup = result;
                bestBehaviours = behaviours;
            }
        }

        if (bestLineup == null || bestBehaviours == null)
            return new();

        LastFormationName = formation;
        LastBehaviourProfile = new Dictionary<int, PlayerBehaviour>(bestBehaviours);
        return bestLineup;
    }

    private static double PositionFit(PlayerData player, PlayerRole role)
    {
        if (role == PlayerRole.Goalkeeper)
            return player.Keeper > 0 ? 1.0 : 0.10;

        double defence =
            player.Defending * 1.00 +
            player.Playmaking * 0.20 +
            player.Passing * 0.10 +
            player.Winger * 0.05;

        double midfield =
            player.Playmaking * 1.00 +
            player.Passing * 0.20 +
            player.Defending * 0.15 +
            player.Winger * 0.10 +
            player.Stamina * 0.05;

        double wing =
            player.Winger * 1.00 +
            player.Playmaking * 0.35 +
            player.Passing * 0.15 +
            player.Defending * 0.10;

        double attack =
            player.Scoring * 1.00 +
            player.Passing * 0.25 +
            player.Winger * 0.15 +
            player.Playmaking * 0.10;

        double target = role switch
        {
            PlayerRole.LeftDefender or PlayerRole.CentralDefender or PlayerRole.RightDefender => defence,
            PlayerRole.LeftMidfielder or PlayerRole.CentralMidfielder or PlayerRole.RightMidfielder => midfield,
            PlayerRole.LeftWinger or PlayerRole.RightWinger => wing,
            PlayerRole.LeftForward or PlayerRole.CentralForward or PlayerRole.RightForward => attack,
            _ => midfield
        };

        double bestAlternative = role switch
        {
            PlayerRole.LeftDefender or PlayerRole.CentralDefender or PlayerRole.RightDefender => Math.Max(midfield, Math.Max(wing, attack)),
            PlayerRole.LeftMidfielder or PlayerRole.CentralMidfielder or PlayerRole.RightMidfielder => Math.Max(defence, Math.Max(wing, attack)),
            PlayerRole.LeftWinger or PlayerRole.RightWinger => Math.Max(midfield, Math.Max(defence, attack)),
            PlayerRole.LeftForward or PlayerRole.CentralForward or PlayerRole.RightForward => Math.Max(midfield, Math.Max(defence, wing)),
            _ => 0
        };

        if (target <= 0)
            return 0.25;

        double ratio = bestAlternative <= 0 ? 1.0 : target / bestAlternative;
        return Math.Clamp(0.40 + ratio * 0.60, 0.40, 1.00);
    }

    private static IEnumerable<int[]> BuildSlotOrders(PlayerRole[] roles)
    {
        var all = Enumerable.Range(0, roles.Length).ToArray();
        var midfield = all.Where(i => roles[i] is PlayerRole.LeftMidfielder or PlayerRole.CentralMidfielder or PlayerRole.RightMidfielder or PlayerRole.LeftWinger or PlayerRole.RightWinger).ToArray();
        var defence = all.Where(i => roles[i] is PlayerRole.Goalkeeper or PlayerRole.LeftDefender or PlayerRole.CentralDefender or PlayerRole.RightDefender).ToArray();
        var attack = all.Where(i => roles[i] is PlayerRole.LeftForward or PlayerRole.CentralForward or PlayerRole.RightForward).ToArray();

        yield return midfield.Concat(defence).Concat(attack).ToArray();
        yield return attack.Concat(midfield).Concat(defence).ToArray();
        yield return defence.Concat(midfield).Concat(attack).ToArray();
        yield return all;
    }

    private static IReadOnlyList<PlayerBehaviour> BehavioursFor(PlayerRole role) => role switch
    {
        PlayerRole.CentralDefender => new[] { PlayerBehaviour.Normal, PlayerBehaviour.Defensive, PlayerBehaviour.Offensive, PlayerBehaviour.TowardsWing },
        PlayerRole.LeftDefender or PlayerRole.RightDefender => new[] { PlayerBehaviour.Normal, PlayerBehaviour.Defensive, PlayerBehaviour.Offensive, PlayerBehaviour.TowardsMiddle },
        PlayerRole.LeftMidfielder or PlayerRole.CentralMidfielder or PlayerRole.RightMidfielder => new[] { PlayerBehaviour.Normal, PlayerBehaviour.Defensive, PlayerBehaviour.Offensive, PlayerBehaviour.TowardsWing },
        PlayerRole.LeftWinger or PlayerRole.RightWinger => new[] { PlayerBehaviour.Normal, PlayerBehaviour.Defensive, PlayerBehaviour.Offensive, PlayerBehaviour.TowardsMiddle },
        PlayerRole.LeftForward or PlayerRole.RightForward or PlayerRole.CentralForward => new[] { PlayerBehaviour.Normal, PlayerBehaviour.Defensive, PlayerBehaviour.TowardsWing },
        _ => new[] { PlayerBehaviour.Normal }
    };

    private static double OverallScore(TeamRatings r) =>
        r.Midfield * 1.30 +
        (r.LeftDefence + r.CentralDefence + r.RightDefence) / 3.0 * 1.05 +
        (r.LeftAttack + r.CentralAttack + r.RightAttack) / 3.0 * 1.10;

    public string GetLineupSummary(List<PlayerData> lineup)
        => GetLineupSummary(lineup, LastFormationName, LastBehaviourProfile);

    public string GetLineupSummary(
        List<PlayerData> lineup,
        string formation,
        IReadOnlyDictionary<int, PlayerBehaviour>? behaviours)
    {
        if (lineup == null || lineup.Count != 11)
            return "Kadroyu oluşturamadım.";

        var roles = LineupRatingEngine.GetRoles(formation);
        string text = $"EN İYİ 11 — {formation}\n\n";

        AppendRole(ref text, "🧤 KALECİ", lineup, roles, behaviours, PlayerRole.Goalkeeper);
        AppendRole(ref text, "🛡️ DEFANS", lineup, roles, behaviours, PlayerRole.LeftDefender, PlayerRole.CentralDefender, PlayerRole.RightDefender);
        AppendRole(ref text, "⚙️ ORTA SAHA", lineup, roles, behaviours,
            PlayerRole.LeftMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightMidfielder,
            PlayerRole.LeftWinger, PlayerRole.RightWinger);
        AppendRole(ref text, "⚽ FORVET", lineup, roles, behaviours,
            PlayerRole.LeftForward, PlayerRole.CentralForward, PlayerRole.RightForward);

        return text;
    }

    private static void AppendRole(ref string text, string title, List<PlayerData> lineup, PlayerRole[] roles, IReadOnlyDictionary<int, PlayerBehaviour>? behaviours, params PlayerRole[] accepted)
    {
        var indexes = Enumerable.Range(0, roles.Length)
            .Where(i => accepted.Contains(roles[i]))
            .ToList();

        if (indexes.Count == 0)
            return;

        text += $"{title}\n";
        foreach (int i in indexes)
        {
            var player = lineup[i];
            string behaviour = behaviours != null && behaviours.TryGetValue(i, out var b)
                ? BehaviourText(b)
                : "Normal";
            text += $"• {player.Name} — {RoleText(roles[i])}, {behaviour}\n";
        }
        text += "\n";
    }

    private static string BehaviourText(PlayerBehaviour behaviour) => behaviour switch
    {
        PlayerBehaviour.Offensive => "Ofansif",
        PlayerBehaviour.Defensive => "Defansif",
        PlayerBehaviour.TowardsMiddle => "Merkeze",
        PlayerBehaviour.TowardsWing => "Kanada",
        _ => "Normal"
    };

    private static string RoleText(PlayerRole role) => role switch
    {
        PlayerRole.Goalkeeper => "Kaleci",
        PlayerRole.LeftDefender => "Sol bek",
        PlayerRole.CentralDefender => "Stoper",
        PlayerRole.RightDefender => "Sağ bek",
        PlayerRole.LeftMidfielder => "Sol iç",
        PlayerRole.CentralMidfielder => "Merkez orta saha",
        PlayerRole.RightMidfielder => "Sağ iç",
        PlayerRole.LeftWinger => "Sol kanat",
        PlayerRole.RightWinger => "Sağ kanat",
        PlayerRole.LeftForward => "Sol forvet",
        PlayerRole.CentralForward => "Santrfor",
        PlayerRole.RightForward => "Sağ forvet",
        _ => "Oyuncu"
    };
}
