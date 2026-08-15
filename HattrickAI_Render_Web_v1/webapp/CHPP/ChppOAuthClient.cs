using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace HattrickAI.CHPP;

public sealed record ChppAccessToken(string Token, string TokenSecret);

public interface IChppTokenStore
{
    Task SetAsync(string key, string value);
    Task<string?> GetAsync(string key);
    void Remove(string key);
}

/// <summary>
/// CHPP OAuth 1.0a client modeled on HO!'s current Connector/OAuthDialog flow.
/// Hattrick CHPP OAuth 1.0a client. The implementation follows the current CHPP
/// OAuth guide for desktop applications (oauth_callback=oob, GET endpoints,
/// HMAC-SHA1) while preserving the same OAuth parameter/signature normalization
/// approach used by HO!/ScribeJava.
/// </summary>
public sealed class ChppOAuthClient
{
    public const string AccessTokenKey = "chpp_access_token";
    public const string AccessTokenSecretKey = "chpp_access_token_secret";
    private const string RequestTokenKey = "chpp_request_token";
    private const string RequestTokenSecretKey = "chpp_request_token_secret";

    private const string RequestTokenUrl = "https://chpp.hattrick.org/oauth/request_token.ashx";
    private const string AuthorizeUrl = "https://chpp.hattrick.org/oauth/authorize.aspx";
    private const string AccessTokenUrl = "https://chpp.hattrick.org/oauth/access_token.ashx";
    private const string CheckTokenUrl = "https://chpp.hattrick.org/oauth/check_token.ashx";
    private const string InvalidateTokenUrl = "https://chpp.hattrick.org/oauth/invalidate_token.ashx";
    private const string UserAgent = "HattrickAI, v18.0";
    // Hattrick's current CHPP guide requires oauth_callback on request-token.
    // For a native app with no web callback endpoint, use the OAuth 1.0a OOB flow.
    private const string OutOfBandCallback = "oob";

    // Extended permissions are requested on authorize.aspx, not on the signed
    // request-token exchange. Keep this empty until HattrickAI actually needs
    // a write command. This is also safer than HO!'s broader default scopes.
    private readonly string _requestedScopes;

    private readonly HttpClient _httpClient;
    private readonly ChppCredentials _credentials;
    private readonly IChppTokenStore _store;

    public string DiagnosticLogPath => ChppOAuthDiagnostics.LogPath;

    public ChppOAuthClient(ChppCredentials credentials, IChppTokenStore store, HttpClient? httpClient = null, string? requestedScopes = null)
    {
        _credentials = credentials;
        _store = store;
        _requestedScopes = NormalizeScopes(requestedScopes);
        _httpClient = httpClient ?? CreateHttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = false,
            UseCookies = false
        };

        return new HttpClient(handler)
        {
            DefaultRequestVersion = new Version(1, 1),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
    }

    public async Task<string> BeginAuthorizationAsync(string? callbackUrl = null, CancellationToken cancellationToken = default)
    {
        // IMPORTANT: The most reliable CHPP-compatible transport is the legacy
        // PHT-style GET where every OAuth parameter is present in the query
        // string. PHT v3 (still current and widely used for CHPP) builds the
        // request-token URL this way. This also matches the CHPP guide's
        // "Use GET method for all requests" requirement.
        //
        // We intentionally do NOT use ScribeJava's DefaultApi10a POST here:
        // HO! uses that library default, but Hattrick's CHPP endpoint is the
        // authority and explicitly documents GET for CHPP requests.
        var oauth = CreateOAuthParameters(callback: string.IsNullOrWhiteSpace(callbackUrl) ? OutOfBandCallback : callbackUrl, token: null, verifier: null);
        var signing = CreateAuthorizationHeader(
            HttpMethod.Get.Method,
            RequestTokenUrl,
            oauth,
            tokenSecret: null);

        var queryUrl = BuildOAuthQueryUrlExact(RequestTokenUrl, oauth, signing.Signature);

        await ChppOAuthDiagnostics.LogRequestAsync(
            "REQUEST TOKEN (PHT GET QUERY)",
            HttpMethod.Get.Method,
            queryUrl,
            oauth,
            signing.SignatureBaseString,
            signing.Signature,
            _credentials.ConsumerKey,
            _credentials.ConsumerSecret);

        using var request = CreateOAuthRequest(HttpMethod.Get, queryUrl, authorizationHeader: null);
        request.Headers.TryAddWithoutValidation("X-CHPP-OAuth-Transport", "query");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        await ChppOAuthDiagnostics.LogResponseAsync(
            "REQUEST TOKEN (PHT GET QUERY)", response, body, _credentials.ConsumerSecret);

        if (!response.IsSuccessStatusCode)
        {
            // Fresh nonce/timestamp for every retry. A reused OAuth nonce can
            // itself cause a 401, so never retry a signed request verbatim.
            var headerOauth = CreateOAuthParameters(callback: string.IsNullOrWhiteSpace(callbackUrl) ? OutOfBandCallback : callbackUrl, token: null, verifier: null);
            var headerSigning = CreateAuthorizationHeader(
                HttpMethod.Get.Method,
                RequestTokenUrl,
                headerOauth,
                tokenSecret: null);

            await ChppOAuthDiagnostics.LogRequestAsync(
                "REQUEST TOKEN (GET AUTH HEADER FALLBACK)",
                HttpMethod.Get.Method,
                RequestTokenUrl,
                headerOauth,
                headerSigning.SignatureBaseString,
                headerSigning.Signature,
                _credentials.ConsumerKey,
                _credentials.ConsumerSecret);

            using var fallbackRequest = CreateOAuthRequest(HttpMethod.Get, RequestTokenUrl, headerSigning.AuthorizationHeader);
            using var fallbackResponse = await _httpClient.SendAsync(fallbackRequest, cancellationToken);
            var fallbackBody = await fallbackResponse.Content.ReadAsStringAsync(cancellationToken);
            await ChppOAuthDiagnostics.LogResponseAsync(
                "REQUEST TOKEN (GET AUTH HEADER FALLBACK)", fallbackResponse, fallbackBody, _credentials.ConsumerSecret);

            if (!fallbackResponse.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"CHPP request token alınamadı. PHT-GET ve GET-Authorization denemeleri 401 döndürdü.\n\n" +
                    $"İlk yanıt: {body}\n\nİkinci yanıt: {fallbackBody}\n\nOAuth teşhis logu: {ChppOAuthDiagnostics.LogPath}");

            body = fallbackBody;
        }

        var values = ParseFormEncoded(body);
        if (!values.TryGetValue("oauth_token", out var token) ||
            !values.TryGetValue("oauth_token_secret", out var secret) ||
            string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                $"Hattrick CHPP request token yanıtı beklenen formatta değil. Yanıt: {body}");
        }

        await _store.SetAsync(RequestTokenKey, token);
        await _store.SetAsync(RequestTokenSecretKey, secret);

        var authorizeUrl = $"{AuthorizeUrl}?oauth_token={OAuthEncode(token)}";
        if (!string.IsNullOrWhiteSpace(_requestedScopes))
            authorizeUrl += $"&scope={OAuthEncode(_requestedScopes)}";

        return authorizeUrl;
    }

    public async Task<ChppAccessToken> CompleteAuthorizationAsync(
        string verifier,
        CancellationToken cancellationToken = default)
    {
        verifier = verifier.Trim().Replace("#_=_", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(verifier))
            throw new ArgumentException("Onay kodu boş olamaz.", nameof(verifier));

        var requestToken = await _store.GetAsync(RequestTokenKey);
        var requestSecret = await _store.GetAsync(RequestTokenSecretKey);

        if (string.IsNullOrWhiteSpace(requestToken) || string.IsNullOrWhiteSpace(requestSecret))
        {
            throw new InvalidOperationException(
                "CHPP yetkilendirme oturumu bulunamadı. Önce Hattrick'e bağlanmayı başlatın.");
        }

        // PHT's access-token exchange is also a GET query request. oauth_token
        // and oauth_verifier are included in the signed parameter set.
        var oauth = CreateOAuthParameters(callback: null, token: requestToken, verifier: verifier);
        var signing = CreateAuthorizationHeader(
            HttpMethod.Get.Method,
            AccessTokenUrl,
            oauth,
            requestSecret);
        var queryUrl = BuildOAuthQueryUrlExact(AccessTokenUrl, oauth, signing.Signature);

        await ChppOAuthDiagnostics.LogRequestAsync(
            "ACCESS TOKEN (PHT GET QUERY)",
            HttpMethod.Get.Method,
            queryUrl,
            oauth,
            signing.SignatureBaseString,
            signing.Signature,
            _credentials.ConsumerKey,
            _credentials.ConsumerSecret);

        using var request = CreateOAuthRequest(HttpMethod.Get, queryUrl, authorizationHeader: null);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        await ChppOAuthDiagnostics.LogResponseAsync(
            "ACCESS TOKEN (PHT GET QUERY)", response, body, _credentials.ConsumerSecret);
        EnsureSuccess(response, body);

        var values = ParseFormEncoded(body);
        if (!values.TryGetValue("oauth_token", out var token) ||
            !values.TryGetValue("oauth_token_secret", out var secret) ||
            string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                $"Hattrick CHPP access token yanıtı beklenen formatta değil. Yanıt: {body}");
        }

        await _store.SetAsync(AccessTokenKey, token);
        await _store.SetAsync(AccessTokenSecretKey, secret);
        _store.Remove(RequestTokenKey);
        _store.Remove(RequestTokenSecretKey);

        return new ChppAccessToken(token, secret);
    }

    public async Task<ChppAccessToken?> GetStoredAccessTokenAsync()
    {
        var token = await _store.GetAsync(AccessTokenKey);
        var secret = await _store.GetAsync(AccessTokenSecretKey);

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(secret))
            return null;

        return new ChppAccessToken(token, secret);
    }

    /// <summary>
    /// Checks whether the stored CHPP access token is still accepted by Hattrick.
    /// A revoked/invalid token is removed locally so the next connection starts a
    /// clean OAuth flow.
    /// </summary>
    public async Task<bool> ValidateStoredAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var access = await GetStoredAccessTokenAsync();
        if (access == null)
            return false;

        var oauth = CreateOAuthParameters(callback: null, token: access.Token, verifier: null);
        var signing = CreateAuthorizationHeader(
            HttpMethod.Get.Method,
            CheckTokenUrl,
            oauth,
            access.TokenSecret);

        using var request = CreateOAuthRequest(HttpMethod.Get, CheckTokenUrl, signing.AuthorizationHeader);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        await ChppOAuthDiagnostics.LogResponseAsync(
            "CHECK TOKEN", response, body, _credentials.ConsumerSecret);

        if (response.IsSuccessStatusCode)
            return true;

        ClearStoredTokens();
        return false;
    }

    /// <summary>
    /// Revokes the currently stored access token at Hattrick and clears the
    /// local token cache.
    /// </summary>
    public async Task InvalidateStoredAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var access = await GetStoredAccessTokenAsync();
        if (access == null)
            return;

        var oauth = CreateOAuthParameters(callback: null, token: access.Token, verifier: null);
        var signing = CreateAuthorizationHeader(
            HttpMethod.Get.Method,
            InvalidateTokenUrl,
            oauth,
            access.TokenSecret);

        using var request = CreateOAuthRequest(HttpMethod.Get, InvalidateTokenUrl, signing.AuthorizationHeader);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        await ChppOAuthDiagnostics.LogResponseAsync(
            "INVALIDATE TOKEN", response, body, _credentials.ConsumerSecret);

        // Whether Hattrick reports success or the token is already revoked, the
        // local credentials must not be reused.
        ClearStoredTokens();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"CHPP bağlantısı Hattrick tarafında iptal edilirken hata oluştu ({(int)response.StatusCode}): {body}");
    }

    public async Task<string> GetXmlAsync(
        string file,
        IDictionary<string, string?>? query = null,
        CancellationToken cancellationToken = default)
    {
        var access = await GetStoredAccessTokenAsync();
        if (access == null)
            throw new InvalidOperationException("CHPP bağlantısı yapılmamış.");

        var parameters = new List<KeyValuePair<string, string>>
        {
            new("file", file)
        };

        if (query != null)
        {
            foreach (var pair in query)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    parameters.Add(new KeyValuePair<string, string>(pair.Key, pair.Value!));
            }
        }

        var queryString = string.Join("&", parameters.Select(p =>
            $"{OAuthEncode(p.Key)}={OAuthEncode(p.Value)}"));
        var url = "https://chpp.hattrick.org/chppxml.ashx?" + queryString;

        // HO! signs normal CHPP XML requests with OAuth10aService.signRequest() and executes GET.
        var oauth = CreateOAuthParameters(callback: null, token: access.Token, verifier: null);
        var signing = CreateAuthorizationHeader(
            HttpMethod.Get.Method,
            url,
            oauth,
            access.TokenSecret);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", signing.AuthorizationHeader);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept-Language", "en");
        request.Headers.TryAddWithoutValidation("Accept", "image/gif, image/x-xbitmap, image/jpeg, image/pjpeg, */*");
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
        request.Headers.TryAddWithoutValidation("Connection", "keep-alive");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body);
        return body;
    }

    private HttpRequestMessage CreateOAuthRequest(
        HttpMethod method,
        string url,
        string? authorizationHeader)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(authorizationHeader))
            request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept-Language", "en");
        request.Headers.TryAddWithoutValidation("Accept", "application/xml, text/xml, */*");
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
        request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
        return request;
    }

    private static void ClearStoredTokens()
    {
        _store.Remove(AccessTokenKey);
        _store.Remove(AccessTokenSecretKey);
        _store.Remove(RequestTokenKey);
        _store.Remove(RequestTokenSecretKey);
    }

    private Dictionary<string, string> CreateOAuthParameters(
        string? callback,
        string? token,
        string? verifier)
    {
        // Mirrors ScribeJava OAuth10aService's parameter insertion order.
        // The order does not affect the OAuth signature, but keeping it the same
        // makes the generated Authorization header behave like HO!/ScribeJava.
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (callback != null)
            result["oauth_callback"] = callback;

        if (token != null)
            result["oauth_token"] = token;

        if (verifier != null)
            result["oauth_verifier"] = verifier;

        result["oauth_timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        result["oauth_nonce"] = CreateNonce();
        result["oauth_consumer_key"] = _credentials.ConsumerKey;
        result["oauth_signature_method"] = "HMAC-SHA1";
        result["oauth_version"] = "1.0";

        return result;
    }

    private OAuthSigningResult CreateAuthorizationHeader(
        string method,
        string url,
        IReadOnlyDictionary<string, string> oauthParameters,
        string? tokenSecret)
    {
        var uri = new Uri(url);

        // ScribeJava BaseStringExtractorImpl combines query + body + OAuth params,
        // sorts by the RAW key/value pair, URL-encodes each pair, joins with '&',
        // and then OAuth-encodes that complete parameter string again.
        var allParameters = new List<KeyValuePair<string, string>>();
        allParameters.AddRange(ParseQuery(uri.Query));
        allParameters.AddRange(oauthParameters.Select(p =>
            new KeyValuePair<string, string>(p.Key, p.Value)));

        var normalized = string.Join("&", allParameters
            .OrderBy(p => OAuthEncode(p.Key), StringComparer.Ordinal)
            .ThenBy(p => OAuthEncode(p.Value), StringComparer.Ordinal)
            .Select(p => $"{OAuthEncode(p.Key)}={OAuthEncode(p.Value)}"));

        // ScribeJava uses OAuthRequest.getSanitizedUrl(): remove query and default port.
        var baseUrl = GetSanitizedUrl(uri);
        var signatureBase =
            $"{method.ToUpperInvariant()}&{OAuthEncode(baseUrl)}&{OAuthEncode(normalized)}";

        // ScribeJava HMACSha1SignatureService: encode both secrets, join with '&'.
        var signingKey =
            $"{OAuthEncode(_credentials.ConsumerSecret)}&{OAuthEncode(tokenSecret ?? string.Empty)}";

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(signingKey));
        var signature = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureBase)));

        // ScribeJava HeaderExtractorImpl iterates the OAuth parameter map in
        // insertion order and OAuth-encodes VALUES only. The signature is the
        // final OAuth parameter because ScribeJava appends it after signing.
        var header = new StringBuilder("OAuth");
        foreach (var pair in oauthParameters)
        {
            header.Append(header.Length > 5 ? "," : " ");
            header.Append(pair.Key);
            header.Append("=\"");
            header.Append(OAuthEncode(pair.Value));
            header.Append('"');
        }

        header.Append(",oauth_signature=\"");
        header.Append(OAuthEncode(signature));
        header.Append('"');

        return new OAuthSigningResult(
            header.ToString(),
            signatureBase,
            signature,
            normalized);
    }

    private sealed record OAuthSigningResult(
        string AuthorizationHeader,
        string SignatureBaseString,
        string Signature,
        string NormalizedParameters);

    private static string BuildOAuthQueryUrlExact(
        string url,
        IReadOnlyDictionary<string, string> oauthParameters,
        string signature)
    {
        // This intentionally mirrors PHT's buildOauthUrl(): parameter names
        // remain raw and parameter VALUES are RFC3986 encoded once for the URL.
        // The signature itself is the OAuth-encoded Base64 value.
        var pairs = oauthParameters
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={OAuthEncode(p.Value)}")
            .ToList();

        pairs.Add($"oauth_signature={OAuthEncode(signature)}");
        return url + "?" + string.Join("&", pairs);
    }

    private static string BuildOAuthQueryUrl(string url, OAuthSigningResult signing)
    {
        // For the fallback transport, put the complete OAuth parameter set in
        // the query string. The signature itself is already calculated over the
        // same normalized parameter set, so the server sees exactly what was
        // signed.
        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return url + separator + signing.NormalizedParameters + "&oauth_signature=" + OAuthEncode(signing.Signature);
    }

    private static string GetSanitizedUrl(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        };

        if ((string.Equals(builder.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
             builder.Port == 443) ||
            (string.Equals(builder.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             builder.Port == 80))
        {
            builder.Port = -1;
        }

        return builder.Uri.GetLeftPart(UriPartial.Path);
    }

    private static string NormalizeScopes(string? scopes)
    {
        if (string.IsNullOrWhiteSpace(scopes))
            return string.Empty;

        // Accept either a comma-separated list or whitespace-separated input,
        // then emit the CHPP-required comma-separated form.
        var normalized = scopes
            .Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return string.Join(',', normalized);
    }

    private static string CreateNonce()
    {
        // ScribeJava's nonce is timestamp-in-seconds + a random integer.
        // Any sufficiently random ASCII nonce is valid, so we preserve the same shape.
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $"{timestamp}{RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue)}";
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            yield break;

        var text = query.StartsWith('?') ? query[1..] : query;
        foreach (var item in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split('=', 2);
            var key = WebUtility.UrlDecode(parts[0]) ?? string.Empty;
            var value = parts.Length == 2
                ? WebUtility.UrlDecode(parts[1]) ?? string.Empty
                : string.Empty;
            yield return new KeyValuePair<string, string>(key, value);
        }
    }

    private static Dictionary<string, string> ParseFormEncoded(string body)
    {
        return ParseQuery(body).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Equivalent to ScribeJava OAuthEncoder.encode(): UTF-8 form encoding with
    /// '+'=>%20, %7E=>~, and *=>%2A fixes, i.e. OAuth RFC 3986 encoding.
    /// </summary>
    private static string OAuthEncode(string value)
    {
        var encoded = Uri.EscapeDataString(value);
        return encoded.Replace("*", "%2A", StringComparison.Ordinal)
            .Replace("%7E", "~", StringComparison.OrdinalIgnoreCase)
            .Replace("+", "%20", StringComparison.Ordinal);
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body;
        throw new HttpRequestException(
            $"CHPP isteği başarısız ({(int)response.StatusCode}): {detail}\n\nOAuth teşhis logu: {ChppOAuthDiagnostics.LogPath}");
    }
}
