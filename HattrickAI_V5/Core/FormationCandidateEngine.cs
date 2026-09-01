namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 4 - Aday Diziliş Motoru.
/// Rakipten gelen tehditleri ve oyuncu uygunluklarını henüz puanlayarak
/// kadro seçmez; önce yasal ve değerlendirilebilir diziliş uzayını üretir.
/// Sonraki Motor 5 bu adayları oyuncu/pozisyon kombinasyonlarıyla doldurur.
/// </summary>
public sealed class FormationCandidateEngine : IFormationCandidateEngine
{
    private static readonly IReadOnlyList<FormationCandidate> LegalCandidates =
    [
        new("3-5-2", ["GK", "DEF-CL", "DEF-C", "DEF-CR", "W-L", "IM-L", "IM-C", "IM-R", "W-R", "FW-L", "FW-R"]),
        new("3-4-3", ["GK", "DEF-CL", "DEF-C", "DEF-CR", "W-L", "IM-L", "IM-R", "W-R", "FW-L", "FW-C", "FW-R"]),
        new("3-4-3", ["GK", "DEF-CL", "DEF-C", "DEF-CR", "IM-L", "IM-C", "IM-R", "W-R", "FW-L", "FW-C", "FW-R"]),
        new("4-4-2", ["GK", "DEF-L", "DEF-CL", "DEF-CR", "DEF-R", "W-L", "IM-L", "IM-R", "W-R", "FW-L", "FW-R"]),
        new("4-5-1", ["GK", "DEF-L", "DEF-CL", "DEF-CR", "DEF-R", "W-L", "IM-L", "IM-C", "IM-R", "W-R", "FW-C"]),
        new("2-5-3", ["GK", "DEF-CL", "DEF-CR", "W-L", "IM-L", "IM-C", "IM-R", "W-R", "FW-L", "FW-C", "FW-R"]),
        new("5-3-2", ["GK", "DEF-L", "DEF-CL", "DEF-C", "DEF-CR", "DEF-R", "IM-L", "IM-C", "IM-R", "FW-L", "FW-R"])
    ];

    public FormationCandidateSet Generate(
        MatchDataContext context,
        PlayerAnalysisResult players)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(players);

        // Motor 4'ün ilk sürümünde aday uzayı sabittir.
        // Rakip tehditleri burada karar verici yapılmaz; bu bilgi Motor 5-9
        // arasında adayların değerlendirilmesi için taşınır.
        var available = new List<FormationCandidate>();
        foreach (var candidate in LegalCandidates)
        {
            if (HasEnoughDistinctPlayers(players, candidate.SlotCodes))
                available.Add(candidate);
        }

        return new FormationCandidateSet(available);
    }

    private static bool HasEnoughDistinctPlayers(
        PlayerAnalysisResult players,
        IReadOnlyList<string> slots)
    {
        if (players.Players.Count < 11) return false;

        // Şimdilik yalnızca aday pozisyonlarının oyuncu havuzunda
        // karşılanabilir olup olmadığını kontrol ediyoruz. Gerçek birebir
        // oyuncu→slot ataması Motor 5'in sorumluluğudur.
        var possible = players.Players.Count(p =>
            p.PositionScores.Any(x => slots.Contains(x.Key, StringComparer.Ordinal)));

        return possible >= 11;
    }
}
