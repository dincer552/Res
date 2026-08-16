namespace HattrickAI.Web;

public static class AppVersion
{
    public const string SourceFileName = "VERSION";

    public static string Current
    {
        get
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, SourceFileName),
                Path.Combine(Directory.GetCurrentDirectory(), SourceFileName)
            };
            var path = candidates.FirstOrDefault(File.Exists);
            if (path is null)
                throw new FileNotFoundException($"Application version source not found. Checked: {string.Join(", ", candidates)}");

            var value = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith('v'))
                throw new InvalidOperationException("VERSION must contain a non-empty semantic version without the leading 'v'.");

            return value;
        }
    }

    public static string Display => $"v{Current}";
}
