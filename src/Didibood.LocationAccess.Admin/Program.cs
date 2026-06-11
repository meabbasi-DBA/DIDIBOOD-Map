using Didibood.LocationAccess.Admin.Services;
using Didibood.LocationAccess.Application;
using Didibood.LocationAccess.Application.Configuration;
using Didibood.LocationAccess.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.Configure<AdminAuthOptions>(
        builder.Configuration.GetSection(AdminAuthOptions.SectionName));
    builder.Services.AddSingleton<AdminAuthService>();

    var adminAuth = builder.Configuration
        .GetSection(AdminAuthOptions.SectionName)
        .Get<AdminAuthOptions>() ?? new AdminAuthOptions();
    var adminAuthEnabled = !string.IsNullOrWhiteSpace(adminAuth.Username)
        && !string.IsNullOrWhiteSpace(adminAuth.Password);

    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/Login";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });
    builder.Services.AddAuthorization();

    builder.Services.AddRazorPages(options =>
    {
        if (adminAuthEnabled)
        {
            options.Conventions.AuthorizeFolder("/");
            options.Conventions.AllowAnonymousToPage("/Account/Login");
            options.Conventions.AllowAnonymousToPage("/Account/Logout");
        }
    });
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration, runStartupValidation: false);

    builder.Services.AddHttpClient("api", client =>
    {
        var baseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5001";
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/');
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapRazorPages();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Admin host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
