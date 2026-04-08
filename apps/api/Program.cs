using AuraPrintsApi.Data;
using AuraPrintsApi.Endpoints;
using AuraPrintsApi.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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
builder.Services.AddSingleton<ITaskTagRepository, TaskTagRepository>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => {
        o.Cookie.Name = "bizhub_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
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
    opt.RejectionStatusCode = 429;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

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
   .MapProjectEndpoints();

app.Run();
