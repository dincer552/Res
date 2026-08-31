namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 4: chooses an individual order for a fixed player/position using
/// the opponent threat map. It evaluates each legal order through the real
/// regional-rating engine, then applies a conservative matchup score.
/// It does not alter the XI; Motor 2 owns player placement.
/// </summary>
public sealed class BehaviourOptimizer
{
    private readonly BehaviourRatingService _ratingService;
    private readonly OpponentThreatEngine _threatEngine;

    public BehaviourOptimizer()
        : this(new BehaviourRatingService(), new OpponentThreatEngine())
    {
    }

    public BehaviourOptimizer(BehaviourRatingService ratingService, OpponentThreatEngine threatEngine)
    {
        _ratingService = ratingService ?? throw new ArgumentNullException(nameof(ratingService));
        _threatEngine = threatEngine ?? throw new ArgumentNullException(nameof(threatEngine));
    }

    public BehaviourDecision Choose(Player player, Slot slot, Lineup lineup,
        IReadOnlyList<Player> players, RegionalRatingSnapshot opponentRating,
        RatingContext? context = null)
    {
        var candidates = _ratingService.Evaluate(player, slot, lineup, players, context);
        var threat = _threatEngine.Analyze(opponentRating);
        var scored = candidates
            .Select(c => new ScoredCandidate(c, Score(c.Rating, slot.Code, threat)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Candidate.Order == PlayerOrder.Normal ? 0 : 1)
            .ToList();

        var best = scored.FirstOrDefault();
        if (best is null)
            return new BehaviourDecision(player.Id, player.Name, slot.Code, PlayerOrder.Normal, 0, "No legal behaviour candidates");

        return new BehaviourDecision(player.Id, player.Name, slot.Code,
            best.Candidate.Order, best.Score, Reason(best.Candidate.Order, slot.Code, threat));
    }

    private static double Score(RegionalRatingSnapshot rating, string slotCode, OpponentThreatMap threat)
    {
        // Base value rewards maintaining the player's local rating strength.
        var baseValue = rating.TotalDefence + rating.Midfield * .35 + rating.TotalAttack * .65;

        // Then add opponent-aware pressure for the side this slot protects or attacks.
        var sideDeficit = slotCode switch
        {
            "DEF-L" or "DEF-CL" => Math.Max(0, threat.LeftThreat - rating.LeftDefence),
            "DEF-R" or "DEF-CR" => Math.Max(0, threat.RightThreat - rating.RightDefence),
            "DEF-C" => Math.Max(0, threat.CenterThreat - rating.CentralDefence),
            _ => 0
        };

        var attackMargin = slotCode switch
        {
            "W-L" or "FW-L" => rating.LeftAttack - threat.LeftDefenceBarrier,
            "W-R" or "FW-R" => rating.RightAttack - threat.RightDefenceBarrier,
            "FW-C" => rating.CentralAttack - threat.CenterDefenceBarrier,
            _ => rating.CentralAttack - threat.CenterDefenceBarrier
        };

        return baseValue - sideDeficit * .70 + attackMargin * .25;
    }

    private static string Reason(PlayerOrder order, string slotCode, OpponentThreatMap threat)
    {
        if (order == PlayerOrder.Defensive)
            return $"{slotCode}: rakip tehdidi {threat.MaxAttackThreat:0.##}; savunma katkısı önceliklendirildi.";
        if (order == PlayerOrder.Offensive)
            return $"{slotCode}: hücum marjı pozisyon için yeterli görüldü.";
        if (order == PlayerOrder.TowardsWing)
            return $"{slotCode}: kanat tehdidi/kanat katkısı dengesi nedeniyle seçildi.";
        if (order == PlayerOrder.TowardsMiddle)
            return $"{slotCode}: merkez katkısı önceliklendirildi.";
        return $"{slotCode}: nötr davranış varsayılan olarak en dengeli bulundu.";
    }

    private sealed record ScoredCandidate(BehaviourRatingCandidate Candidate, double Score);
}

public sealed record BehaviourDecision(
    int PlayerId,
    string PlayerName,
    string PositionCode,
    PlayerOrder Order,
    double Score,
    string Reason);
