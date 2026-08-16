namespace HattrickAI.Web;

public static class AppVersion
{
    public const string SourceFileName = "VERSION";

    public static string Current
    {
        get
        {
            var path = Path.Combine(AppContext.BaseDirectory, SourceFileName);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Application version source not found: {path}");

            var value = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith('v'))
                throw new InvalidOperationException("VERSION must contain a non-empty semantic version without the leading 'v'.");

            return value;
        }
    }

    public static string Display => $"v{Current}";
}
