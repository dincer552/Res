using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HattrickAI.Web;

public static class ClimatixWebhookExtensions
{
    private const string SignatureHeader = "X-Climatix-Signature";
    private const string DefaultFanDatapointId = "1!6WHH73LSF8XLAY";
    private static readonly ConcurrentDictionary<string, ClimatixStatusSnapshot> Latest = new();

    public static IEndpointRouteBuilder MapClimatixWebhook(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/climatix/webhook", async (HttpRequest request) =>
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var body = await reader.ReadToEndAsync(request.HttpContext.RequestAborted);
            request.Body.Position = 0;

            var secret = Environment.GetEnvironmentVariable("CLIMATIX_WEBHOOK_SECRET");
            if (string.IsNullOrWhiteSpace(secret))
                return Results.Problem("CLIMATIX_WEBHOOK_SECRET ortam değişkeni ayarlanmamış.", statusCode: 503);

            var signature = request.Headers[SignatureHeader].FirstOrDefault();
            if (!IsValidSignature(body, signature, secret))
                return Results.Unauthorized();

            var fanDatapointId = Environment.GetEnvironmentVariable("CLIMATIX_FAN_DATAPOINT_ID") ?? DefaultFanDatapointId;
            using var document = JsonDocument.Parse(body);
            var fanSpeed = FindDatapointValue(document.RootElement, fanDatapointId, "Supply Fan Speed");
            if (fanSpeed is null)
            {
                return Results.BadRequest(new
                {
                    ok = false,
                    message = "Supply Fan Speed datapoint değeri webhook gövdesinde bulunamadı.",
                    expectedDatapointId = fanDatapointId
                });
            }

            var status = fanSpeed.Value > 5d ? "ON" : "OFF";
            var now = DateTimeOffset.UtcNow;
            var plantId = Environment.GetEnvironmentVariable("CLIMATIX_PLANT_ID") ?? "unknown";
            Latest[plantId] = new ClimatixStatusSnapshot(plantId, fanDatapointId, fanSpeed.Value, status, now);

            Console.WriteLine($"CLIMATIX webhook: plant={plantId}, fanSpeed={fanSpeed.Value:0.###}, status={status}");

            return Results.Ok(new { ok = true, plantId, fanSpeed = fanSpeed.Value, status, updatedAtUtc = now });
        });

        endpoints.MapGet("/api/climatix/status", () =>
        {
            var plantId = Environment.GetEnvironmentVariable("CLIMATIX_PLANT_ID") ?? "unknown";
            return Latest.TryGetValue(plantId, out var snapshot)
                ? Results.Ok(snapshot)
                : Results.NotFound(new { ok = false, message = "Henüz Climatix webhook verisi alınmadı.", plantId });
        });

        return endpoints;
    }

    private static bool IsValidSignature(string body, string? suppliedSignature, string secret)
    {
        if (string.IsNullOrWhiteSpace(suppliedSignature)) return false;

        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        var hex = Convert.ToHexString(digest);
        var base64 = Convert.ToBase64String(digest);
        var normalized = suppliedSignature.Trim();

        return SecureEquals(normalized, hex)
               || SecureEquals(normalized, base64)
               || (normalized.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
                   && SecureEquals(normalized[7..], hex));
    }

    private static bool SecureEquals(string left, string right)
    {
        if (left.Length != right.Length) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    }

    private static double? FindDatapointValue(JsonElement element, string datapointId, string datapointName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                string? objectDatapointId = null;
                string? objectName = null;
                double? directValue = null;

                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("datapointId") || property.NameEquals("dataPointId") || property.NameEquals("id"))
                        objectDatapointId = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                    else if (property.NameEquals("name") || property.NameEquals("datapointName") || property.NameEquals("dataPointName"))
                        objectName = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                    else if (property.NameEquals("value") && property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var number))
                        directValue = number;
                }

                if (directValue is not null &&
                    (string.Equals(objectDatapointId, datapointId, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(objectName, datapointName, StringComparison.OrdinalIgnoreCase)))
                    return directValue;

                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, datapointId, StringComparison.OrdinalIgnoreCase))
                    {
                        var nested = FindNumericValue(property.Value);
                        if (nested is not null) return nested;
                    }

                    var nestedResult = FindDatapointValue(property.Value, datapointId, datapointName);
                    if (nestedResult is not null) return nestedResult;
                }
                break;
            }

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nestedResult = FindDatapointValue(item, datapointId, datapointName);
                    if (nestedResult is not null) return nestedResult;
                }
                break;
        }

        return null;
    }

    private static double? FindNumericValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var direct)) return direct;
        if (element.ValueKind != JsonValueKind.Object) return null;

        foreach (var property in element.EnumerateObject())
        {
            if ((property.NameEquals("value") || property.NameEquals("Value")) &&
                property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var value))
                return value;

            var nested = FindNumericValue(property.Value);
            if (nested is not null) return nested;
        }

        return null;
    }

    private sealed record ClimatixStatusSnapshot(string PlantId, string FanDatapointId, double FanSpeed, string Status, DateTimeOffset UpdatedAtUtc);
}
