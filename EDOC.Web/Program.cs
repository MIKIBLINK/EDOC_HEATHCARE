using EDOC.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "Administrator");
    options.Conventions.AuthorizeFolder("/HospitalAdmin", "HospitalAdmin");
    options.Conventions.AuthorizeFolder("/Hospital", "HospitalStaff");
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("EDOC.Api", client =>
{
    var address = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5155/";
    if (!Uri.TryCreate(address, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https"))
        throw new InvalidOperationException("Configure Api:BaseUrl with a valid HTTP or HTTPS URL.");
    client.BaseAddress = baseUri;
    var timeoutSeconds = builder.Configuration.GetValue("Api:TimeoutSeconds", 60);
    if (timeoutSeconds <= 0) throw new InvalidOperationException("Configure Api:TimeoutSeconds with a positive value.");
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
builder.Services.AddScoped<ApiPortalClient>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = ".EDOC.Portal";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
    options.SlidingExpiration = true;
});
builder.Services.AddSingleton<PortalTicketStore>();
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<PortalTicketStore>((options, store) => options.SessionStore = store);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Administrator", policy => policy.RequireRole("Administrator", "SuperAdmin"));
    options.AddPolicy("HospitalAdmin", policy => policy.RequireRole("HospitalAdmin"));
    options.AddPolicy("HospitalStaff", policy => policy.RequireRole("HospitalStaff"));
});

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapGet("/", (HttpContext context) => Results.Redirect(context.User.Identity?.IsAuthenticated == true
    ? context.User.IsInRole("Administrator") || context.User.IsInRole("SuperAdmin") ? "/admin" : context.User.IsInRole("HospitalAdmin") ? "/hospital-admin" : "/hospital"
    : "/login")).AllowAnonymous();
app.Run();

public partial class Program;
