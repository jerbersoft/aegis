using Aegis.Adapters.Alpaca.Configuration;
using Aegis.Adapters.Alpaca.Services;
using Aegis.Backend.Auth;
using Aegis.Backend.Endpoints;
using Aegis.Backend.MarketData;
using Aegis.MarketData.Application;
using Aegis.MarketData.Application.Abstractions;
using Aegis.MarketData.Infrastructure;
using Aegis.Shared.Ports.MarketData;
using Aegis.Universe.Application;
using Aegis.Universe.Application.Abstractions;
using Aegis.Universe.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Aegis.Shared.Serialization;
using NodaTime;

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
builder.Services.ConfigureHttpJsonOptions(options => AegisJson.Configure(options.SerializerOptions));
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
    var connectionString = builder.Configuration.GetConnectionString("universe")
                           ?? builder.Configuration.GetConnectionString("Universe")
                           ?? builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? "Host=localhost;Port=5432;Database=aegis;Username=postgres;Password=postgres";

    options.UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime());
});

builder.Services.AddDbContext<MarketDataDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("marketdata")
                           ?? builder.Configuration.GetConnectionString("MarketData")
                           ?? builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? "Host=localhost;Port=5432;Database=aegis;Username=postgres;Password=postgres";

    options.UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime());
});

var alpacaSymbolReferenceOptions = builder.Configuration.GetSection(AlpacaSymbolReferenceOptions.SectionName).Get<AlpacaSymbolReferenceOptions>()
                                   ?? new AlpacaSymbolReferenceOptions();
var alpacaHistoricalDataOptions = builder.Configuration.GetSection(AlpacaHistoricalDataOptions.SectionName).Get<AlpacaHistoricalDataOptions>()
                                 ?? new AlpacaHistoricalDataOptions();

var useFakeHistoricalProvider = builder.Configuration.GetValue<bool>("Alpaca:HistoricalData:UseFakeProvider");

if (string.IsNullOrWhiteSpace(alpacaHistoricalDataOptions.ApiKey) || string.IsNullOrWhiteSpace(alpacaHistoricalDataOptions.ApiSecret))
{
    // Local/dev historical reads can reuse symbol-reference credentials when a separate historical key pair is not configured.
    alpacaHistoricalDataOptions = new AlpacaHistoricalDataOptions
    {
        BaseUrl = alpacaHistoricalDataOptions.BaseUrl,
        ApiKey = alpacaSymbolReferenceOptions.ApiKey,
        ApiSecret = alpacaSymbolReferenceOptions.ApiSecret,
        TimeoutSeconds = alpacaHistoricalDataOptions.TimeoutSeconds,
        Feed = alpacaHistoricalDataOptions.Feed
    };
}

builder.Services.AddSingleton(alpacaSymbolReferenceOptions);
builder.Services.AddSingleton(alpacaHistoricalDataOptions);
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton<MarketDataBootstrapStateStore>();
builder.Services.AddSingleton<MarketDataDailyRuntimeStore>();
builder.Services.AddSingleton<MarketDataIntradayRuntimeStore>();
builder.Services.AddScoped<IMarketDataSymbolDemandReader, UniverseMarketDataDemandReader>();
builder.Services.AddScoped<DailyMarketDataHydrationService>();
builder.Services.AddScoped<IntradayMarketDataHydrationService>();
builder.Services.AddScoped<MarketDataBootstrapService>();
builder.Services.AddScoped<UniverseService>();
if (alpacaSymbolReferenceOptions.UseFakeProvider)
{
    builder.Services.AddScoped<ISymbolReferenceProvider, FakeSymbolReferenceProvider>();
}
else
{
    builder.Services.AddHttpClient<ISymbolReferenceProvider, AlpacaSymbolReferenceProvider>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<AlpacaSymbolReferenceOptions>();
        client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl), UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 10);
    });
}

if (useFakeHistoricalProvider)
{
    builder.Services.AddScoped<IHistoricalBarProvider, FakeHistoricalBarProvider>();
}
else
{
    builder.Services.AddHttpClient<IHistoricalBarProvider, AlpacaHistoricalBarProvider>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<AlpacaHistoricalDataOptions>();
        client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl), UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 10);
    });
}

builder.Services.AddScoped<IExecutionRemovalGuardService, FakeExecutionRemovalGuardService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    // Startup eagerly initializes both module databases and the daily warmup so the first UI load can query real runtime state.
    var dbContext = scope.ServiceProvider.GetRequiredService<UniverseDbContext>();
    await UniverseDbInitializer.EnsureInitializedAsync(dbContext, CancellationToken.None);

    var marketDataDbContext = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();
    await MarketDataDbInitializer.EnsureInitializedAsync(marketDataDbContext, CancellationToken.None);

    var bootstrapService = scope.ServiceProvider.GetRequiredService<MarketDataBootstrapService>();
    await bootstrapService.RunWarmupAsync(CancellationToken.None);
}

app.MapAuthEndpoints();
app.MapMarketDataEndpoints();
app.MapUniverseEndpoints();
app.MapDefaultEndpoints();

app.Run();

static string EnsureTrailingSlash(string baseUrl) =>
    string.IsNullOrWhiteSpace(baseUrl)
        ? "https://paper-api.alpaca.markets/"
        : baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";

public partial class Program;
