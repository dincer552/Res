using HattrickAI.CHPP;
using HattrickAI.HOEngine;
using HattrickAI.Web;

namespace HattrickAI.FormationTests;

public sealed class HistoricalFormationMapperTests
{
    public static IEnumerable<object[]> FormationCases()
    {
        yield return new object[] { "3-5-2", new[] { 100, 101, 103, 105, 106, 107, 108, 109, 110, 111, 113 } };
        yield return new object[] { "4-4-2", new[] { 100, 101, 102, 104, 105, 106, 107, 109, 110, 111, 113 } };
        yield return new object[] { "4-3-3", new[] { 100, 101, 102, 104, 105, 107, 108, 109, 111, 112, 113 } };
        yield return new object[] { "5-3-2", new[] { 100, 101, 102, 103, 104, 105, 107, 108, 109, 111, 113 } };
    }

    [Theory]
    [MemberData(nameof(FormationCases))]
    public void InferFormation_ReturnsExpectedFormation(string expected, int[] roleIds)
    {
        var players = roleIds.Select((roleId, i) => CreatePlayer(i + 1, PositionCodeForRole(roleId), roleId)).ToList();
        Assert.Equal(expected, HistoricalFormationMapper.InferFormation(players));
    }

    [Fact]
    public void Goalkeeper_IsNotCountedAsOutfieldPlayer()
    {
        var players = new List<ChppLineupPlayer>
        {
            CreatePlayer(1, 1, 100),
            CreatePlayer(2, 2, 101), CreatePlayer(3, 3, 103), CreatePlayer(4, 5, 105),
            CreatePlayer(5, 6, 106), CreatePlayer(6, 7, 107), CreatePlayer(7, 8, 108), CreatePlayer(8, 9, 110),
            CreatePlayer(9, 4, 108), CreatePlayer(10, 10, 111), CreatePlayer(11, 11, 113)
        };

        Assert.Equal("3-5-2", HistoricalFormationMapper.InferFormation(players));
    }

    [Fact]
    public void HistoricalRole_UsesPositionCodeAndCorrectsRepositionedSlot()
    {
        Assert.Equal(PlayerRole.Goalkeeper, HistoricalFormationMapper.HistoricalRole(CreatePlayer(1, 1, 100), "4-4-2"));
        Assert.Equal(PlayerRole.RightDefender, HistoricalFormationMapper.HistoricalRole(CreatePlayer(2, 2, 101), "4-4-2"));
        Assert.Equal(PlayerRole.CentralDefender, HistoricalFormationMapper.HistoricalRole(CreatePlayer(3, 3, 103), "4-4-2"));
        Assert.Equal(PlayerRole.LeftDefender, HistoricalFormationMapper.HistoricalRole(CreatePlayer(4, 5, 105), "4-4-2"));
        Assert.Equal(PlayerRole.RightWinger, HistoricalFormationMapper.HistoricalRole(CreatePlayer(5, 6, 106), "4-4-2"));
        Assert.Equal(PlayerRole.CentralMidfielder, HistoricalFormationMapper.HistoricalRole(CreatePlayer(6, 7, 107), "4-4-2"));
        Assert.Equal(PlayerRole.LeftWinger, HistoricalFormationMapper.HistoricalRole(CreatePlayer(7, 9, 110), "4-4-2"));
        Assert.Equal(PlayerRole.LeftForward, HistoricalFormationMapper.HistoricalRole(CreatePlayer(8, 10, 113), "4-4-2"));
        Assert.Equal(PlayerRole.RightForward, HistoricalFormationMapper.HistoricalRole(CreatePlayer(9, 11, 111), "4-4-2"));
        Assert.Equal(PlayerRole.CentralMidfielder, HistoricalFormationMapper.HistoricalRole(CreatePlayer(10, 4, 108), "3-5-2"));
    }

    [Fact]
    public void InvalidLineupWithoutExactlyOneKeeper_IsRejected()
    {
        var players = Enumerable.Range(1, 11).Select(i => CreatePlayer(i, 2, 101)).ToList();
        Assert.Equal("", HistoricalFormationMapper.InferFormation(players));
    }

    private static ChppLineupPlayer CreatePlayer(int id, int positionCode, int roleId)
        => new(id, $"P{id}", roleId, positionCode, 0, 5.0);

    private static int PositionCodeForRole(int roleId) => roleId switch
    {
        100 => 1,
        101 => 2,
        102 => 3,
        103 => 4,
        104 => 4,
        105 => 5,
        106 => 6,
        107 => 7,
        108 => 8,
        109 => 8,
        110 => 9,
        111 => 10,
        112 => 11,
        113 => 11,
        _ => 0
    };
}
