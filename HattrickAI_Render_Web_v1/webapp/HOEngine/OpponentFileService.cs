namespace HattrickAI.HOEngine;

public class OpponentFileService
{
    private readonly OpponentHtmlLoader _loader = new();

    public async Task<OpponentMatchData> LoadOpponentAsync()
    {
        FileResult? file =
            await FilePicker.Default.PickAsync(
                new PickOptions
                {
                    PickerTitle = "Select the clean Hattrick match report"
                });

        if (file == null)
            throw new OperationCanceledException("Opponent file was not selected.");

        await using Stream stream =
            await file.OpenReadAsync();

        using var reader = new StreamReader(stream);

        string html = await reader.ReadToEndAsync();

        return _loader.Load(html);
    }
}
