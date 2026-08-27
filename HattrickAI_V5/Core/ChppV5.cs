using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace HattrickAI.V5.Core;

public sealed record Credentials(string Key, string Secret);

public sealed class ChppV5
{
    private const string RequestTokenEndpoint = "https://chpp.hattrick.org/oauth/request_token.ashx";
    private const string AuthorizeEndpoint = "https://chpp.hattrick.org/oauth/authorize.aspx";
    private const string AccessTokenEndpoint = "https://chpp.hattrick.org/oauth/access_token.ashx";
    private const string ApiEndpoint = "https://chpp.hattrick.org/chppxml.ashx";
    private readonly HttpClient _http;
    private readonly Credentials _credentials;
    private readonly Dictionary<string,string> _session = new();

    public ChppV5(Credentials credentials)
    {
        _credentials = credentials;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public bool Connected => _session.ContainsKey("access") && _session.ContainsKey("accessSecret");
    public void LoadAccess(string token, string secret) { _session["access"] = token; _session["accessSecret"] = secret; }
    public void Clear() { _session.Clear(); }

    public async Task<string> BeginAsync(string callbackUrl, CancellationToken ct)
    {
        var oauth = OAuthParams(callback: callbackUrl, token: null, verifier: null);
        var signature = Sign("GET", RequestTokenEndpoint, oauth, null);
        var query = string.Join("&", oauth.Select(x => Encode(x.Key) + "=" + Encode(x.Value)).Append("oauth_signature=" + Encode(signature)));
        using var response = await _http.GetAsync(RequestTokenEndpoint + "?" + query, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();
        var form = Parse(body);
        if (!form.TryGetValue("oauth_token", out var token) || !form.TryGetValue("oauth_token_secret", out var secret))
            throw new InvalidOperationException("CHPP request token alınamadı.");
        _session["request"] = token; _session["requestSecret"] = secret;
        return AuthorizeEndpoint + "?oauth_token=" + Encode(token);
    }

    public async Task CompleteAsync(string verifier, CancellationToken ct)
    {
        if (!_session.TryGetValue("request", out var token) || !_session.TryGetValue("requestSecret", out var secret))
            throw new InvalidOperationException("CHPP yetkilendirme oturumu bulunamadı.");
        var oauth = OAuthParams(callback: null, token: token, verifier: verifier);
        var signature = Sign("GET", AccessTokenEndpoint, oauth, secret);
        var query = string.Join("&", oauth.Select(x => Encode(x.Key) + "=" + Encode(x.Value)).Append("oauth_signature=" + Encode(signature)));
        using var response = await _http.GetAsync(AccessTokenEndpoint + "?" + query, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();
        var form = Parse(body);
        if (!form.TryGetValue("oauth_token", out var access) || !form.TryGetValue("oauth_token_secret", out var accessSecret))
            throw new InvalidOperationException("CHPP access token alınamadı.");
        LoadAccess(access, accessSecret);
        _session.Remove("request"); _session.Remove("requestSecret");
    }

    public async Task<string> GetXmlAsync(string file, IDictionary<string,string?> parameters, CancellationToken ct)
    {
        if (!Connected) throw new InvalidOperationException("CHPP bağlantısı yok.");
        var query = new List<KeyValuePair<string,string>> { new("file", file) };
        query.AddRange(parameters.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => new KeyValuePair<string,string>(x.Key, x.Value!)));
        var url = ApiEndpoint + "?" + string.Join("&", query.Select(x => Encode(x.Key) + "=" + Encode(x.Value)));
        var oauth = OAuthParams(null, _session["access"], null);
        var all = new List<KeyValuePair<string,string>>(query);
        all.AddRange(oauth.Select(x => new KeyValuePair<string,string>(x.Key, x.Value)));
        var signature = Sign("GET", ApiEndpoint + "?" + string.Join("&", query.Select(x => Encode(x.Key) + "=" + Encode(x.Value))), oauth, _session["accessSecret"], all);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", "OAuth " + string.Join(", ", oauth.Select(x => Encode(x.Key) + "=\"" + Encode(x.Value) + "\"").Append("oauth_signature=\"" + Encode(signature) + "\"")));
        request.Headers.TryAddWithoutValidation("User-Agent", "HattrickAI-V5/1.0");
        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();
        return body;
    }

    private Dictionary<string,string> OAuthParams(string? callback, string? token, string? verifier)
    {
        var d = new Dictionary<string,string>(StringComparer.Ordinal)
        {
            ["oauth_consumer_key"] = _credentials.Key,
            ["oauth_nonce"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant(),
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["oauth_version"] = "1.0"
        };
        if (callback != null) d["oauth_callback"] = callback;
        if (token != null) d["oauth_token"] = token;
        if (verifier != null) d["oauth_verifier"] = verifier;
        return d;
    }

    private string Sign(string method, string url, IDictionary<string,string> oauth, string? tokenSecret, IEnumerable<KeyValuePair<string,string>>? extra = null)
    {
        var values = new List<KeyValuePair<string,string>>();
        values.AddRange(oauth);
        if (extra != null) values.AddRange(extra);
        var normalized = string.Join("&", values.Select(p => new { K = Encode(p.Key), V = Encode(p.Value) }).OrderBy(p => p.K, StringComparer.Ordinal).ThenBy(p => p.V, StringComparer.Ordinal).Select(p => p.K + "=" + p.V));
        var baseString = method.ToUpperInvariant() + "&" + Encode(url.Split('?')[0]) + "&" + Encode(normalized);
        var key = Encode(_credentials.Secret) + "&" + Encode(tokenSecret ?? string.Empty);
        using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(key));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(baseString)));
    }

    private static string Encode(string value) => Uri.EscapeDataString(value).Replace("%20", "%20", StringComparison.Ordinal);
    private static Dictionary<string,string> Parse(string text) => text.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=',2)).Where(p => p.Length==2).ToDictionary(p => WebUtility.UrlDecode(p[0]) ?? p[0], p => WebUtility.UrlDecode(p[1]) ?? p[1]);
}

public static class XmlV5
{
    public static XElement? Root(string xml) => XDocument.Parse(xml).Root;
    public static XElement? Desc(XContainer? root, string name) => root?.Descendants(name).FirstOrDefault();
    public static string Text(XElement? e, string name) => e?.Element(name)?.Value?.Trim() ?? e?.Descendants(name).FirstOrDefault()?.Value?.Trim() ?? string.Empty;
    public static int Int(XElement? e, string name) => int.TryParse(Text(e,name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    public static DateTimeOffset Date(XElement? e, string name) => DateTimeOffset.TryParse(Text(e,name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var v) ? v : default;
}
