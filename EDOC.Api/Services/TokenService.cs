using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EDOC.Api.Data;
using EDOC.Api.DTOs;
using EDOC.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace EDOC.Api.Services;

public sealed class TokenService(
    IConfiguration configuration,
    UserManager<UserAccount> users,
    HealthcareDbContext database)
{
    private readonly string _key = configuration["Jwt:Key"] is { Length: >= 32 } key
        ? key
        : throw new InvalidOperationException("Configure Jwt:Key with at least 32 characters using user-secrets or an environment variable.");

    public async Task<TokenResponse> IssueAsync(UserAccount user, CancellationToken cancellationToken = default)
    {
        var roles = await users.GetRolesAsync(user);
        var now = DateTimeOffset.UtcNow;
        var accessExpiry = now.AddMinutes(15);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(configuration["Jwt:Issuer"] ?? "EDOC.Api", configuration["Jwt:Audience"] ?? "EDOC.Client", claims, now.UtcDateTime, accessExpiry.UtcDateTime, signingCredentials);

        var rawRefresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshExpiry = now.AddDays(14);
        database.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(rawRefresh),
            ExpiresAt = refreshExpiry
        });
        await database.SaveChangesAsync(cancellationToken);
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt), accessExpiry, rawRefresh, refreshExpiry, user.FullName, roles.ToArray());
    }

    public async Task<TokenResponse?> RotateAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var hash = Hash(rawToken);
            var stored = await database.RefreshTokens.Include(x => x.User)
                .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            if (stored?.User is null) return null;
            if (stored.RevokedAt is not null)
            {
                // A previously rotated token is being reused. Revoke the remaining session tokens.
                await database.RefreshTokens.Where(x => x.UserId == stored.UserId && x.RevokedAt == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, now), cancellationToken);
                database.AccountSecurityLogs.Add(new AccountSecurityLog
                {
                    UserId = stored.UserId,
                    EventType = "refresh_token_reuse",
                    Succeeded = false
                });
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }
            if (stored.ExpiresAt <= now || !stored.User.IsActive) return null;
            stored.RevokedAt = now;
            await database.SaveChangesAsync(cancellationToken); // RowVersion rejects concurrent refresh attempts.
            var result = await IssueAsync(stored.User, cancellationToken);
            var replacementHash = Hash(result.RefreshToken);
            stored.ReplacedByTokenId = await database.RefreshTokens.Where(x => x.TokenHash == replacementHash).Select(x => (Guid?)x.Id).SingleAsync(cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
    }

    public async Task RevokeAsync(Guid userId, string rawToken, CancellationToken cancellationToken = default)
    {
        var entry = await database.RefreshTokens.SingleOrDefaultAsync(x => x.UserId == userId && x.TokenHash == Hash(rawToken), cancellationToken);
        if (entry is null || entry.RevokedAt is not null) return;
        entry.RevokedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
