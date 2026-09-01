namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 3: Oyuncu Analiz Motoru.
/// CHPP'den gelen oyuncu durumunu takım seçiminin geri kalanından bağımsız
/// olarak değerlendirir. Rakibi değerlendirmez, takım ratingi üretmez ve
/// bireysel davranış seçmez. Her oyuncu için yasal/uygun pozisyon adaylarını
/// puanlar ve sonraki motorların kullanacağı temiz bir oyuncu profili üretir.
/// </summary>
public sealed class PlayerAnalysisEngine
{
    private static readonly string[] PositionCodes =
    [
        "GK",
        "DEF-L", "DEF-CL", "DEF-C", "DEF-CR", "DEF-R",
        "W-L", "IM-L", "IM-C", "IM-R", "W-R",
        "FW-L", "FW-C", "FW-R"
    ];

    public PlayerAnalysisProfile Analyze(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var candidates = PositionCodes
            .Select(code => new PlayerPositionCandidate(code, Score(player, code)))
            .Where(x => !double.IsNegativeInfinity(x.Score))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => PositionOrder(x.PositionCode))
            .ToList();

        return new PlayerAnalysisProfile(
            player.Id,
            player.Name,
            candidates,
            candidates.FirstOrDefault()?.PositionCode,
            candidates.Skip(1).FirstOrDefault()?.PositionCode);
    }

    public IReadOnlyList<PlayerAnalysisProfile> AnalyzeAll(IEnumerable<Player> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        return players.Select(Analyze).ToList();
    }

    public double Score(Player player, string positionCode)
    {
        ArgumentNullException.ThrowIfNull(player);

        return positionCode switch
        {
            "GK" => player.Keeper + player.Form * .15,

            "DEF-L" or "DEF-R" =>
                player.Defending + player.Passing * .10 + player.Winger * .05,

            "DEF-C" or "DEF-CL" or "DEF-CR" =>
                player.Defending * 1.05 + player.Passing * .15 + player.Playmaking * .04,

            "W-L" or "W-R" =>
                player.Winger + player.Passing * .22 + player.Playmaking * .08,

            "IM-L" or "IM-R" =>
                player.Playmaking + player.Passing * .25 + player.Stamina * .12,

            "IM-C" =>
                player.Playmaking * 1.05 + player.Passing * .25 + player.Stamina * .12 + player.Experience * .04,

            "FW-L" or "FW-R" =>
                player.Scoring + player.Passing * .18 + player.Winger * .08 + player.Experience * .02,

            "FW-C" =>
                player.Scoring * 1.05 + player.Passing * .20 + player.Playmaking * .04,

            _ => double.NegativeInfinity
        };
    }

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

public sealed record PlayerPositionCandidate(
    string PositionCode,
    double Score);

public sealed record PlayerAnalysisProfile(
    int PlayerId,
    string PlayerName,
    IReadOnlyList<PlayerPositionCandidate> Positions,
    string? PrimaryPosition,
    string? SecondaryPosition)
{
    public double PrimaryScore => Positions.Count == 0 ? double.NegativeInfinity : Positions[0].Score;
    public double SecondaryScore => Positions.Count < 2 ? double.NegativeInfinity : Positions[1].Score;
}
