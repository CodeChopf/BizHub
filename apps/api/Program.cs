using Anthropic.SDK;
using AuraPrintsApi.Data;
using AuraPrintsApi.Endpoints;
using AuraPrintsApi.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var dataDir = Environment.GetEnvironmentVariable("BIZHUB_DATA_DIR")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BizHub", "Data");
Directory.CreateDirectory(dataDir);
var dbFile = Path.Combine(dataDir, "auraprints.db");
var dbContext = new DatabaseContext(dbFile);

builder.Services.AddSingleton(dbContext);
builder.Services.AddSingleton<IRoadmapRepository, RoadmapRepository>();
builder.Services.AddSingleton<IProductRepository, ProductRepository>();
builder.Services.AddSingleton<IStateRepository, StateRepository>();
builder.Services.AddSingleton<ICategoryRepository, CategoryRepository>();
builder.Services.AddSingleton<IExpenseRepository, ExpenseRepository>();
builder.Services.AddSingleton<IAdminRepository, AdminRepository>();
builder.Services.AddSingleton<IAttachmentRepository, AttachmentRepository>();
builder.Services.AddSingleton<IMilestoneRepository, MilestoneRepository>();
builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
builder.Services.AddSingleton<IProductCatalogRepository, ProductCatalogRepository>();
builder.Services.AddSingleton<IProductionRepository, ProductionRepository>();
builder.Services.AddSingleton<ICalendarRepository, CalendarRepository>();
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IProjectRepository, ProjectRepository>();
builder.Services.AddSingleton<IInviteRepository, InviteRepository>();
builder.Services.AddSingleton<IAgentRepository, AgentRepository>();
builder.Services.AddSingleton<IActivityRepository, ActivityRepository>();
builder.Services.AddSingleton(new AnthropicClient(
    Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? ""));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => {
        o.Cookie.Name = "bizhub_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromHours(24);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });

builder.Services.AddAuthorization(options => {
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRateLimiter(opt => {
    opt.AddFixedWindowLimiter("login", o => {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
    opt.AddFixedWindowLimiter("register", o => {
        o.PermitLimit = 3;
        o.Window = TimeSpan.FromMinutes(5);
        o.QueueLimit = 0;
    });
    opt.AddFixedWindowLimiter("writes", o => {
        o.PermitLimit = 120;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
    opt.RejectionStatusCode = 429;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (ctx, next) =>
{
    if (HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method) || HttpMethods.IsOptions(ctx.Request.Method))
    {
        await next();
        return;
    }

    if (!ctx.User.Identity?.IsAuthenticated ?? true)
    {
        await next();
        return;
    }

    var origin = ctx.Request.Headers.Origin.ToString();
    if (!string.IsNullOrWhiteSpace(origin))
    {
        var expected = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
        if (!origin.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "CSRF check failed." });
            return;
        }
    }

    await next();
});

var seeder = new DatabaseSeeder(dbContext);
dbContext.Initialize();
seeder.Seed();

// Bootstrap: migrate single-password or create first admin user
{
    var settingsRepo = app.Services.GetRequiredService<ISettingsRepository>();
    var userRepo = app.Services.GetRequiredService<IUserRepository>();
    if (!userRepo.HasAnyUser())
    {
        var oldHash = settingsRepo.GetPasswordHash();
        if (oldHash != null)
        {
            userRepo.CreateWithHash("admin", oldHash, isAdmin: true);
            settingsRepo.DeletePasswordHash();
        }
        else
        {
            var envPw = Environment.GetEnvironmentVariable("BIZHUB_PASSWORD");
            if (envPw != null)
                userRepo.Create("admin", envPw, isAdmin: true);
        }
    }
}

app.MapAuthEndpoints()
   .MapUserEndpoints()
   .MapRoadmapEndpoints()
   .MapFinanceEndpoints()
   .MapSettingsEndpoints()
   .MapCatalogEndpoints()
   .MapProductionEndpoints()
   .MapCalendarEndpoints()
   .MapProjectEndpoints()
   .MapAgentEndpoints()
   .MapActivityEndpoints();

app.MapGet("/health", () => Results.Ok(new { ok = true, utc = DateTime.UtcNow }));

app.Run();
