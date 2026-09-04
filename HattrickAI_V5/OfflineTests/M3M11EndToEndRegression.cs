using System.Text.Json;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// C acceptance: M3 → M11 end-to-end pipeline continuity contract.
/// First checkpoint: M3 input/output continuity.
/// </summary>
public static class M3M11EndToEndRegression
{
    public static int Run(string path)
    {
        if (!File.Exists(path)) return Fail($"fixture bulunamadı: {path}");

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var normalized = doc.RootElement.GetProperty("normalized");
            var analysis = doc.RootElement.GetProperty("v5Analysis");
            var players = normalized.GetProperty("ownPlayers").EnumerateArray().ToList();
            var m3Players = players.Select(ReadId).ToList();
            var uniqueInputIds = m3Players.Distinct().Count();

            Console.WriteLine("=== C M3-M11 END-TO-END REGRESSION ===");
            Console.WriteLine($"Input players={m3Players.Count} | unique IDs={uniqueInputIds}");

            Check(m3Players.Count >= 11, "M3 input player pool >= 11");
            Check(uniqueInputIds == m3Players.Count, "M3 input player IDs unique");

            // This checkpoint is intentionally structural: it verifies the fixture contract
            // that feeds the real MotorPipelineService and the expected M3 result shape.
            // The full live pipeline execution remains the subsequent C checkpoints.
            Check(analysis.TryGetProperty("ownLineup", out var ownLineup), "fixture contains downstream ownLineup");
            if (ownLineup.ValueKind != JsonValueKind.Undefined)
            {
                Check(ownLineup.TryGetProperty("slots", out var slots), "downstream lineup exposes slots");
                if (slots.ValueKind != JsonValueKind.Undefined)
                    Check(slots.GetArrayLength() == 11, "downstream own XI has 11 slots");
            }

            Console.WriteLine("PASS: C1 M3 input/output continuity contract");
            Console.WriteLine("NEXT: C2 M4 legal formations");
            return 0;
        }
        catch (Exception ex)
        {
            return Fail("C1 exception: " + ex.Message);
        }
    }

    private static int ReadId(JsonElement e) => e.GetProperty("id").GetInt32();

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    private static int Fail(string message)
    {
        Console.WriteLine("FAIL: " + message);
        return 1;
    }
}
