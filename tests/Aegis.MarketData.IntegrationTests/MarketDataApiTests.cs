using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Aegis.Adapters.Alpaca.Services;
using Aegis.MarketData.Application;
using Aegis.Shared.Contracts.Auth;
using Aegis.Shared.Contracts.MarketData;
using Aegis.Shared.Contracts.Universe;
using Aegis.Shared.Ports.MarketData;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using Shouldly;

namespace Aegis.MarketData.IntegrationTests;

public sealed class MarketDataApiTests : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<PostgresTestContainer>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MarketDataApiTests(WebApplicationFactory<Program> factory, PostgresTestContainer postgres)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = postgres.ConnectionString,
                    ["ConnectionStrings:Universe"] = postgres.ConnectionString,
                    ["ConnectionStrings:MarketData"] = postgres.ConnectionString,
                    ["Alpaca:SymbolReference:UseFakeProvider"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISymbolReferenceProvider>();
                services.AddScoped<ISymbolReferenceProvider, FakeSymbolReferenceProvider>();
                services.RemoveAll<IHistoricalBarProvider>();
                services.AddScoped<IHistoricalBarProvider, TestHistoricalBarProvider>();
            });
        });

        _readyFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = postgres.ConnectionString,
                    ["ConnectionStrings:Universe"] = postgres.ConnectionString,
                    ["ConnectionStrings:MarketData"] = postgres.ConnectionString,
                    ["Alpaca:SymbolReference:UseFakeProvider"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISymbolReferenceProvider>();
                services.AddScoped<ISymbolReferenceProvider, FakeSymbolReferenceProvider>();
                services.RemoveAll<IHistoricalBarProvider>();
                services.AddScoped<IHistoricalBarProvider, DailyHistoryHistoricalBarProvider>();
            });
        });

        _backfillFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = postgres.ConnectionString,
                    ["ConnectionStrings:Universe"] = postgres.ConnectionString,
                    ["ConnectionStrings:MarketData"] = postgres.ConnectionString,
                    ["Alpaca:SymbolReference:UseFakeProvider"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISymbolReferenceProvider>();
                services.AddScoped<ISymbolReferenceProvider, FakeSymbolReferenceProvider>();
                services.RemoveAll<IHistoricalBarProvider>();
                services.AddScoped<IHistoricalBarProvider, BackfillHistoricalBarProvider>();
            });
        });
    }

    private readonly WebApplicationFactory<Program> _readyFactory;
    private readonly WebApplicationFactory<Program> _backfillFactory;

    [Fact]
    public async Task RunBootstrap_ShouldWarmPersistedBars_ForUniverseSymbols()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var watchlist = await client.PostAsJsonAsync("/api/universe/watchlists", new CreateWatchlistRequest("MarketDataDemand"));
        watchlist.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdWatchlist = await watchlist.Content.ReadAegisJsonAsync<WatchlistDetailView>();
        createdWatchlist.ShouldNotBeNull();

        var addSymbol = await client.PostAsJsonAsync($"/api/universe/watchlists/{createdWatchlist.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("AAPL"));
        addSymbol.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bootstrapResponse = await client.PostAsync("/api/market-data/bootstrap/run", null);
        bootstrapResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var status = await bootstrapResponse.Content.ReadAegisJsonAsync<MarketDataBootstrapStatusView>();
        status.ShouldNotBeNull();
        status.ReadinessState.ShouldBe("not_ready");
        status.ReasonCode.ShouldBe("missing_required_bars");
        status.DemandSymbols.ShouldContain("AAPL");
        status.PersistedBarCount.ShouldBeGreaterThan(0);
        status.NotReadySymbolCount.ShouldBe(1);

        var rollup = await client.GetAegisJsonAsync<DailyUniverseReadinessView>("/api/market-data/daily/readiness");
        rollup.ShouldNotBeNull();
        rollup.TotalSymbolCount.ShouldBe(1);
        rollup.NotReadySymbolCount.ShouldBe(1);
        rollup.Symbols.ShouldContain(x => x.Symbol == "AAPL" && x.ReadinessState == "not_ready");
    }

    [Fact]
    public async Task DailyReadiness_ShouldBeReady_WhenSufficientHistoryExists()
    {
        using var client = await CreateAuthenticatedClientAsync(_readyFactory);

        var watchlist = await client.PostAsJsonAsync("/api/universe/watchlists", new CreateWatchlistRequest("MarketDataReady"));
        watchlist.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdWatchlist = await watchlist.Content.ReadAegisJsonAsync<WatchlistDetailView>();
        createdWatchlist.ShouldNotBeNull();

        var addSymbol = await client.PostAsJsonAsync($"/api/universe/watchlists/{createdWatchlist.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("MSFT"));
        addSymbol.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bootstrapResponse = await client.PostAsync("/api/market-data/bootstrap/run", null);
        bootstrapResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var symbolReadiness = await client.GetAegisJsonAsync<DailySymbolReadinessView>("/api/market-data/daily/readiness/MSFT");
        symbolReadiness.ShouldNotBeNull();
        symbolReadiness.ReadinessState.ShouldBe("ready");
        symbolReadiness.AvailableBarCount.ShouldBeGreaterThanOrEqualTo(DailyHistoryHistoricalBarProvider.ReadyBarCount);
    }

    [Fact]
    public async Task Bootstrap_ShouldBackfillMissingHistory_AndTransitionToReady()
    {
        using var client = await CreateAuthenticatedClientAsync(_backfillFactory);

        var watchlist = await client.PostAsJsonAsync("/api/universe/watchlists", new CreateWatchlistRequest("MarketDataBackfill"));
        watchlist.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdWatchlist = await watchlist.Content.ReadAegisJsonAsync<WatchlistDetailView>();
        createdWatchlist.ShouldNotBeNull();

        var addSymbol = await client.PostAsJsonAsync($"/api/universe/watchlists/{createdWatchlist.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("NVDA"));
        addSymbol.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bootstrapResponse = await client.PostAsync("/api/market-data/bootstrap/run", null);
        bootstrapResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var status = await bootstrapResponse.Content.ReadAegisJsonAsync<MarketDataBootstrapStatusView>();
        status.ShouldNotBeNull();
        status.ReadinessState.ShouldBe("ready");

        var symbolReadiness = await client.GetAegisJsonAsync<DailySymbolReadinessView>("/api/market-data/daily/readiness/NVDA");
        symbolReadiness.ShouldNotBeNull();
        symbolReadiness.ReadinessState.ShouldBe("ready");
        symbolReadiness.AvailableBarCount.ShouldBeGreaterThanOrEqualTo(DailyMarketDataHydrationService.DailyCoreRequiredBarCount);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program>? factory = null)
    {
        var client = (factory ?? _factory).CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("demo", "demo"));
        loginResponse.EnsureSuccessStatusCode();
        return client;
    }

    private sealed class TestHistoricalBarProvider : IHistoricalBarProvider
    {
        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken)
        {
            HistoricalBarRecord[] bars =
            [
                new(request.Symbol, "1day", Instant.FromUtc(2026, 3, 10, 0, 0), 100, 105, 99, 104, 1000, "regular", new LocalDate(2026, 3, 10), "reconciled", true),
                new(request.Symbol, "1day", Instant.FromUtc(2026, 3, 11, 0, 0), 104, 106, 103, 105, 1200, "regular", new LocalDate(2026, 3, 11), "reconciled", true)
            ];

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, "1day", bars, "fake", "iex"));
        }
    }

    private sealed class DailyHistoryHistoricalBarProvider : IHistoricalBarProvider
    {
        public const int ReadyBarCount = 220;

        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken)
        {
            var bars = Enumerable.Range(0, ReadyBarCount)
                .Select(index =>
                {
                    var barTime = Instant.FromUtc(2025, 1, 1, 0, 0) + Duration.FromDays(index);
                    return new HistoricalBarRecord(request.Symbol, "1day", barTime, 100 + index, 101 + index, 99 + index, 100 + index, 1000 + index, "regular", barTime.InUtc().Date, "reconciled", true);
                })
                .ToArray();

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, "1day", bars, "fake", "iex"));
        }
    }

    private sealed class BackfillHistoricalBarProvider : IHistoricalBarProvider
    {
        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken)
        {
            var bars = Enumerable.Range(0, 220)
                .Select(index =>
                {
                    var barTime = Instant.FromUtc(2024, 8, 1, 0, 0) + Duration.FromDays(index);
                    return new HistoricalBarRecord(request.Symbol, "1day", barTime, 100 + index, 101 + index, 99 + index, 100 + index, 1000 + index, "regular", barTime.InUtc().Date, "reconciled", true);
                })
                .ToArray();

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, "1day", bars, "fake", "iex"));
        }
    }
}

public sealed class PostgresTestContainer : IAsyncLifetime
{
    private readonly string _containerName = $"aegis-tests-{Guid.NewGuid():N}";
    private readonly int _hostPort = GetFreePort();

    public string ConnectionString => $"Host=localhost;Port={_hostPort};Database=aegis;Username=postgres;Password=postgres";

    public async Task InitializeAsync()
    {
        await RunDockerAsync($"run -d --name {_containerName} -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=aegis -p {_hostPort}:5432 postgres:17");

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var result = await RunDockerAsync($"exec {_containerName} pg_isready -U postgres -d aegis", throwOnFailure: false);
            if (result == 0)
            {
                return;
            }

            await Task.Delay(1000);
        }

        throw new InvalidOperationException("PostgreSQL test container did not become ready in time.");
    }

    public async Task DisposeAsync()
    {
        await RunDockerAsync($"rm -f {_containerName}", throwOnFailure: false);
    }

    private static async Task<int> RunDockerAsync(string arguments, bool throwOnFailure = true)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (throwOnFailure && process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Docker command failed: docker {arguments}\n{error}");
        }

        return process.ExitCode;
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
