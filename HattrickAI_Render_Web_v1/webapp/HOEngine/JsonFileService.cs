using System.Text.Json;

namespace HattrickAI.HOEngine;

public class JsonFileService
{
    private readonly TeamJsonLoader loader = new();

    public async Task<TeamInput> LoadTeamAsync()
    {
        FileResult? file =
            await FilePicker.Default.PickAsync(
                new PickOptions
                {
                    PickerTitle = "Hattrick JSON dosyasını seç"
                });

        if (file == null)
            throw new OperationCanceledException(
                "Dosya seçilmedi.");

        await using Stream stream =
            await file.OpenReadAsync();

        using var reader =
            new StreamReader(stream);

        string json =
            await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException(
                "JSON dosyası boş.");

        return loader.Load(json);
    }
}