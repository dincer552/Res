using System.Text.Json;
using System.Text.Json.Serialization;
using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// Aşama B: Core'un /api/v5/analysis response sözleşmesi ile WEB'in tükettiği
/// camelCase alanların aynı olduğunu offline doğrular. Canlı CHPP erişimi yapmaz.
/// </summary>
public static class CoreWebParityRegression
{
    public static int Run()
    {
        try
        {
            var root = FindRepositoryRoot();
            var indexPath = Path.Combine(root, "HattrickAI_V5", "wwwroot", "index.html");
            var programPath = Path.Combine(root, "HattrickAI_V5", "Program.cs");
            if (!File.Exists(indexPath) || !File.Exists(programPath))
                return Fail("WEB/Core dosyaları bulunamadı.");

            var html = File.ReadAllText(indexPath);
            var program = File.ReadAllText(programPath);
            var failures = new List<string>();

            Check(program.Contains("MapGet(\"/api/v5/analysis\"", StringComparison.Ordinal), "analysis endpoint exists", failures);
            Check(program.Contains("return Results.Ok(result)", StringComparison.Ordinal), "analysis endpoint returns Core Analysis object", failures);
            Check(program.Contains("PropertyNamingPolicy = JsonNamingPolicy.CamelCase", StringComparison.Ordinal), "API uses camelCase JSON naming", failures);

            // Analysis is the canonical boundary object. Serialize it exactly as ASP.NET does.
            var own = new Lineup("Own", "3-5-2", new[]
            {
                new Slot("GK", "Kaleci", "Kaleci", "Own GK", 1, 6, 50, 10),
                new Slot("DEF-L", "Sol bek", "Sol bek", "Own DL", 2, 5, 12, 34),
                new Slot("DEF-C", "Stoper", "Stoper", "Own DC", 3, 6, 50, 34),
                new Slot("DEF-R", "Sağ bek", "Sağ bek", "Own DR", 4, 5, 88, 34),
                new Slot("IM-L", "Sol iç", "Sol iç", "Own IM-L", 5, 6, 34, 50),
                new Slot("IM-C", "Merkez", "Merkez", "Own IM-C", 6, 7, 50, 50),
                new Slot("IM-R", "Sağ iç", "Sağ iç", "Own IM-R", 7, 6, 66, 50),
                new Slot("W-L", "Sol kanat", "Sol kanat", "Own W-L", 8, 5, 12, 50),
                new Slot("W-R", "Sağ kanat", "Sağ kanat", "Own W-R", 9, 5, 88, 50),
                new Slot("FW-L", "Sol forvet", "Sol forvet", "Own FW-L", 10, 6, 38, 72),
                new Slot("FW-R", "Sağ forvet", "Sağ forvet", "Own FW-R", 11, 6, 62, 72)
            });
            var opp = own with { TeamName = "Opponent" };
            var rating = new RegionalRatingSnapshot(6, 6, 6, 6, 6, 6, 6, 5, 5, 5, 5, 5, 5, 5);
            var questionnaire = MatchQuestionnaire.Default;
            var analysis = new Analysis("test", "Own", "Opponent", "test match", own, opp, rating, rating, questionnaire);

            var json = JsonSerializer.Serialize(analysis, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            });
            using var doc = JsonDocument.Parse(json);
            var body = doc.RootElement;

            var requiredRoot = new[]
            {
                "build", "teamName", "opponentName", "matchTitle", "own", "opponent",
                "ownRating", "opponentRating", "appliedQuestionnaire", "ownLineup", "opponentLineup",
                "ownFormation", "opponentFormation", "regionalRatings", "opponentThreat"
            };
            foreach (var field in requiredRoot)
                Check(body.TryGetProperty(field, out _), $"Core JSON field: {field}", failures);

            Check(body.GetProperty("own").GetProperty("slots").GetArrayLength() == 11, "own.slots contains 11 players", failures);
            Check(body.GetProperty("opponent").GetProperty("slots").GetArrayLength() == 11, "opponent.slots contains 11 players", failures);
            Check(body.GetProperty("own").GetProperty("formation").GetString() == "3-5-2", "own.formation parity", failures);
            Check(body.GetProperty("opponent").GetProperty("formation").GetString() == "3-5-2", "opponent.formation parity", failures);

            // The WEB must consume the same response boundary rather than maintaining a second schema.
            Check(html.Contains("/api/v5/analysis", StringComparison.Ordinal), "WEB calls the canonical analysis endpoint", failures);
            Check(html.Contains("ownFormation", StringComparison.Ordinal), "WEB consumes ownFormation", failures);
            Check(html.Contains("opponentFormation", StringComparison.Ordinal), "WEB consumes opponentFormation", failures);
            Check(html.Contains("ownRating", StringComparison.Ordinal), "WEB consumes ownRating", failures);
            Check(html.Contains("opponentRating", StringComparison.Ordinal), "WEB consumes opponentRating", failures);
            Check(html.Contains("own", StringComparison.Ordinal) && html.Contains("opponent", StringComparison.Ordinal), "WEB has own/opponent response bindings", failures);
            Check(html.Contains("slots", StringComparison.Ordinal), "WEB consumes lineup slots", failures);

            Console.WriteLine("=== V5 B) CORE ↔ WEB PARITY ===");
            Console.WriteLine("Boundary: /api/v5/analysis → Analysis → camelCase JSON → WEB");
            Console.WriteLine("Fixture: 11 own slots + 11 opponent slots + ratings + questionnaire");
            Console.WriteLine($"Core JSON: {body.EnumerateObject().Count()} root fields checked");

            if (failures.Count == 0)
            {
                Console.WriteLine("PASS: B) Core ↔ WEB parity");
                Console.WriteLine("Note: live CHPP/OAuth is intentionally outside B and remains in G production smoke.");
                return 0;
            }
            return Report(failures);
        }
        catch (Exception ex)
        {
            return Fail("B exception: " + ex.Message);
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "HattrickAI_V5", "wwwroot", "index.html"))) return current.FullName;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("Repository root bulunamadı.");
    }

    private static void Check(bool condition, string name, List<string> failures)
    {
        if (!condition) failures.Add(name);
    }

    private static int Report(List<string> failures)
    {
        foreach (var failure in failures) Console.WriteLine("FAIL: " + failure);
        Console.WriteLine($"FAIL: B) Core ↔ WEB parity ({failures.Count} assertion(s))");
        return 1;
    }

    private static int Fail(string message)
    {
        Console.WriteLine("FAIL: " + message);
        return 1;
    }
}
