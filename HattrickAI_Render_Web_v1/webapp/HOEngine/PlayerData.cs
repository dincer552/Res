namespace HattrickAI.HOEngine;

public sealed class PlayerData
{
    public int PlayerId { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public int Form { get; set; }
    public int Stamina { get; set; }
    public int Experience { get; set; }
    public int Leadership { get; set; }
    public int Loyalty { get; set; } = 20;
    public string Specialty { get; set; } = "";

    public int Keeper { get; set; }
    public int Defending { get; set; }
    public int Playmaking { get; set; }
    public int Winger { get; set; }
    public int Passing { get; set; }
    public int Scoring { get; set; }
    public int SetPieces { get; set; }

    public bool Injured { get; set; }
    public bool Suspended { get; set; }

    public double PositionScore(string position)
    {
        var calculator = new PlayerRatingCalculator();
        var role = position.ToLowerInvariant() switch
        {
            "kaleci" or "keeper" => PlayerRole.Goalkeeper,
            "sol bek" or "leftback" => PlayerRole.LeftDefender,
            "defans" or "stoper" or "centraldefender" => PlayerRole.CentralDefender,
            "sağ bek" or "rightback" => PlayerRole.RightDefender,
            "sol kanat" or "leftwinger" => PlayerRole.LeftWinger,
            "kanat" or "rightwinger" => PlayerRole.RightWinger,
            "ortasaha" or "orta saha" or "merkez orta saha" => PlayerRole.CentralMidfielder,
            "sol forvet" or "leftforward" => PlayerRole.LeftForward,
            "forvet" or "santrfor" or "centralforward" => PlayerRole.CentralForward,
            "sağ forvet" or "rightforward" => PlayerRole.RightForward,
            _ => PlayerRole.CentralMidfielder
        };

        return new LineupRatingEngine().GetPlayerPositionRating(
            this,
            role,
            PlayerBehaviour.Normal);
    }
}
