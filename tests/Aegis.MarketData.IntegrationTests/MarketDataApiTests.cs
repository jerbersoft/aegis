using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Aegis.Adapters.Alpaca.Services;
using Aegis.MarketData.Application;
using Aegis.MarketData.Infrastructure;
using Aegis.Shared.Contracts.Auth;
using Aegis.Shared.Contracts.MarketData;
using Aegis.Shared.Contracts.Universe;
using Aegis.Shared.Ports.MarketData;
using Aegis.Universe.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

        _benchmarkBlockedFactory = factory.WithWebHostBuilder(builder =>
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
                services.AddScoped<IHistoricalBarProvider, BenchmarkBlockedHistoricalBarProvider>();
            });
        });
    }

    private readonly WebApplicationFactory<Program> _readyFactory;
    private readonly WebApplicationFactory<Program> _backfillFactory;
    private readonly WebApplicationFactory<Program> _benchmarkBlockedFactory;

    [Fact]
    public async Task RunBootstrap_ShouldWarmPersistedBars_ForUniverseSymbols()
    {
        await ResetStateAsync(_factory);
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
        status.DemandSymbols.ShouldContain(DailyMarketDataDemandExpander.BenchmarkSymbol);
        status.PersistedBarCount.ShouldBeGreaterThan(0);
        status.NotReadySymbolCount.ShouldBe(2);

        var rollup = await client.GetAegisJsonAsync<DailyUniverseReadinessView>("/api/market-data/daily/readiness");
        rollup.ShouldNotBeNull();
        rollup.TotalSymbolCount.ShouldBe(2);
        rollup.NotReadySymbolCount.ShouldBe(2);
        rollup.Symbols.ShouldContain(x => x.Symbol == "AAPL" && x.ReadinessState == "not_ready");
    }

    [Fact]
    public async Task DailyReadiness_ShouldBeReady_WhenSufficientHistoryExists()
    {
        await ResetStateAsync(_readyFactory);
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
        symbolReadiness.HasRequiredIndicatorState.ShouldBeTrue();
        symbolReadiness.HasBenchmarkDependency.ShouldBeTrue();
        symbolReadiness.BenchmarkSymbol.ShouldBe(DailyMarketDataDemandExpander.BenchmarkSymbol);
        symbolReadiness.BenchmarkReadinessState.ShouldBe("ready");
    }

    [Fact]
    public async Task Bootstrap_ShouldBackfillMissingHistory_AndTransitionToReady()
    {
        await ResetStateAsync(_backfillFactory);
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

    [Fact]
    public async Task DailyReadiness_ShouldShowBenchmarkNotReady_WhenBenchmarkHistoryIsInsufficient()
    {
        await ResetStateAsync(_benchmarkBlockedFactory);
        using var client = await CreateAuthenticatedClientAsync(_benchmarkBlockedFactory);

        var watchlist = await client.PostAsJsonAsync("/api/universe/watchlists", new CreateWatchlistRequest("MarketDataBenchmarkBlocked"));
        watchlist.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdWatchlist = await watchlist.Content.ReadAegisJsonAsync<WatchlistDetailView>();
        createdWatchlist.ShouldNotBeNull();

        var addSymbol = await client.PostAsJsonAsync($"/api/universe/watchlists/{createdWatchlist.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("MSFT"));
        addSymbol.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bootstrapResponse = await client.PostAsync("/api/market-data/bootstrap/run", null);
        bootstrapResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var symbolReadiness = await client.GetAegisJsonAsync<DailySymbolReadinessView>("/api/market-data/daily/readiness/MSFT");
        symbolReadiness.ShouldNotBeNull();
        symbolReadiness.ReadinessState.ShouldBe("not_ready");
        symbolReadiness.ReasonCode.ShouldBe("benchmark_not_ready");
        symbolReadiness.HasRequiredIndicatorState.ShouldBeFalse();
        symbolReadiness.BenchmarkSymbol.ShouldBe(DailyMarketDataDemandExpander.BenchmarkSymbol);
        symbolReadiness.BenchmarkReadinessState.ShouldBe("not_ready");
    }

    [Fact]
    public async Task IntradayReadiness_ShouldBeReady_WhenExecutionSymbolHasFinalizedOneMinuteHistory()
    {
        await ResetStateAsync(_readyFactory);
        using var client = await CreateAuthenticatedClientAsync(_readyFactory);

        var watchlists = await client.GetAegisJsonAsync<List<WatchlistSummaryView>>("/api/universe/watchlists");
        watchlists.ShouldNotBeNull();
        var execution = watchlists.Single(x => x.IsExecution);

        var addSymbol = await client.PostAsJsonAsync($"/api/universe/watchlists/{execution.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("AMD"));
        addSymbol.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bootstrapResponse = await client.PostAsync("/api/market-data/bootstrap/run", null);
        bootstrapResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var intradayReadiness = await client.GetAegisJsonAsync<IntradaySymbolReadinessView>("/api/market-data/intraday/readiness/AMD");
        intradayReadiness.ShouldNotBeNull();
        intradayReadiness.ReadinessState.ShouldBe("ready");
        intradayReadiness.HasRequiredIntradayBars.ShouldBeTrue();
        intradayReadiness.HasRequiredIndicatorState.ShouldBeTrue();
        intradayReadiness.HasRequiredVolumeBuzzReferenceHistory.ShouldBeTrue();
        intradayReadiness.AvailableVolumeBuzzReferenceSessionCount.ShouldBe(IntradayMarketDataHydrationService.VolumeBuzzReferenceSessionCount);
        intradayReadiness.VolumeBuzzPercent.ShouldNotBeNull();
        intradayReadiness.AvailableBarCount.ShouldBeGreaterThanOrEqualTo(IntradayMarketDataHydrationService.IntradayRequiredBarCount);
        intradayReadiness.ActiveGapType.ShouldBeNull();
    }

    [Fact]
    public async Task IntradayReadiness_ShouldExposeGapReason_WhenPersistedExecutionHistoryHasInternalGap()
    {
        await ResetStateAsync(_readyFactory);
        using var client = await CreateAuthenticatedClientAsync(_readyFactory);

        var watchlists = await client.GetAegisJsonAsync<List<WatchlistSummaryView>>("/api/universe/watchlists");
        watchlists.ShouldNotBeNull();
        var execution = watchlists.Single(x => x.IsExecution);

        var addSymbol = await client.PostAsJsonAsync($"/api/universe/watchlists/{execution.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("AMD"));
        addSymbol.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bootstrapResponse = await client.PostAsync("/api/market-data/bootstrap/run", null);
        bootstrapResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await RemoveIntradayBarAsync(_readyFactory, "AMD", Instant.FromUtc(2026, 3, 12, 15, 0));
        var statusRefresh = await client.GetAsync("/api/market-data/bootstrap/status");
        statusRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        var intradayReadiness = await client.GetAegisJsonAsync<IntradaySymbolReadinessView>("/api/market-data/intraday/readiness/AMD");
        intradayReadiness.ShouldNotBeNull();
        intradayReadiness.ReadinessState.ShouldBe("not_ready");
        intradayReadiness.ReasonCode.ShouldBe("gap_internal");
        intradayReadiness.ActiveGapType.ShouldBe("internal");
        intradayReadiness.ActiveGapStartUtc.ShouldBe(Instant.FromUtc(2026, 3, 12, 15, 0));
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

    private static async Task ResetStateAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var marketDataDbContext = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();
        var universeDbContext = scope.ServiceProvider.GetRequiredService<UniverseDbContext>();

        await marketDataDbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE bar RESTART IDENTITY CASCADE;");
        await universeDbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE watchlist_item, watchlist, symbol RESTART IDENTITY CASCADE;");
        await UniverseDbInitializer.EnsureInitializedAsync(universeDbContext, CancellationToken.None);
    }

    private static async Task RemoveIntradayBarAsync(WebApplicationFactory<Program> factory, string symbol, Instant barTimeUtc)
    {
        using var scope = factory.Services.CreateScope();
        var marketDataDbContext = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();
        var row = await marketDataDbContext.Bars.SingleAsync(x => x.Symbol == symbol && x.Interval == IntradayMarketDataHydrationService.IntradayInterval && x.BarTimeUtc == barTimeUtc);
        marketDataDbContext.Bars.Remove(row);
        await marketDataDbContext.SaveChangesAsync();
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

        public Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, request.Interval, BuildIntradayBars(request.Symbol, request.Interval), "fake", "iex"));
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

        public Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, request.Interval, BuildIntradayBars(request.Symbol, request.Interval), "fake", "iex"));
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

        public Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, request.Interval, BuildIntradayBars(request.Symbol, request.Interval), "fake", "iex"));
    }

    private sealed class BenchmarkBlockedHistoricalBarProvider : IHistoricalBarProvider
    {
        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken)
        {
            var count = string.Equals(request.Symbol, DailyMarketDataDemandExpander.BenchmarkSymbol, StringComparison.OrdinalIgnoreCase) ? 50 : 220;
            var bars = Enumerable.Range(0, count)
                .Select(index =>
                {
                    var barTime = Instant.FromUtc(2025, 1, 1, 0, 0) + Duration.FromDays(index);
                    return new HistoricalBarRecord(request.Symbol, "1day", barTime, 100 + index, 101 + index, 99 + index, 100 + index, 1000 + index, "regular", barTime.InUtc().Date, "reconciled", true);
                })
                .ToArray();

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, "1day", bars, "fake", "iex"));
        }

        public Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, request.Interval, BuildIntradayBars(request.Symbol, request.Interval), "fake", "iex"));
    }

    private static HistoricalBarRecord[] BuildIntradayBars(string symbol, string interval) =>
        Enumerable.Range(0, 11)
            .SelectMany(sessionIndex => Enumerable.Range(0, 390)
            .Select(index =>
            {
                var marketDate = new LocalDate(2026, 3, 2).PlusDays(sessionIndex);
                var barTime = Instant.FromUtc(marketDate.Year, marketDate.Month, marketDate.Day, 14, 30) + Duration.FromMinutes(index);
                return new HistoricalBarRecord(symbol, interval, barTime, 100 + sessionIndex + index / 10m, 101 + sessionIndex + index / 10m, 99 + sessionIndex + index / 10m, 100 + sessionIndex + index / 10m, 10_000 + (sessionIndex * 100) + index, "regular", marketDate, "reconciled", true);
            }))
            .ToArray();
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
