using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.HOEngine;

/// <summary>
/// Single source of truth for match ratings.
/// Both sides of a simulated match must pass through LineupRatingEngine.
/// Historical sector ratings are never substituted for a calculated rating.
/// </summary>
public sealed class SymmetricMatchRatingEngine
{
    private readonly LineupRatingEngine _engine = new();

    public TeamRatings Calculate(
        IReadOnlyList<PlayerData> lineup,
        string formation,
        TeamMatchContext? context = null)
    {
        return _engine.Calculate(lineup.ToList(), formation, context ?? new TeamMatchContext());
    }

    /// <summary>
    /// Calculates a historical opponent lineup with the exact same engine used
    /// for our current lineup. The historical slot data supplies only position
    /// and behaviour; player skill/form/experience data comes from CHPP players.
    /// </summary>
    public bool TryCalculateHistorical(
        IReadOnlyList<PlayerData> squad,
        IReadOnlyList<HistoricalLineupSlot> historicalSlots,
        string formation,
        TeamMatchContext context,
        out TeamRatings ratings,
        out List<PlayerData> orderedLineup,
        out Dictionary<int, PlayerBehaviour> behaviours,
        out string error)
    {
        ratings = new TeamRatings();
        orderedLineup = new List<PlayerData>();
        behaviours = new Dictionary<int, PlayerBehaviour>();
        error = string.Empty;

        var roles = LineupRatingEngine.GetRoles(formation);
        if (roles.Length != 11 || historicalSlots.Count != 11)
        {
            error = "Tarihsel kadro 11 oyuncu içermiyor.";
            return false;
        }

        var byId = squad
            .Where(p => p.PlayerId > 0)
            .GroupBy(p => p.PlayerId)
            .ToDictionary(g => g.Key, g => g.First());

        var unused = historicalSlots.ToList();

        for (int slotIndex = 0; slotIndex < roles.Length; slotIndex++)
        {
            var expectedRole = roles[slotIndex];
            var match = FindBestSlotMatch(unused, expectedRole);
            if (match == null)
            {
                error = $"Tarihsel kadrodaki {expectedRole} pozisyonu güncel CHPP oyuncu verisiyle eşleşmedi.";
                return false;
            }

            if (!byId.TryGetValue(match.PlayerId, out var sourcePlayer))
            {
                error = $"Tarihsel oyuncu ID {match.PlayerId} artık CHPP kadrosunda bulunmuyor.";
                return false;
            }

            // The historical matchlineup proves that the player was available then.
            // Current injury/suspension flags must therefore not zero a historical rating.
            var historicalPlayer = CloneForHistoricalMatch(sourcePlayer);
            orderedLineup.Add(historicalPlayer);
            behaviours[slotIndex] = match.Behaviour;
            unused.Remove(match);
        }

        ratings = _engine.Calculate(orderedLineup, formation, new TeamMatchContext
        {
            TacticType = context.TacticType,
            TacticLevel = context.TacticLevel,
            Attitude = context.Attitude,
            IsHome = context.IsHome,
            CoachModifier = context.CoachModifier,
            TeamSpirit = context.TeamSpirit,
            Confidence = context.Confidence,
            Weather = context.Weather,
            Minute = context.Minute,
            SlotBehaviours = behaviours
        });

        return true;
    }

    private static HistoricalLineupSlot? FindBestSlotMatch(
        List<HistoricalLineupSlot> slots,
        PlayerRole expectedRole)
    {
        var exact = slots.FirstOrDefault(x => x.Role == expectedRole);
        return exact;
    }

    private static PlayerData CloneForHistoricalMatch(PlayerData source)
    {
        return new PlayerData
        {
            PlayerId = source.PlayerId,
            Name = source.Name,
            Age = source.Age,
            Form = source.Form,
            Stamina = source.Stamina,
            Experience = source.Experience,
            Leadership = source.Leadership,
            Loyalty = source.Loyalty,
            Specialty = source.Specialty,
            Keeper = source.Keeper,
            Defending = source.Defending,
            Playmaking = source.Playmaking,
            Winger = source.Winger,
            Passing = source.Passing,
            Scoring = source.Scoring,
            SetPieces = source.SetPieces,
            Injured = false,
            Suspended = false
        };
    }
}

public sealed record HistoricalLineupSlot(
    int PlayerId,
    PlayerRole Role,
    PlayerBehaviour Behaviour);
