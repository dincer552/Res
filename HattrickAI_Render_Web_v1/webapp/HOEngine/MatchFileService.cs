namespace HattrickAI.HOEngine;

public class MatchFileService
{
    private readonly MatchJsonLoader loader = new();

    public async Task<MatchData> LoadLatestMatchAsync(
        string teamName)
    {
        FileResult? file =
            await FilePicker.Default.PickAsync(
                new PickOptions
                {
                    PickerTitle =
                        "Hattrick JSON dosyasını seç"
                });

        if (file == null)
        {
            throw new OperationCanceledException(
                "Dosya seçilmedi.");
        }

        await using Stream stream =
            await file.OpenReadAsync();

        using var reader =
            new StreamReader(stream);

        string json =
            await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException(
                "JSON dosyası boş.");
        }

        MatchData? match =
            loader.FindLatestMatch(
                json,
                teamName);

        if (match == null)
        {
            throw new InvalidDataException(
                $"{teamName} için maç bulunamadı.");
        }

        return match;
    }
}