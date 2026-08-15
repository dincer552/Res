namespace HattrickAI.HOEngine;

public class PlayerFileService
{
    private readonly PlayerHtmlLoader loader = new();

    public async Task<List<PlayerData>> LoadPlayersAsync()
    {
        FileResult? file =
            await FilePicker.Default.PickAsync(
                new PickOptions
                {
                    PickerTitle =
                        "Hattrick oyuncu sayfasını seç"
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

        string html =
            await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidDataException(
                "Oyuncu dosyası boş.");
        }

        List<PlayerData> players =
            loader.Load(html);

        if (players.Count == 0)
        {
            throw new InvalidDataException(
                "Oyuncu bulunamadı.");
        }

        return players;
    }
}