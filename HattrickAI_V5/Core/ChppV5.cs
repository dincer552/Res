using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;

namespace HattrickAI.V5.Core;

public sealed record Credentials(string Key, string Secret);

public sealed class ChppV5
{
    private const string RequestUrl = "https://chpp.hattrick.org/oauth/request_token.ashx";
    private const string AuthorizeUrl = "https://chpp.hattrick.org/oauth/authorize.aspx";
    private const string AccessUrl = "https://chpp.hattrick.org/oauth/access_token.ashx";
    private const string ApiUrl = "https://chpp.hattrick.org/chppxml.ashx";
    private readonly HttpClient _http;
    private readonly Credentials _credentials;
    private readonly IHttpContextAccessor _context;

    public ChppV5(Credentials credentials, IHttpContextAccessor context)
    {
        _credentials = credentials;
        _context = context;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    private ISession Session => _context.HttpContext?.Session ?? throw new InvalidOperationException("HTTP oturumu bulunamadı.");
    private string? AccessToken => Session.GetString("v5.access");
    private string? AccessSecret => Session.GetString("v5.accessSecret");

    public bool Connected => !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(AccessSecret);

    public async Task<string> StartAsync(string callback, CancellationToken ct)
    {
        var oauth = OAuthParameters(callback, null, null);
        var signature = Signature("GET", RequestUrl, oauth, null, null);
        var url = Query(RequestUrl, oauth, ("oauth_signature", signature));
        using var response = await _http.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();
        var values = Form(body);
        if (!values.TryGetValue("oauth_token", out var token) || !values.TryGetValue("oauth_token_secret", out var secret))
            throw new InvalidOperationException("CHPP request token alınamadı.");
        Session.SetString("v5.request", token);
        Session.SetString("v5.requestSecret", secret);
        return AuthorizeUrl + "?oauth_token=" + Encode(token);
    }

    public async Task FinishAsync(string verifier, CancellationToken ct)
    {
        var token = Session.GetString("v5.request");
        var secret = Session.GetString("v5.requestSecret");
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("CHPP yetkilendirme oturumu bulunamadı.");
        var oauth = OAuthParameters(null, token, verifier.Trim());
        var signature = Signature("GET", AccessUrl, oauth, secret, null);
        var url = Query(AccessUrl, oauth, ("oauth_signature", signature));
        using var response = await _http.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();
        var values = Form(body);
        if (!values.TryGetValue("oauth_token", out var access) || !values.TryGetValue("oauth_token_secret", out var accessSecret))
            throw new InvalidOperationException("CHPP access token alınamadı.");
        Session.SetString("v5.access", access);
        Session.SetString("v5.accessSecret", accessSecret);
        Session.Remove("v5.request");
        Session.Remove("v5.requestSecret");
    }

    public void Disconnect()
    {
        Session.Remove("v5.access");
        Session.Remove("v5.accessSecret");
        Session.Remove("v5.request");
        Session.Remove("v5.requestSecret");
    }

    public async Task<string> GetXmlAsync(string file, IDictionary<string,string?> parameters, CancellationToken ct)
    {
        if (!Connected) throw new InvalidOperationException("CHPP bağlantısı yok.");
        var query = new List<KeyValuePair<string,string>> { new("file", file) };
        query.AddRange(parameters.Where(p => !string.IsNullOrWhiteSpace(p.Value)).Select(p => new KeyValuePair<string,string>(p.Key, p.Value!)));
        var requestUrl = ApiUrl + "?" + string.Join("&", query.Select(p => Encode(p.Key) + "=" + Encode(p.Value)));
        var oauth = OAuthParameters(null, AccessToken, null);
        var signing = query.Select(p => new KeyValuePair<string,string>(p.Key, p.Value)).Concat(oauth.Select(p => new KeyValuePair<string,string>(p.Key,p.Value)));
        var signature = Signature("GET", ApiUrl, oauth, AccessSecret, signing);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.TryAddWithoutValidation("Authorization", "OAuth " + string.Join(", ", oauth.Select(p => Encode(p.Key)+"=\""+Encode(p.Value)+"\"").Append("oauth_signature=\""+Encode(signature)+"\"")));
        request.Headers.TryAddWithoutValidation("User-Agent", "HattrickAI-V5/1.0");
        request.Headers.TryAddWithoutValidation("Accept", "application/xml,text/xml,*/*");
        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();
        return body;
    }

    private Dictionary<string,string> OAuthParameters(string? callback, string? token, string? verifier)
    {
        var d = new Dictionary<string,string>(StringComparer.Ordinal)
        {
            ["oauth_consumer_key"] = _credentials.Key,
            ["oauth_nonce"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["oauth_version"] = "1.0"
        };
        if (!string.IsNullOrWhiteSpace(callback)) d["oauth_callback"] = callback;
        if (!string.IsNullOrWhiteSpace(token)) d["oauth_token"] = token;
        if (!string.IsNullOrWhiteSpace(verifier)) d["oauth_verifier"] = verifier;
        return d;
    }

    private string Signature(string method, string baseUrl, IDictionary<string,string> oauth, string? tokenSecret, IEnumerable<KeyValuePair<string,string>>? all)
    {
        var values = (all ?? oauth.Select(x => new KeyValuePair<string,string>(x.Key,x.Value)))
            .Select(p => new KeyValuePair<string,string>(Encode(p.Key), Encode(p.Value)))
            .OrderBy(p => p.Key, StringComparer.Ordinal).ThenBy(p => p.Value, StringComparer.Ordinal);
        var normalized = string.Join("&", values.Select(p => p.Key + "=" + p.Value));
        var baseString = method.ToUpperInvariant() + "&" + Encode(baseUrl) + "&" + Encode(normalized);
        var signingKey = Encode(_credentials.Secret) + "&" + Encode(tokenSecret ?? string.Empty);
        using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(signingKey));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(baseString)));
    }

    private static string Query(string url, IDictionary<string,string> values, params (string Key,string Value)[] extra)
    {
        var list = values.Select(x => Encode(x.Key)+"="+Encode(x.Value)).ToList();
        list.AddRange(extra.Select(x => Encode(x.Key)+"="+Encode(x.Value)));
        return url + "?" + string.Join("&", list);
    }

    private static Dictionary<string,string> Form(string value) => value.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=',2)).Where(x => x.Length==2).ToDictionary(x => WebUtility.UrlDecode(x[0]) ?? x[0], x => WebUtility.UrlDecode(x[1]) ?? x[1]);
    private static string Encode(string value) => Uri.EscapeDataString(value);
}

public static class XmlV5
{
    public static XElement? Root(string xml) => XDocument.Parse(xml).Root;
    public static string Text(XElement? e, string name) => e?.Element(name)?.Value?.Trim() ?? e?.Descendants(name).FirstOrDefault()?.Value?.Trim() ?? string.Empty;
    public static int Int(XElement? e, string name) => int.TryParse(Text(e,name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    public static DateTimeOffset Date(XElement? e, string name) => DateTimeOffset.TryParse(Text(e,name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var v) ? v : default;
}
