using Aegis.Adapters.Alpaca.Services;
using Aegis.Backend.Auth;
using Aegis.Backend.Endpoints;
using Aegis.Shared.Ports.MarketData;
using Aegis.Universe.Application;
using Aegis.Universe.Application.Abstractions;
using Aegis.Universe.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "aegis.auth";
        options.LoginPath = "/login";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    var allowedOrigins = configuredOrigins.Length > 0
        ? configuredOrigins
        : ["http://localhost:3000", "http://localhost:3001"];

    options.AddPolicy("web", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddDbContext<UniverseDbContext>(options =>
{
    var useInMemory = builder.Configuration.GetValue<bool>("Universe:UseInMemory");
    if (useInMemory)
    {
        var databaseName = builder.Configuration["Universe:InMemoryDatabaseName"] ?? "aegis-universe";
        options.UseInMemoryDatabase(databaseName);
    }
    else
    {
        var connectionString = builder.Configuration.GetConnectionString("universe")
                               ?? builder.Configuration.GetConnectionString("Universe")
                               ?? builder.Configuration.GetConnectionString("DefaultConnection")
                               ?? "Host=localhost;Port=5432;Database=aegis;Username=postgres;Password=postgres";

        options.UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime());
    }
});

builder.Services.AddScoped<UniverseService>();
builder.Services.AddScoped<ISymbolReferenceProvider, FakeSymbolReferenceProvider>();
builder.Services.AddScoped<IExecutionRemovalGuardService, FakeExecutionRemovalGuardService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UniverseDbContext>();
    await UniverseDbInitializer.EnsureInitializedAsync(dbContext, CancellationToken.None);
}

app.MapAuthEndpoints();
app.MapUniverseEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
