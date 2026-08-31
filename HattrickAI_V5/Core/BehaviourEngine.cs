namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 3: selected position için mümkün individual behaviour seçeneklerini
/// üretir. Bu katman henüz rakibi değerlendirmez; seçenekleri ve rating motoruna
/// verilecek davranışları tanımlar.
/// </summary>
public sealed class BehaviourEngine
{
    public IReadOnlyList<PlayerOrder> GetAllowedOrders(string positionCode) => positionCode switch
    {
        "GK" => [PlayerOrder.Normal],
        "DEF-L" or "DEF-R" => [PlayerOrder.Normal, PlayerOrder.Defensive, PlayerOrder.TowardsMiddle],
        "DEF-CL" or "DEF-C" or "DEF-CR" => [PlayerOrder.Normal, PlayerOrder.Offensive, PlayerOrder.TowardsWing],
        "W-L" or "W-R" => [PlayerOrder.Normal, PlayerOrder.Offensive, PlayerOrder.Defensive, PlayerOrder.TowardsMiddle],
        "IM-L" or "IM-C" or "IM-R" => [PlayerOrder.Normal, PlayerOrder.Offensive, PlayerOrder.Defensive, PlayerOrder.TowardsWing],
        "FW-L" or "FW-R" or "FW-C" => [PlayerOrder.Normal, PlayerOrder.Defensive, PlayerOrder.TowardsWing],
        _ => [PlayerOrder.Normal]
    };

    public IReadOnlyList<BehaviourCandidate> EnumerateCandidates(Player player, string positionCode)
        => GetAllowedOrders(positionCode)
            .Select(order => new BehaviourCandidate(player.Id, positionCode, order))
            .ToList();
}

public sealed record BehaviourCandidate(int PlayerId, string PositionCode, PlayerOrder Order);
