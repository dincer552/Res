using HattrickAI.CHPP;
using Microsoft.AspNetCore.Http;

namespace HattrickAI.V3;

public sealed class ChppSessionTokenStore : IChppTokenStore
{
    private readonly IHttpContextAccessor _accessor;
    public ChppSessionTokenStore(IHttpContextAccessor accessor) => _accessor = accessor;

    private ISession Session => _accessor.HttpContext?.Session
        ?? throw new InvalidOperationException("HTTP oturumu bulunamadı.");

    public Task SetAsync(string key, string value)
    {
        Session.SetString(key, value);
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key) => Task.FromResult(Session.GetString(key));

    public void Remove(string key) => Session.Remove(key);
}
