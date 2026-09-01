namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 3: Oyuncu Analiz Motoru.
/// Sadece oyuncu uygunluk profilini üretir. XI seçmez, diziliş seçmez,
/// rakip skoru kullanmaz ve takım ratingi üretmez.
/// </summary>
public sealed class PlayerAnalysisEngine : IPlayerAnalysisEngine
{
    private static readonly string[] PositionCodes =
    [
        "GK",
        "DEF-L", "DEF-CL", "DEF-C", "DEF-CR", "DEF-R",
        "W-L", "IM-L", "IM-C", "IM-R", "W-R",
        "FW-L", "FW-C", "FW-R"
    ];

    public PlayerAnalysisResult Analyze(IReadOnlyList<Player> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        return new PlayerAnalysisResult(players.Select(AnalyzePlayer).ToList());
    }

    public PlayerAnalysisProfile AnalyzePlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var eligible = IsEligible(player);
        var candidates = PositionCodes
            .Select(code => new PlayerPositionCandidate(code, eligible ? Score(player, code) : double.NegativeInfinity))
            .Where(x => !double.IsNegativeInfinity(x.Score))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => PositionOrder(x.PositionCode))
            .ToList();

        return new PlayerAnalysisProfile(
            player.Id,
            player.Name,
            eligible,
            player.InjuryLevel,
            candidates,
            candidates.FirstOrDefault()?.PositionCode,
            candidates.Skip(1).FirstOrDefault()?.PositionCode);
    }

    public double Score(Player player, string positionCode)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!IsEligible(player)) return double.NegativeInfinity;

        return positionCode switch
        {
            "GK" => player.Keeper + player.Form * .15,
            "DEF-L" or "DEF-R" => player.Defending + player.Passing * .10 + player.Winger * .05,
            "DEF-C" or "DEF-CL" or "DEF-CR" => player.Defending * 1.05 + player.Passing * .15 + player.Playmaking * .04,
            "W-L" or "W-R" => player.Winger + player.Passing * .22 + player.Playmaking * .08,
            "IM-L" or "IM-R" => player.Playmaking + player.Passing * .25 + player.Stamina * .12,
            "IM-C" => player.Playmaking * 1.05 + player.Passing * .25 + player.Stamina * .12 + player.Experience * .04,
            "FW-L" or "FW-R" => player.Scoring + player.Passing * .18 + player.Winger * .08 + player.Experience * .02,
            "FW-C" => player.Scoring * 1.05 + player.Passing * .20 + player.Playmaking * .04,
            _ => double.NegativeInfinity
        };
    }

    private static bool IsEligible(Player player)
        => player.Id > 0 && player.InjuryLevel != 999;

    private static int PositionOrder(string code) => code switch
    {
        "GK" => 0,
        "DEF-L" => 10,
        "DEF-CL" => 11,
        "DEF-C" => 12,
        "DEF-CR" => 13,
        "DEF-R" => 14,
        "W-L" => 20,
        "IM-L" => 21,
        "IM-C" => 22,
        "IM-R" => 23,
        "W-R" => 24,
        "FW-L" => 30,
        "FW-C" => 31,
        "FW-R" => 32,
        _ => 99
    };
}

public sealed record PlayerPositionCandidate(string PositionCode, double Score);

public sealed record PlayerAnalysisProfile(
    int PlayerId,
    string PlayerName,
    bool IsEligible,
    int InjuryLevel,
    IReadOnlyList<PlayerPositionCandidate> Positions,
    string? PrimaryPosition,
    string? SecondaryPosition)
{
    public double PrimaryScore => Positions.Count == 0 ? double.NegativeInfinity : Positions[0].Score;
    public double SecondaryScore => Positions.Count < 2 ? double.NegativeInfinity : Positions[1].Score;
}
