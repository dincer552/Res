using System.Text.RegularExpressions;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// Aşama A: WEB'in gönderdiği input sözleşmesini ve backend'in aynı sözleşmeyi
/// CHPP/Core tarafına taşıyan endpoint akışını statik olarak doğrular.
/// Bu test production hesabına bağlanmaz; canlı bağlantı G/G smoke testinde yapılır.
/// </summary>
public static class WebInputIntegrityRegression
{
    private static readonly string[] RequiredQuestionKeys =
    [
        "coachStyle",
        "teamSpirit",
        "matchImportance"
    ];

    private static readonly string[] RequiredSlotCodes =
    [
        "GK",
        "DEF-L", "DEF-CL", "DEF-C", "DEF-CR", "DEF-R",
        "W-L", "IM-L", "IM-C", "IM-R", "W-R",
        "FW-L", "FW-C", "FW-R"
    ];

    private static readonly string[] RequiredPlayerFields =
    [
        "PlayerID", "PlayerName", "KeeperSkill", "DefenderSkill",
        "PlaymakerSkill", "PassingSkill", "WingerSkill", "ScorerSkill",
        "StaminaSkill", "PlayerForm", "Experience", "Loyalty",
        "InjuryLevel", "Specialty", "SetPiecesSkill"
    ];

    public static int Run()
    {
        try
        {
            var root = FindRepositoryRoot();
            var indexPath = Path.Combine(root, "HattrickAI_V5", "wwwroot", "index.html");
            var programPath = Path.Combine(root, "HattrickAI_V5", "Program.cs");
            var analysisPath = Path.Combine(root, "HattrickAI_V5", "Core", "AnalysisService.cs");

            var failures = new List<string>();
            RequireFile(indexPath, failures);
            RequireFile(programPath, failures);
            RequireFile(analysisPath, failures);
            if (failures.Count > 0) return Report(failures);

            var html = File.ReadAllText(indexPath);
            var program = File.ReadAllText(programPath);
            var analysis = File.ReadAllText(analysisPath);

            Check(html.Contains("id=\"connect\"", StringComparison.Ordinal), "WEB CHPP connect control exists", failures);
            Check(html.Contains("id=\"analyze\"", StringComparison.Ordinal), "WEB analyze control exists", failures);
            Check(html.Contains("id=\"questionCard\"", StringComparison.Ordinal), "WEB questionnaire container exists", failures);
            Check(html.Contains("id=\"ownPitch\"", StringComparison.Ordinal), "WEB own lineup output exists", failures);
            Check(html.Contains("id=\"oppPitch\"", StringComparison.Ordinal), "WEB opponent lineup output exists", failures);

            foreach (var key in RequiredQuestionKeys)
                Check(Regex.IsMatch(html, $"key\\s*:\\s*['\"]{Regex.Escape(key)}['\"]"), $"question key preserved: {key}", failures);

            Check(html.Contains("JSON.stringify(answers)", StringComparison.Ordinal), "questionnaire POST serializes the same answer object", failures);
            Check(html.Contains("/api/v5/questionnaire", StringComparison.Ordinal), "WEB questionnaire endpoint wired", failures);
            Check(html.Contains("/api/v5/analysis", StringComparison.Ordinal), "WEB analysis endpoint wired", failures);
            Check(html.Contains("/api/v5/status", StringComparison.Ordinal), "WEB status endpoint wired", failures);
            Check(html.Contains("/api/v5/build", StringComparison.Ordinal), "WEB build endpoint wired", failures);

            foreach (var slot in RequiredSlotCodes)
                Check(html.Contains(slot, StringComparison.Ordinal), $"WEB slot code preserved: {slot}", failures);

            Check(program.Contains("MapPost(\"/api/v5/questionnaire\"", StringComparison.Ordinal), "backend questionnaire POST route exists", failures);
            Check(program.Contains("MapGet(\"/api/v5/questionnaire\"", StringComparison.Ordinal), "backend questionnaire GET route exists", failures);
            Check(program.Contains("MapGet(\"/api/v5/analysis\"", StringComparison.Ordinal), "backend analysis GET route exists", failures);
            Check(program.Contains("SetString(\"v5.coach\"", StringComparison.Ordinal), "coach style stored in session", failures);
            Check(program.Contains("SetString(\"v5.spirit\"", StringComparison.Ordinal), "team spirit stored in session", failures);
            Check(program.Contains("SetString(\"v5.attitude\"", StringComparison.Ordinal), "match importance stored in session", failures);
            Check(program.Contains("MatchQuestionnaire", StringComparison.Ordinal), "backend reconstructs MatchQuestionnaire", failures);

            Check(analysis.Contains("GetXmlAsync(\"teamdetails\"", StringComparison.Ordinal), "CHPP teamdetails input source exists", failures);
            Check(analysis.Contains("GetXmlAsync(\"training\"", StringComparison.Ordinal), "CHPP training input source exists", failures);
            Check(analysis.Contains("GetXmlAsync(\"players\"", StringComparison.Ordinal), "CHPP players input source exists", failures);
            Check(analysis.Contains("GetXmlAsync(\"matches\"", StringComparison.Ordinal), "CHPP matches input source exists", failures);
            Check(analysis.Contains("GetXmlAsync(\"matchlineup\"", StringComparison.Ordinal), "CHPP opponent lineup input source exists", failures);
            Check(analysis.Contains("GetXmlAsync(\"matchdetails\"", StringComparison.Ordinal), "CHPP opponent matchdetails input source exists", failures);
            Check(analysis.Contains("Request.Cookies[\"v5.matchId\"]", StringComparison.Ordinal), "selected match input is read from WEB cookie", failures);
            Check(analysis.Contains("questionnaire.MatchImportance", StringComparison.Ordinal), "questionnaire input reaches rating context", failures);

            foreach (var field in RequiredPlayerFields)
                Check(analysis.Contains($"\"{field}\"", StringComparison.Ordinal), $"CHPP player field mapped: {field}", failures);

            Check(analysis.Contains("ownPlayers.Count < 11", StringComparison.Ordinal), "minimum 11 own players enforced", failures);
            Check(analysis.Contains("lineupNodes.Count != 11", StringComparison.Ordinal), "opponent 11-player lineup enforced", failures);
            Check(analysis.Contains("opponentId <= 0", StringComparison.Ordinal), "opponent ID integrity enforced", failures);

            Console.WriteLine("=== V5 A) WEB INPUT INTEGRITY ===");
            Console.WriteLine($"WEB: questionnaire={RequiredQuestionKeys.Length} keys | slots={RequiredSlotCodes.Length}");
            Console.WriteLine("Backend: questionnaire/session + selected match + CHPP team/training/players/matches/lineup/details");
            Console.WriteLine($"Player mapping: {RequiredPlayerFields.Length} CHPP fields checked");

            if (failures.Count == 0)
            {
                Console.WriteLine("PASS: A) WEB input integrity");
                Console.WriteLine("Boundary note: this is a repository-level contract test; live OAuth/CHPP availability is intentionally deferred to G) production smoke test.");
                return 0;
            }

            return Report(failures);
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL: A) WEB input integrity exception: " + ex.Message);
            return 1;
        }
    }

    private static string FindRepositoryRoot()
    {
        var candidates = new List<string>
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var start in candidates)
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "HattrickAI_V5", "wwwroot", "index.html")) &&
                    File.Exists(Path.Combine(current.FullName, "HattrickAI_V5", "Program.cs")))
                    return current.FullName;
                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Repository root bulunamadı.");
    }

    private static void RequireFile(string path, List<string> failures)
    {
        if (!File.Exists(path)) failures.Add("missing file: " + path);
    }

    private static void Check(bool condition, string name, List<string> failures)
    {
        if (!condition) failures.Add(name);
    }

    private static int Report(List<string> failures)
    {
        foreach (var failure in failures) Console.WriteLine("FAIL: " + failure);
        Console.WriteLine($"FAIL: A) WEB input integrity ({failures.Count} assertion(s))");
        return 1;
    }
}
