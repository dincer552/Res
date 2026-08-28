using HattrickAI.Web;

namespace HattrickAI.FormationTests;

internal static class VersionSourceTests
{
    public static int RunAll()
    {
        try
        {
            var current = AppVersion.Current;
            AssertTrue(!string.IsNullOrWhiteSpace(current), "VERSION source must not be empty");
            AssertTrue(!current.StartsWith('v'), "VERSION source must not contain a leading v");
            AssertTrue(AppVersion.Display == $"v{current}", "Display version must come from VERSION source");
            AssertTrue(File.Exists(Path.Combine(AppContext.BaseDirectory, AppVersion.SourceFileName)),
                "VERSION must be published with the application");
            Console.WriteLine($"PASS version source: {AppVersion.Display}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL version source: {ex.Message}");
            return 1;
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
