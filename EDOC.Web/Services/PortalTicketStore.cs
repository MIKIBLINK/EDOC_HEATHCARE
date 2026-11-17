using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;

namespace EDOC.Web.Services;

public sealed class PortalTicketStore : ITicketStore
{
    private readonly ConcurrentDictionary<string, AuthenticationTicket> _tickets = new(StringComparer.Ordinal);
    private long _lastCleanupTicks = DateTimeOffset.UtcNow.UtcTicks;

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        CleanupExpired();
        var key = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        _tickets[key] = ticket;
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        _tickets[key] = ticket;
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (!_tickets.TryGetValue(key, out var ticket)) return Task.FromResult<AuthenticationTicket?>(null);
        if (ticket.Properties.ExpiresUtc is { } expiry && expiry <= DateTimeOffset.UtcNow)
        {
            _tickets.TryRemove(key, out _);
            return Task.FromResult<AuthenticationTicket?>(null);
        }
        return Task.FromResult<AuthenticationTicket?>(ticket);
    }

    public Task RemoveAsync(string key)
    {
        _tickets.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var previous = Interlocked.Read(ref _lastCleanupTicks);
        if (now.UtcTicks - previous < TimeSpan.FromMinutes(5).Ticks
            || Interlocked.CompareExchange(ref _lastCleanupTicks, now.UtcTicks, previous) != previous) return;
        foreach (var pair in _tickets)
            if (pair.Value.Properties.ExpiresUtc is { } expiry && expiry <= now)
                _tickets.TryRemove(pair.Key, out _);
    }
}
