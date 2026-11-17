using System.Text;
using System.Threading.RateLimiting;
using EDOC.Api.Data;
using EDOC.Api.Models;
using EDOC.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var isDirectoryImport = args.Contains("--import-cambodia-directory", StringComparer.OrdinalIgnoreCase);
var jwtKey = builder.Configuration["Jwt:Key"];
if (isDirectoryImport && (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32))
    jwtKey = "E-DOC-directory-import-only-temporary-signing-key";
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("JWT configuration is missing. Set Jwt:Key with at least 32 characters using user-secrets or an environment variable.");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<HealthcareDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Healthcare")));
builder.Services.AddIdentityCore<UserAccount>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddRoles<UserRole>()
.AddEntityFrameworkStores<HealthcareDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "EDOC.Api",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "EDOC.Client",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var subject = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                if (!Guid.TryParse(subject, out var userId)) { context.Fail("Invalid account token."); return; }
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<UserAccount>>();
                var account = await userManager.FindByIdAsync(userId.ToString());
                if (account is null || !account.IsActive || (account.RequireEmailVerification && !account.EmailConfirmed))
                { context.Fail("Account is inactive."); return; }
                if (await userManager.IsInRoleAsync(account, "Doctor"))
                {
                    var database = context.HttpContext.RequestServices.GetRequiredService<HealthcareDbContext>();
                    if (!await database.Doctors.AnyAsync(x => x.UserId == userId && x.IsApproved, context.HttpContext.RequestAborted))
                        context.Fail("Doctor approval is pending.");
                }
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<IAccountEmailSender, SmtpAccountEmailSender>();
builder.Services.AddSingleton<IPrivateFileStore, LocalPrivateFileStore>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 12,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
if (args.Contains("--bootstrap-admin", StringComparer.OrdinalIgnoreCase))
{
    var email = app.Configuration["BootstrapAdmin:Email"];
    var password = app.Configuration["BootstrapAdmin:Password"];
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        throw new InvalidOperationException("Set BootstrapAdmin:Email and BootstrapAdmin:Password in user-secrets before bootstrapping an administrator.");
    await using var scope = app.Services.CreateAsyncScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();
    var existingAdmin = await userManager.GetUsersInRoleAsync("Administrator");
    if (existingAdmin.Count > 0) throw new InvalidOperationException("An administrator already exists; bootstrap is one-time only.");
    var user = await userManager.FindByEmailAsync(email);
    if (user is null)
    {
        user = new UserAccount { UserName = email, Email = email, FullName = "E-DOC Administrator", EmailConfirmed = true };
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(error => error.Description)));
    }
    var assigned = await userManager.AddToRoleAsync(user, "Administrator");
    if (!assigned.Succeeded) throw new InvalidOperationException(string.Join("; ", assigned.Errors.Select(error => error.Description)));
    Console.WriteLine("Administrator role bootstrapped. Remove BootstrapAdmin secrets after this one-time command.");
    return;
}
if (args.Contains("--import-cambodia-directory", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var importer = new CambodiaDirectoryImporter(scope.ServiceProvider.GetRequiredService<HealthcareDbContext>());
    var result = await importer.ImportAsync();
    Console.WriteLine($"Cambodia directory import complete: {result.Provinces} provinces, {result.Districts} districts, {result.Communes} communes, {result.FacilitySourceRows} sourced hospital rows ({result.HospitalsInserted} inserted), {result.Specialties} specialty seeds, {result.Doctors} external doctors.");
    return;
}
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "EDOC.Api" })).AllowAnonymous();
app.Run();

public partial class Program;
