using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 2: selects an XI/position assignment for a fixed formation.
/// The optimizer uses Motor 1 suitability scores as the primary assignment
/// signal, while keeping the position occupancy valid. It deliberately does
/// not choose player behaviour yet; that belongs to Motor 3/4.
/// </summary>
public sealed class XIOptimizer
{
    private readonly PositionSuitabilityEngine _suitability;

    public XIOptimizer(PositionSuitabilityEngine suitability)
        => _suitability = suitability ?? throw new ArgumentNullException(nameof(suitability));

    public Lineup Optimize(string teamName, IReadOnlyList<Player> players, string formation = "3-5-2")
    {
        ArgumentNullException.ThrowIfNull(players);
        if (players.Count < 11) throw new InvalidOperationException("XI optimizasyonu için en az 11 oyuncu gerekli.");

        var slots = FormationSlots(formation);
        if (slots.Count != 11) throw new InvalidOperationException($"Desteklenmeyen diziliş: {formation}");

        // The problem is a weighted bipartite assignment: every player can be
        // used once and every slot must be filled once. For a normal squad
        // size we can solve it exactly with dynamic programming over players.
        // This is intentionally deterministic and avoids the old greedy
        // BuildOwnLineup() order dependency.
        var candidateCount = Math.Min(players.Count, 20);
        var orderedPlayers = players
            .OrderByDescending(p => BroadScore(p))
            .Take(candidateCount)
            .ToList();

        var memo = new Dictionary<(int SlotIndex, ulong UsedMask), AssignmentResult>();
        var best = Solve(0, 0UL, orderedPlayers, slots, memo);

        if (best.Assignments.Count != slots.Count)
            throw new InvalidOperationException("XI optimizasyonu geçerli bir 11 üretemedi.");

        var result = new List<Slot>(slots.Count);
        foreach (var assignment in best.Assignments)
        {
            var slot = slots[assignment.SlotIndex];
            var player = orderedPlayers[assignment.PlayerIndex];
            result.Add(new Slot(
                slot.Code,
                slot.Label,
                slot.Description,
                player.Name,
                player.Id,
                assignment.Suitability,
                slot.X,
                slot.Y,
                PlayerOrder.Normal));
        }

        return new Lineup(teamName, formation, result);
    }

    private AssignmentResult Solve(
        int slotIndex,
        ulong usedMask,
        IReadOnlyList<Player> players,
        IReadOnlyList<TemplateSlot> slots,
        Dictionary<(int SlotIndex, ulong UsedMask), AssignmentResult> memo)
    {
        if (slotIndex >= slots.Count)
            return new AssignmentResult(0, new List<Assignment>());

        if (memo.TryGetValue((slotIndex, usedMask), out var cached))
            return cached;

        var slot = slots[slotIndex];
        AssignmentResult? best = null;

        for (var playerIndex = 0; playerIndex < players.Count; playerIndex++)
        {
            var bit = 1UL << playerIndex;
            if ((usedMask & bit) != 0) continue;

            var score = _suitability.Score(players[playerIndex], slot.Code);
            if (double.IsNegativeInfinity(score)) continue;

            var child = Solve(slotIndex + 1, usedMask | bit, players, slots, memo);
            if (child.Assignments.Count != slots.Count - slotIndex - 1) continue;

            var assignments = new List<Assignment>(child.Assignments.Count + 1)
            {
                new(slotIndex, playerIndex, score)
            };
            assignments.AddRange(child.Assignments);

            var candidate = new AssignmentResult(score + child.TotalScore, assignments);
            if (best is null || candidate.TotalScore > best.TotalScore + 1e-9)
                best = candidate;
        }

        best ??= new AssignmentResult(double.NegativeInfinity, new List<Assignment>());
        memo[(slotIndex, usedMask)] = best;
        return best;
    }

    private static double BroadScore(Player p)
        => p.Keeper + p.Defending + p.Playmaking + p.Passing + p.Winger + p.Scoring + p.Form * .1 + p.Stamina * .05;

    private static IReadOnlyList<TemplateSlot> FormationSlots(string formation)
        => formation switch
        {
            "3-5-2" => new[]
            {
                new TemplateSlot("GK","GK","Kaleci",50,10),
                new TemplateSlot("DEF-CL","DEF-CL","Sol stoper",30,34),
                new TemplateSlot("DEF-C","DEF-C","Merkez stoper",50,34),
                new TemplateSlot("DEF-CR","DEF-CR","Sağ stoper",70,34),
                new TemplateSlot("W-L","W-L","Sol kanat",12,50),
                new TemplateSlot("IM-L","IM-L","Sol iç",34,50),
                new TemplateSlot("IM-C","IM-C","Merkez",50,50),
                new TemplateSlot("IM-R","IM-R","Sağ iç",66,50),
                new TemplateSlot("W-R","W-R","Sağ kanat",88,50),
                new TemplateSlot("FW-L","FW-L","Sol forvet",38,72),
                new TemplateSlot("FW-R","FW-R","Sağ forvet",62,72)
            },
            _ => throw new ArgumentException($"Formation '{formation}' desteklenmiyor.", nameof(formation))
        };

    private readonly record struct TemplateSlot(string Code,string Label,string Description,double X,double Y);
    private readonly record struct Assignment(int SlotIndex,int PlayerIndex,double Suitability);
    private sealed record AssignmentResult(double TotalScore,List<Assignment> Assignments);
}
