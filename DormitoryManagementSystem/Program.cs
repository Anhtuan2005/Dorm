using System.Globalization;
using DormitoryManagementSystem.Data;
using DormitoryManagementSystem.Hubs;
using DormitoryManagementSystem.Models;
using DormitoryManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var viCulture = CultureInfo.GetCultureInfo("vi-VN");
CultureInfo.DefaultThreadCurrentCulture = viCulture;
CultureInfo.DefaultThreadCurrentUICulture = viCulture;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection.");

builder.Services.AddDbContext<DormitoryDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<DormitoryDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".NepKtx.Auth";
    options.Cookie.HttpOnly = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<IRoomMapService, RoomMapService>();
builder.Services.AddScoped<IOperationsCatalogService, OperationsCatalogService>();
builder.Services.AddScoped<IStudentPortalService, StudentPortalService>();
builder.Services.AddScoped<IApplicationManagementService, ApplicationManagementService>();
builder.Services.AddScoped<IStudentManagementService, StudentManagementService>();
builder.Services.AddScoped<IBillingLedgerService, BillingLedgerService>();
builder.Services.AddScoped<IMaintenanceManagementService, MaintenanceManagementService>();
builder.Services.AddScoped<IMaintenanceAttachmentStorage, MaintenanceAttachmentStorage>();
builder.Services.AddScoped<IOperationsReportingService, OperationsReportingService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(viCulture),
    SupportedCultures = [viCulture],
    SupportedUICultures = [viCulture]
});
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (DormitoryDbContext db, CancellationToken cancellationToken) =>
{
    var databaseReady = await db.Database.CanConnectAsync(cancellationToken);
    return databaseReady
        ? Results.Ok(new { status = "Healthy", database = "Connected" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<OperationsHub>("/hubs/operations");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DormitoryDbContext>();
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("DemoData:Seed"))
        await DataSeeder.SeedAsync(scope.ServiceProvider);
}

try
{
    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Ứng dụng dừng bất thường.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
