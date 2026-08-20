using System.Text.Json;
using HattrickAI.HOEngine;

namespace HattrickAI.Web.CHPP;

/// <summary>
/// Builds a portable, token-free dataset from data already fetched from CHPP.
/// The export is intended for offline testing/calibration and contains no OAuth/session secrets.
/// </summary>
public sealed class ChppDatasetExportService
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string Export(object payload)
        => JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            exportedAtUtc = DateTime.UtcNow,
            purpose = "HattrickAI offline lineup/simulation testing",
            data = payload
        }, _json);
}
