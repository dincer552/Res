using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 2: selects an XI/position assignment for a fixed formation.
/// Uses Motor 1 suitability as the assignment weight and solves the full
/// rectangular assignment problem instead of truncating the squad to 20.
/// Behaviour remains Normal here; Motor 3/4 handles individual orders later.
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
        var rows = slots.Count;
        var cols = players.Count;
        var cost = new double[rows, cols];

        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                var score = _suitability.Score(players[j], slots[i].Code);
                cost[i, j] = double.IsNegativeInfinity(score) ? 1e9 : -score;
            }
        }

        var assignment = RectangularHungarian(cost);
        var result = new List<Slot>(rows);

        for (var i = 0; i < rows; i++)
        {
            var playerIndex = assignment[i];
            if (playerIndex < 0)
                throw new InvalidOperationException("XI optimizasyonu geçerli bir atama üretemedi.");

            var slot = slots[i];
            var player = players[playerIndex];
            var suitability = _suitability.Score(player, slot.Code);
            if (double.IsNegativeInfinity(suitability))
                throw new InvalidOperationException($"Oyuncu {player.Name} için {slot.Code} geçersiz atama.");

            result.Add(new Slot(slot.Code, slot.Label, slot.Description,
                player.Name, player.Id, suitability, slot.X, slot.Y, PlayerOrder.Normal));
        }

        return new Lineup(teamName, formation, result);
    }

    private static int[] RectangularHungarian(double[,] cost)
    {
        var rows = cost.GetLength(0);
        var cols = cost.GetLength(1);
        if (rows > cols) throw new ArgumentException("Satır sayısı sütun sayısından büyük olamaz.");

        var u = new double[rows + 1];
        var v = new double[cols + 1];
        var p = new int[cols + 1];
        var way = new int[cols + 1];

        for (var i = 1; i <= rows; i++)
        {
            p[0] = i;
            var j0 = 0;
            var minv = Enumerable.Repeat(double.PositiveInfinity, cols + 1).ToArray();
            var used = new bool[cols + 1];

            do
            {
                used[j0] = true;
                var i0 = p[j0];
                var delta = double.PositiveInfinity;
                var j1 = 0;

                for (var j = 1; j <= cols; j++)
                {
                    if (used[j]) continue;
                    var cur = cost[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cur < minv[j])
                    {
                        minv[j] = cur;
                        way[j] = j0;
                    }
                    if (minv[j] < delta)
                    {
                        delta = minv[j];
                        j1 = j;
                    }
                }

                if (double.IsPositiveInfinity(delta))
                    throw new InvalidOperationException("XI atamasında geçerli yol bulunamadı.");

                for (var j = 0; j <= cols; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }

                j0 = j1;
            }
            while (p[j0] != 0);

            do
            {
                var j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            }
            while (j0 != 0);
        }

        var assignment = Enumerable.Repeat(-1, rows).ToArray();
        for (var j = 1; j <= cols; j++)
        {
            if (p[j] > 0)
                assignment[p[j] - 1] = j - 1;
        }

        return assignment;
    }

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
}
