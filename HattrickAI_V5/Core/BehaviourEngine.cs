namespace HattrickAI.V5.Core;

/// <summary>
/// M6 primitive: defines legal individual behaviour options for each fixed
/// Hattrick position. It does not inspect the opponent and does not choose
/// the winning behaviour.
/// </summary>
public sealed class BehaviourEngine
{
    public IReadOnlyList<PlayerOrder> GetAllowedOrders(string positionCode) => positionCode switch
    {
        "GK" => [PlayerOrder.Normal],

        // Wing backs: normal, offensive, defensive and towards middle.
        "DEF-L" or "DEF-R" =>
            [PlayerOrder.Normal, PlayerOrder.Offensive, PlayerOrder.Defensive, PlayerOrder.TowardsMiddle],

        // Central defenders: normal, offensive and towards wing.
        "DEF-CL" or "DEF-C" or "DEF-CR" =>
            [PlayerOrder.Normal, PlayerOrder.Offensive, PlayerOrder.TowardsWing],

        // Wingers: normal, offensive, defensive and towards middle.
        "W-L" or "W-R" =>
            [PlayerOrder.Normal, PlayerOrder.Offensive, PlayerOrder.Defensive, PlayerOrder.TowardsMiddle],

        // Inner midfielders: normal, offensive, defensive and towards wing.
        "IM-L" or "IM-C" or "IM-R" =>
            [PlayerOrder.Normal, PlayerOrder.Offensive, PlayerOrder.Defensive, PlayerOrder.TowardsWing],

        // Forwards: normal, defensive and towards wing.
        "FW-L" or "FW-R" or "FW-C" =>
            [PlayerOrder.Normal, PlayerOrder.Defensive, PlayerOrder.TowardsWing],

        _ => [PlayerOrder.Normal]
    };

    public IReadOnlyList<BehaviourCandidate> EnumerateCandidates(Player player, string positionCode)
    {
        ArgumentNullException.ThrowIfNull(player);

        return GetAllowedOrders(positionCode)
            .Distinct()
            .Select(order => new BehaviourCandidate(player.Id, positionCode, order))
            .ToList();
    }
}

public sealed record BehaviourCandidate(
    int PlayerId,
    string PositionCode,
    PlayerOrder Order);
