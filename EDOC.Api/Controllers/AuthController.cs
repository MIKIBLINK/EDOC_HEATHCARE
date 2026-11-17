using System.Security.Cryptography;
using System.Text;
using EDOC.Api.Data;
using EDOC.Api.DTOs;
using EDOC.Api.Models;
using EDOC.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace EDOC.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("authentication")]
public sealed class AuthController(
    UserManager<UserAccount> users,
    SignInManager<UserAccount> signInManager,
    HealthcareDbContext database,
    TokenService tokens,
    IAccountEmailSender emailSender,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            return BadRequest(new { message = "Password confirmation does not match." });
        if (request.AccountType.Equals("Doctor", StringComparison.OrdinalIgnoreCase)
            && (!request.SpecialtyId.HasValue || !await database.Specialties.AnyAsync(x => x.Id == request.SpecialtyId, cancellationToken)))
            return BadRequest(new { message = "Doctor registration requires a valid specialty." });

        var email = request.Email.Trim();
        var phone = LoginIdentifierParser.NormalizePhoneNumber(request.PhoneNumber);
        if (phone is null)
            return BadRequest(new { message = "Enter a valid phone number, such as 096123456." });
        if (await users.FindByEmailAsync(email) is not null || await database.Users.AnyAsync(x => x.PhoneNumber == phone, cancellationToken))
            return Conflict(new { message = "An account with the supplied email or phone number already exists." });

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var isDoctor = request.AccountType.Equals("Doctor", StringComparison.OrdinalIgnoreCase);
        var user = new UserAccount
        {
            UserName = email,
            Email = email,
            PhoneNumber = phone,
            FullName = request.FullName.Trim(),
            IsActive = true,
            LockoutEnabled = true,
            // New accounts can sign in immediately. Email remains a contact/login
            // identifier, but registration does not depend on outbound email.
            EmailConfirmed = true,
            RequireEmailVerification = false
        };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(x => x.Code is "DuplicateEmail" or "DuplicateUserName"))
                return Conflict(new { message = "An account with the supplied email or phone number already exists." });
            return BadRequest(new { errors = created.Errors.Select(x => new { x.Code, x.Description }) });
        }

        var isCaregiver = request.AccountType.Equals("Caregiver", StringComparison.OrdinalIgnoreCase);
        var roleName = isDoctor ? "Doctor" : isCaregiver ? "Caregiver" : "Patient";
        var roleResult = await users.AddToRoleAsync(user, roleName);
        if (!roleResult.Succeeded)
            return Problem("Unable to complete account registration.", statusCode: StatusCodes.Status500InternalServerError);

        if (isDoctor)
        {
            database.Doctors.Add(new Doctor
            {
                UserId = user.Id,
                SpecialtyId = request.SpecialtyId!.Value,
                Qualifications = request.Qualifications?.Trim(),
                IsApproved = false
            });
        }
        else if (!isCaregiver) database.Patients.Add(new Patient { UserId = user.Id });

        database.AccountSecurityLogs.Add(SecurityEvent(user.Id, "registration", true));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new { message = isDoctor
            ? "Registration complete. You can sign in now; doctor access remains pending administrative approval."
            : isCaregiver ? "Registration complete. You can sign in now; accept a patient invitation to access shared care information."
            : "Registration complete. You can sign in now." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var identifier = request.LoginIdentifier.Trim();
        if (string.IsNullOrWhiteSpace(identifier))
            return BadRequest(new { message = "An email address or phone number is required." });

        UserAccount? user;
        if (identifier.Contains('@'))
            user = await users.FindByEmailAsync(identifier);
        else
        {
            var phone = LoginIdentifierParser.NormalizePhoneNumber(identifier);
            user = phone is null
                ? null
                : await users.Users.SingleOrDefaultAsync(account => account.PhoneNumber == phone, cancellationToken);
        }
        if (user is null)
        {
            await WriteSecurityEventAsync(null, "login", false, cancellationToken);
            return Unauthorized(new { message = "The email/phone number or password is incorrect, or the account is unavailable." });
        }

        var check = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        var eligibleDoctor = !await users.IsInRoleAsync(user, "Doctor")
            || await database.Doctors.AnyAsync(x => x.UserId == user.Id && x.IsApproved, cancellationToken);
        if (!check.Succeeded || !user.IsActive || (user.RequireEmailVerification && !user.EmailConfirmed) || !eligibleDoctor)
        {
            await WriteSecurityEventAsync(user.Id, check.IsLockedOut ? "login_locked" : "login", false, cancellationToken);
            return Unauthorized(new { message = "The email/phone number or password is incorrect, or the account is unavailable." });
        }

        await WriteSecurityEventAsync(user.Id, "login", true, cancellationToken);
        return Ok(await tokens.IssueAsync(user, cancellationToken));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var replacement = await tokens.RotateAsync(request.RefreshToken, cancellationToken);
        return replacement is null ? Unauthorized(new { message = "The refresh token is invalid or expired." }) : Ok(replacement);
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        var idText = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (Guid.TryParse(idText, out var userId))
        {
            await tokens.RevokeAsync(userId, request.RefreshToken, cancellationToken);
            await WriteSecurityEventAsync(userId, "logout", true, cancellationToken);
        }
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!emailSender.IsConfigured)
            return Problem("Account email delivery is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);

        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null && user.IsActive)
        {
            var rawToken = CreateRawToken();
            database.AccountSecurityTokens.Add(new AccountSecurityToken
            {
                UserId = user.Id,
                Purpose = AccountSecurityTokenPurpose.PasswordReset,
                TokenHash = Hash(rawToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
            });
            await database.SaveChangesAsync(cancellationToken);
            try
            {
                await emailSender.SendAsync(user.Email!, "Reset your E-DOC password",
                    $"Use this password reset token: {rawToken}\nIt expires in 30 minutes.", cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Password reset email delivery failed.");
            }
            await WriteSecurityEventAsync(user.Id, "password_reset_requested", true, cancellationToken);
        }
        return Accepted(new { message = "If the account is eligible, password reset instructions will be sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            return BadRequest(new { message = "Password confirmation does not match." });
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) return BadRequest(new { message = "The reset token is invalid or expired." });
        var token = await FindValidTokenAsync(user.Id, AccountSecurityTokenPurpose.PasswordReset, request.Token, cancellationToken);
        if (token is null) return BadRequest(new { message = "The reset token is invalid or expired." });

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        token.ConsumedAt = DateTimeOffset.UtcNow;
        var changed = await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), request.Password);
        if (!changed.Succeeded)
            return BadRequest(new { errors = changed.Errors.Select(x => new { x.Code, x.Description }) });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await WriteSecurityEventAsync(user.Id, "password_reset", true, cancellationToken);
        return NoContent();
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var token = await FindValidTokenAsync(request.UserId, AccountSecurityTokenPurpose.EmailVerification, request.Token, cancellationToken);
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (token is null || user is null) return BadRequest(new { message = "The verification token is invalid or expired." });
        token.ConsumedAt = DateTimeOffset.UtcNow;
        user.EmailConfirmed = true;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return Problem("Unable to verify the account.", statusCode: StatusCodes.Status500InternalServerError);
        await database.SaveChangesAsync(cancellationToken);
        await WriteSecurityEventAsync(user.Id, "email_verified", true, cancellationToken);
        return NoContent();
    }

    private async Task<AccountSecurityToken?> FindValidTokenAsync(Guid userId, AccountSecurityTokenPurpose purpose, string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        return await database.AccountSecurityTokens.SingleOrDefaultAsync(x => x.UserId == userId && x.Purpose == purpose
            && x.TokenHash == hash && x.ConsumedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
    }

    private async Task WriteSecurityEventAsync(Guid? userId, string eventType, bool succeeded, CancellationToken cancellationToken)
    {
        database.AccountSecurityLogs.Add(SecurityEvent(userId, eventType, succeeded));
        await database.SaveChangesAsync(cancellationToken);
    }

    private AccountSecurityLog SecurityEvent(Guid? userId, string eventType, bool succeeded) => new()
    {
        UserId = userId,
        EventType = eventType,
        Succeeded = succeeded,
        ClientAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
    };

    private static string CreateRawToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
