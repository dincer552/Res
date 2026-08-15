using HattrickAI.CHPP;
using HattrickAI.HOEngine;

namespace HattrickAI.Web;

public static class HistoricalFormationMapper
{
    public static string InferFormation(IReadOnlyList<ChppLineupPlayer> players)
    {
        if (players is null || players.Count != 11)
            return "";

        var roles = players.Select(ClassifyLine).ToList();
        if (roles.Count(r => r == LineFamily.Goalkeeper) != 1)
            return "";

        var fieldPlayers = roles.Count(r => r != LineFamily.Goalkeeper);
        if (fieldPlayers != 10 || roles.Any(r => r == LineFamily.Unknown))
            return "";

        var defenders = roles.Count(r => r == LineFamily.Defender);
        var midfielders = roles.Count(r => r == LineFamily.Midfielder);
        var forwards = roles.Count(r => r == LineFamily.Forward);

        if (defenders is < 2 or > 5 || midfielders is < 1 or > 5 || forwards is < 1 or > 3)
            return "";

        return $"{defenders}-{midfielders}-{forwards}";
    }

    public static PlayerRole HistoricalRole(ChppLineupPlayer player, string formation)
    {
        // PositionCode is the direct position source. RoleID is used only where
        // the formal historical slot carries an extra-role/repositioning detail.
        return player.PositionCode switch
        {
            1 => PlayerRole.Goalkeeper,
            2 => RoleOverride(player, PlayerRole.RightDefender),
            3 or 4 => RoleOverride(player, PlayerRole.CentralDefender),
            5 => RoleOverride(player, PlayerRole.LeftDefender),
            6 => RoleOverride(player, PlayerRole.RightWinger),
            7 or 8 => RoleOverride(player, PlayerRole.CentralMidfielder),
            9 => RoleOverride(player, PlayerRole.LeftWinger),
            10 or 11 => ForwardRoleFromChpp(player.RoleId),
            _ => RoleFromChppRoleId(player.RoleId) ?? PlayerRole.CentralMidfielder
        };
    }

    private static PlayerRole RoleOverride(ChppLineupPlayer player, PlayerRole positionRole)
    {
        var role = RoleFromChppRoleId(player.RoleId);
        return role.HasValue && RoleFamily(role.Value) != RoleFamily(positionRole)
            ? role.Value
            : positionRole;
    }

    private static LineFamily ClassifyLine(ChppLineupPlayer player)
    {
        var role = RoleFromChppRoleId(player.RoleId);
        if (role.HasValue)
            return RoleFamily(role.Value);

        return player.PositionCode switch
        {
            1 => LineFamily.Goalkeeper,
            >= 2 and <= 5 => LineFamily.Defender,
            >= 6 and <= 9 => LineFamily.Midfielder,
            10 or 11 => LineFamily.Forward,
            _ => LineFamily.Unknown
        };
    }

    private static PlayerRole ForwardRoleFromChpp(int roleId) => roleId switch
    {
        111 => PlayerRole.RightForward,
        113 => PlayerRole.LeftForward,
        _ => PlayerRole.CentralForward
    };

    private static PlayerRole? RoleFromChppRoleId(int roleId) => roleId switch
    {
        100 => PlayerRole.Goalkeeper,
        101 => PlayerRole.RightDefender,
        102 or 103 or 104 => PlayerRole.CentralDefender,
        105 => PlayerRole.LeftDefender,
        106 => PlayerRole.RightWinger,
        107 or 108 or 109 => PlayerRole.CentralMidfielder,
        110 => PlayerRole.LeftWinger,
        111 => PlayerRole.RightForward,
        112 => PlayerRole.CentralForward,
        113 => PlayerRole.LeftForward,
        _ => null
    };

    private static LineFamily RoleFamily(PlayerRole role) => role switch
    {
        PlayerRole.Goalkeeper => LineFamily.Goalkeeper,
        PlayerRole.LeftDefender or PlayerRole.CentralDefender or PlayerRole.RightDefender => LineFamily.Defender,
        PlayerRole.LeftMidfielder or PlayerRole.CentralMidfielder or PlayerRole.RightMidfielder or PlayerRole.LeftWinger or PlayerRole.RightWinger => LineFamily.Midfielder,
        PlayerRole.LeftForward or PlayerRole.CentralForward or PlayerRole.RightForward => LineFamily.Forward,
        _ => LineFamily.Unknown
    };

    private enum LineFamily
    {
        Unknown,
        Goalkeeper,
        Defender,
        Midfielder,
        Forward
    }
}
