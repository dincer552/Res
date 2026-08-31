namespace HattrickAI.V5.Core;

/// <summary>
/// Connects Motor 3 to the existing regional rating engine.
/// It evaluates a player in one fixed slot under each legal individual order.
/// </summary>
public sealed class BehaviourRatingService
{
    private readonly BehaviourEngine _behaviourEngine = new();
    private readonly RegionalRatingEngineFixed _ratingEngine = new();

    public IReadOnlyList<BehaviourRatingCandidate> Evaluate(Player player, Slot baseSlot, Lineup lineup, IReadOnlyList<Player> players, RatingContext? context = null)
    {
        var result = new List<BehaviourRatingCandidate>();
        foreach (var order in _behaviourEngine.GetAllowedOrders(baseSlot.Code))
        {
            var testSlot = baseSlot with { Order = order };
            var testLineup = lineup with
            {
                Slots = lineup.Slots.Select(s => s.PlayerId == baseSlot.PlayerId ? testSlot : s).ToList()
            };
            var rating = _ratingEngine.CalculateLineup(testLineup, players, context);
            result.Add(new BehaviourRatingCandidate(player.Id, player.Name, baseSlot.Code, order, rating));
        }
        return result;
    }
}

public sealed record BehaviourRatingCandidate(
    int PlayerId,
    string PlayerName,
    string PositionCode,
    PlayerOrder Order,
    RegionalRatingSnapshot Rating);
