// v23.01.21 deployment verification: keep the regression suite on every Render-triggering push.
using HattrickAI.CHPP;
using HattrickAI.HOEngine;
using HattrickAI.Web;

namespace HattrickAI.FormationTests;

internal static class HistoricalFormationMapperTests
{
    public static int RunAll()
    {
        var tests = new (string Name, System.Action Test)[]
        {
            ("3-5-2", () => AssertFormation("3-5-2", new[] { 100, 101, 103, 105, 106, 107, 108, 109, 110, 111, 113 })),
            ("4-4-2", () => AssertFormation("4-4-2", new[] { 100, 101, 102, 104, 105, 106, 107, 109, 110, 111, 113 })),
            ("4-3-3", () => AssertFormation("4-3-3", new[] { 100, 101, 102, 104, 105, 107, 108, 109, 111, 112, 113 })),
            ("5-3-2", () => AssertFormation("5-3-2", new[] { 100, 101, 102, 103, 104, 105, 107, 108, 109, 111, 113 })),
            ("keeper excluded", GoalkeeperIsNotCountedAsOutfieldPlayer),
            ("PositionCode mapping", HistoricalRoleUsesPositionCode),
            ("invalid keeper count", InvalidLineupWithoutExactlyOneKeeperIsRejected)
        };

        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
            }
        }

        Console.WriteLine($"Formation regression tests: {tests.Length - failures}/{tests.Length} passed.");
        return failures;
    }

    private static void AssertFormation(string expected, int[] roleIds)
    {
        var players = roleIds.Select((roleId, i) => CreatePlayer(i + 1, PositionCodeForRole(roleId), roleId)).ToList();
        var actual = HistoricalFormationMapper.InferFormation(players);
        AssertEqual(expected, actual, $"Expected {expected}, got {actual}");
    }

    private static void GoalkeeperIsNotCountedAsOutfieldPlayer()
    {
        var players = new List<ChppLineupPlayer>
        {
            CreatePlayer(1, 1, 100),
            CreatePlayer(2, 2, 101), CreatePlayer(3, 3, 103), CreatePlayer(4, 5, 105),
            CreatePlayer(5, 6, 106), CreatePlayer(6, 7, 107), CreatePlayer(7, 8, 108), CreatePlayer(8, 9, 110),
            CreatePlayer(9, 4, 108), CreatePlayer(10, 10, 111), CreatePlayer(11, 11, 113)
        };

        AssertEqual("3-5-2", HistoricalFormationMapper.InferFormation(players), "Goalkeeper must not be counted as an outfield player");
    }

    private static void HistoricalRoleUsesPositionCode()
    {
        AssertEqual(PlayerRole.Goalkeeper, HistoricalFormationMapper.HistoricalRole(CreatePlayer(1, 1, 100), "4-4-2"));
        AssertEqual(PlayerRole.RightDefender, HistoricalFormationMapper.HistoricalRole(CreatePlayer(2, 2, 101), "4-4-2"));
        AssertEqual(PlayerRole.CentralDefender, HistoricalFormationMapper.HistoricalRole(CreatePlayer(3, 3, 103), "4-4-2"));
        AssertEqual(PlayerRole.LeftDefender, HistoricalFormationMapper.HistoricalRole(CreatePlayer(4, 5, 105), "4-4-2"));
        AssertEqual(PlayerRole.RightWinger, HistoricalFormationMapper.HistoricalRole(CreatePlayer(5, 6, 106), "4-4-2"));
        AssertEqual(PlayerRole.CentralMidfielder, HistoricalFormationMapper.HistoricalRole(CreatePlayer(6, 7, 107), "4-4-2"));
        AssertEqual(PlayerRole.LeftWinger, HistoricalFormationMapper.HistoricalRole(CreatePlayer(7, 9, 110), "4-4-2"));
        AssertEqual(PlayerRole.LeftForward, HistoricalFormationMapper.HistoricalRole(CreatePlayer(8, 10, 113), "4-4-2"));
        AssertEqual(PlayerRole.RightForward, HistoricalFormationMapper.HistoricalRole(CreatePlayer(9, 11, 111), "4-4-2"));
        AssertEqual(PlayerRole.CentralMidfielder, HistoricalFormationMapper.HistoricalRole(CreatePlayer(10, 4, 108), "3-5-2"));
    }

    private static void InvalidLineupWithoutExactlyOneKeeperIsRejected()
    {
        var players = Enumerable.Range(1, 11).Select(i => CreatePlayer(i, 2, 101)).ToList();
        AssertEqual("", HistoricalFormationMapper.InferFormation(players), "Lineup without exactly one goalkeeper must be rejected");
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

    private static void AssertEqual<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(message ?? $"Expected {expected}, got {actual}");
    }
}
