using AirbnbBooking;
using AirbnbBooking.Data;
using AirbnbBooking.Hubs;
using AirbnbBooking.Middleware;
using AirbnbBooking.Models;
using AirbnbBooking.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// File-based SQL Server Express .mdf inside App_Data
string appDataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
if (!Directory.Exists(appDataPath))
{
    Directory.CreateDirectory(appDataPath);
}
AppDomain.CurrentDomain.SetData("DataDirectory", appDataPath);

string rawConn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\\AirbnbBookingDb.mdf;Database=AirbnbBookingDb;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True";
string resolvedConn = rawConn.Replace("|DataDirectory|", appDataPath);

// Brotli + Gzip HTTP Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["image/svg+xml"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// MVC & SignalR
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Module 8: EF Core Optimized Connection Pooling
builder.Services.AddDbContextPool<DB>(options =>
    options.UseSqlServer(
        resolvedConn,
        sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

// Core Utilities & Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<RateLimitTelemetry>();
builder.Services.AddScoped<Helper>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<PricingService>();

// Module 1: Manual Cookie-Based Authentication (Strictly NO ASP.NET Core Identity Framework)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "StayNest.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Apply EF Core Code-First Migrations & Seed Sample Data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DB>();
    var helper = scope.ServiceProvider.GetRequiredService<Helper>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        db.Database.Migrate();
        DbSeeder.Seed(db, helper);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration/seeding error. Falling back to EnsureCreated if migrations are not yet added.");
        try
        {
            db.Database.EnsureCreated();
            DbSeeder.Seed(db, helper);
        }
        catch (Exception inner)
        {
            logger.LogError(inner, "Unable to initialize SQL Server Express database.");
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        bool versioned = ctx.Context.Request.Query.ContainsKey("v");
        ctx.Context.Response.Headers.CacheControl = versioned
            ? "public,max-age=31536000,immutable"
            : "public,max-age=86400";
    }
});
app.UseRouting();

// Module 8: Sliding-Window Rate Limiting & Brute-Force Protection Middleware
app.UseMiddleware<RateLimitMiddleware>();

app.UseSession();
app.UseAuthentication();

// Automatic Rotating Persistent Session Token Renewal when cookie expired
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated != true &&
        context.Request.Cookies.ContainsKey("StayNest_RefreshToken"))
    {
        var db = context.RequestServices.GetRequiredService<DB>();
        var helper = context.RequestServices.GetRequiredService<Helper>();
        await helper.RotateSessionTokenIfNeededAsync(db);
    }
    await next();
});

app.UseAuthorization();

app.MapHub<StayHub>("/hubs/stay");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Property}/{action=Catalog}/{id?}");

app.Run();
