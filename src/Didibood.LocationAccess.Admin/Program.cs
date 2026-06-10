using Didibood.LocationAccess.Application;
using Didibood.LocationAccess.Infrastructure;
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

    builder.Services.AddRazorPages();
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
