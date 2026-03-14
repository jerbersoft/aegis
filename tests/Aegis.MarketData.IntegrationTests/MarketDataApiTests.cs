using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Aegis.Adapters.Alpaca.Services;
using Aegis.MarketData.Application;
using Aegis.MarketData.Infrastructure;
using Aegis.Shared.Contracts.Auth;
using Aegis.Shared.Contracts.MarketData;
using Aegis.Shared.Contracts.Universe;
using Aegis.Shared.Ports.MarketData;
using Aegis.Universe.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
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

        _repairFetchFailingFactory = factory.WithWebHostBuilder(builder =>
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
                services.AddScoped<IHistoricalBarProvider, RepairFetchFailingHistoricalBarProvider>();
            });
        });

        _repairValidationFailingFactory = factory.WithWebHostBuilder(builder =>
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
                services.AddScoped<IHistoricalBarProvider, RepairValidationFailingHistoricalBarProvider>();
            });
        });

        _materiallyCorrectedFactory = factory.WithWebHostBuilder(builder =>
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
                services.AddScoped<IHistoricalBarProvider, MateriallyCorrectedHistoricalBarProvider>();
            });
        });
    }

    private readonly WebApplicationFactory<Program> _readyFactory;
    private readonly WebApplicationFactory<Program> _backfillFactory;
    private readonly WebApplicationFactory<Program> _benchmarkBlockedFactory;
    private readonly WebApplicationFactory<Program> _repairFetchFailingFactory;
    private readonly WebApplicationFactory<Program> _repairValidationFailingFactory;
    private readonly WebApplicationFactory<Program> _materiallyCorrectedFactory;

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
        intradayReadiness.HasActiveRepair.ShouldBeFalse();
        intradayReadiness.PendingRecompute.ShouldBeFalse();
        intradayReadiness.EarliestAffectedBarUtc.ShouldBeNull();
    }

    [Fact]
    public async Task IntradayReadiness_ShouldRepairInternalGap_AndRestoreReadyState()
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

        var awaitingRecompute = await client.GetAegisJsonAsync<IntradaySymbolReadinessView>("/api/market-data/intraday/readiness/AMD");
        awaitingRecompute.ShouldNotBeNull();
        awaitingRecompute.ReadinessState.ShouldBe("repairing");
        awaitingRecompute.ReasonCode.ShouldBe("awaiting_recompute");
        awaitingRecompute.PendingRecompute.ShouldBeTrue();

        var finalRefresh = await client.GetAsync("/api/market-data/bootstrap/status");
        finalRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        var intradayReadiness = await client.GetAegisJsonAsync<IntradaySymbolReadinessView>("/api/market-data/intraday/readiness/AMD");
        intradayReadiness.ShouldNotBeNull();
        intradayReadiness.ReadinessState.ShouldBe("ready");
        intradayReadiness.ReasonCode.ShouldBe("none");
        intradayReadiness.HasRequiredIntradayBars.ShouldBeTrue();
        intradayReadiness.HasRequiredIndicatorState.ShouldBeTrue();
        intradayReadiness.ActiveGapType.ShouldBeNull();
        intradayReadiness.ActiveGapStartUtc.ShouldBeNull();

        var rollupReadiness = await client.GetAegisJsonAsync<IntradayUniverseReadinessView>("/api/market-data/intraday/readiness");
        rollupReadiness.ShouldNotBeNull();
        rollupReadiness.ReadinessState.ShouldBe("ready");
        rollupReadiness.ReasonCode.ShouldBe("none");
        rollupReadiness.Symbols.ShouldContain(x => x.Symbol == "AMD" && x.ReadinessState == "ready");

        await AssertIntradayBarExistsAsync(_readyFactory, "AMD", Instant.FromUtc(2026, 3, 12, 15, 0));
    }

    [Fact]
    public async Task IntradayReadiness_ShouldNormalizeCorrectedBar_AndRestoreReadyState()
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

        var correctedBarTimeUtc = Instant.FromUtc(2026, 3, 12, 15, 0);
        await MarkIntradayBarCorrectedAsync(_readyFactory, "AMD", correctedBarTimeUtc);

        var statusRefresh = await client.GetAsync("/api/market-data/bootstrap/status");
        statusRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        var intradayReadiness = await client.GetAegisJsonAsync<IntradaySymbolReadinessView>("/api/market-data/intraday/readiness/AMD");
        intradayReadiness.ShouldNotBeNull();
        intradayReadiness.ReadinessState.ShouldBe("ready");
        intradayReadiness.ReasonCode.ShouldBe("none");
        intradayReadiness.HasRequiredIndicatorState.ShouldBeTrue();
        intradayReadiness.ActiveGapType.ShouldBeNull();
        intradayReadiness.ActiveGapStartUtc.ShouldBeNull();

        await AssertIntradayBarReconciledAsync(_readyFactory, "AMD", correctedBarTimeUtc);
    }

    [Fact]
    public async Task IntradayReadiness_ShouldRemainRepairing_WhenRepairValidationFails()
    {
        await ResetStateAsync(_repairValidationFailingFactory);
        using var client = await CreateAuthenticatedClientAsync(_repairValidationFailingFactory);

        var watchlists = await client.GetAegisJsonAsync<List<WatchlistSummaryView>>("/api/universe/watchlists");
        watchlists.ShouldNotBeNull();
        var execution = watchlists.Single(x => x.IsExecution);

        var addSymbol = await client.PostAsJsonAsync($"/api/universe/watchlists/{execution.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("AMD"));
        addSymbol.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bootstrapResponse = await client.PostAsync("/api/market-data/bootstrap/run", null);
        bootstrapResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await RemoveIntradayBarAsync(_repairValidationFailingFactory, "AMD", Instant.FromUtc(2026, 3, 12, 15, 0));
        var statusRefresh = await client.GetAsync("/api/market-data/bootstrap/status");
        statusRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        var awaitingRecompute = await client.GetAegisJsonAsync<IntradaySymbolReadinessView>("/api/market-data/intraday/readiness/AMD");
        awaitingRecompute.ShouldNotBeNull();
        awaitingRecompute.ReadinessState.ShouldBe("repairing");
        awaitingRecompute.ReasonCode.ShouldBe(IntradayRepairState.AwaitingRecomputeReasonCode);
        awaitingRecompute.PendingRecompute.ShouldBeTrue();

        var validationRefresh = await client.GetAsync("/api/market-data/bootstrap/status");
        validationRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        var intradayReadiness = await client.GetAegisJsonAsync<IntradaySymbolReadinessView>("/api/market-data/intraday/readiness/AMD");
        intradayReadiness.ShouldNotBeNull();
        intradayReadiness.ReadinessState.ShouldBe("repairing");
        intradayReadiness.ReasonCode.ShouldBe(IntradayRepairState.RepairValidationFailedReasonCode);
        intradayReadiness.HasRequiredIndicatorState.ShouldBeFalse();
        intradayReadiness.ActiveGapType.ShouldBe("internal");
        intradayReadiness.ActiveGapStartUtc.ShouldBe(Instant.FromUtc(2026, 3, 12, 15, 0));
        intradayReadiness.HasActiveRepair.ShouldBeTrue();
        intradayReadiness.PendingRecompute.ShouldBeFalse();
        intradayReadiness.EarliestAffectedBarUtc.ShouldBe(Instant.FromUtc(2026, 3, 12, 15, 0));

        var rollupReadiness = await client.GetAegisJsonAsync<IntradayUniverseReadinessView>("/api/market-data/intraday/readiness");
        rollupReadiness.ShouldNotBeNull();
        rollupReadiness.ReadinessState.ShouldBe("repairing");
        rollupReadiness.ReasonCode.ShouldBe(IntradayRepairState.RepairValidationFailedReasonCode);
        rollupReadiness.ActiveRepairSymbolCount.ShouldBe(1);
        rollupReadiness.PendingRecomputeSymbolCount.ShouldBe(0);
        rollupReadiness.EarliestAffectedBarUtc.ShouldBe(Instant.FromUtc(2026, 3, 12, 15, 0));
    }

    [Fact]
    public async Task IntradayReadiness_ShouldRemainRepairing_WhenRepairFetchFails()
    {
        await ResetStateAsync(_repairFetchFailingFactory);
        using var client = await CreateAuthenticatedClientAsync(_repairFetchFailingFactory);

        var watchlists = await client.GetAegisJsonAsync<List<WatchlistSummaryView>>("/api/universe/watchlists");
        watchlists.ShouldNotBeNull();
        var execution = watchlists.Single(x => x.IsExecution);

        var addSymbol = await client.PostAsJsonAsync($"/api/universe/watchlists/{execution.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("AMD"));
        addSymbol.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bootstrapResponse = await client.PostAsync("/api/market-data/bootstrap/run", null);
        bootstrapResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await RemoveIntradayBarAsync(_repairFetchFailingFactory, "AMD", Instant.FromUtc(2026, 3, 12, 15, 0));
        var statusRefresh = await client.GetAsync("/api/market-data/bootstrap/status");
        statusRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        var intradayReadiness = await client.GetAegisJsonAsync<IntradaySymbolReadinessView>("/api/market-data/intraday/readiness/AMD");
        intradayReadiness.ShouldNotBeNull();
        intradayReadiness.ReadinessState.ShouldBe("repairing");
        intradayReadiness.ReasonCode.ShouldBe(IntradayRepairState.RepairFetchFailedReasonCode);
        intradayReadiness.HasRequiredIndicatorState.ShouldBeFalse();
        intradayReadiness.ActiveGapType.ShouldBe("internal");
        intradayReadiness.ActiveGapStartUtc.ShouldBe(Instant.FromUtc(2026, 3, 12, 15, 0));
        intradayReadiness.HasActiveRepair.ShouldBeTrue();
        intradayReadiness.PendingRecompute.ShouldBeFalse();
        intradayReadiness.EarliestAffectedBarUtc.ShouldBe(Instant.FromUtc(2026, 3, 12, 15, 0));
    }

    [Fact]
    public async Task IntradayReadiness_ShouldExposeAwaitingRecompute_BeforeRestoredReadyState()
    {
        await ResetStateAsync(_materiallyCorrectedFactory);
        using var client = await CreateAuthenticatedClientAsync(_materiallyCorrectedFactory);

        var watchlists = await client.GetAegisJsonAsync<List<WatchlistSummaryView>>("/api/universe/watchlists");
        watchlists.ShouldNotBeNull();
        var execution = watchlists.Single(x => x.IsExecution);

        var addSymbol = await client.PostAsJsonAsync($"/api/universe/watchlists/{execution.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("AMD"));
        addSymbol.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bootstrapResponse = await client.PostAsync("/api/market-data/bootstrap/run", null);
        bootstrapResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var correctedBarTimeUtc = Instant.FromUtc(2026, 3, 12, 15, 0);
        await MarkIntradayBarCorrectedAsync(_materiallyCorrectedFactory, "AMD", correctedBarTimeUtc);

        var statusRefresh = await client.GetAsync("/api/market-data/bootstrap/status");
        statusRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        var intradayReadiness = await client.GetAegisJsonAsync<IntradaySymbolReadinessView>("/api/market-data/intraday/readiness/AMD");
        intradayReadiness.ShouldNotBeNull();
        intradayReadiness.ReadinessState.ShouldBe("repairing");
        intradayReadiness.ReasonCode.ShouldBe("awaiting_recompute");
        intradayReadiness.HasActiveRepair.ShouldBeTrue();
        intradayReadiness.PendingRecompute.ShouldBeTrue();
        intradayReadiness.EarliestAffectedBarUtc.ShouldBe(correctedBarTimeUtc);

        var rollupReadiness = await client.GetAegisJsonAsync<IntradayUniverseReadinessView>("/api/market-data/intraday/readiness");
        rollupReadiness.ShouldNotBeNull();
        rollupReadiness.ReadinessState.ShouldBe("repairing");
        rollupReadiness.ReasonCode.ShouldBe("awaiting_recompute");
        rollupReadiness.ActiveRepairSymbolCount.ShouldBe(1);
        rollupReadiness.PendingRecomputeSymbolCount.ShouldBe(1);
        rollupReadiness.EarliestAffectedBarUtc.ShouldBe(correctedBarTimeUtc);

        var finalRefresh = await client.GetAsync("/api/market-data/bootstrap/status");
        finalRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        var restoredReadiness = await client.GetAegisJsonAsync<IntradaySymbolReadinessView>("/api/market-data/intraday/readiness/AMD");
        restoredReadiness.ShouldNotBeNull();
        restoredReadiness.ReadinessState.ShouldBe("ready");
        restoredReadiness.ReasonCode.ShouldBe("none");
        restoredReadiness.HasActiveRepair.ShouldBeFalse();
        restoredReadiness.PendingRecompute.ShouldBeFalse();
    }

    [Fact]
    public async Task MarketDataHub_ShouldRejectUnauthenticatedConnections()
    {
        await ResetStateAsync(_factory);

        await using var connection = CreateHubConnection(_factory, new HttpClientHandler());

        await Should.ThrowAsync<Exception>(async () => await connection.StartAsync());
    }

    [Fact]
    public async Task MarketDataHub_ShouldDeliverWatchlistSnapshot_AndHomeRefreshHint_ForAuthenticatedClient()
    {
        await ResetStateAsync(_readyFactory);
        using var client = await CreateAuthenticatedClientAsync(_readyFactory);

        var watchlistResponse = await client.PostAsJsonAsync("/api/universe/watchlists", new CreateWatchlistRequest("RealtimeWatchlist"));
        watchlistResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var watchlist = await watchlistResponse.Content.ReadAegisJsonAsync<WatchlistDetailView>();
        watchlist.ShouldNotBeNull();

        var addSymbolResponse = await client.PostAsJsonAsync($"/api/universe/watchlists/{watchlist.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("AAPL"));
        addSymbolResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        await using var connection = CreateHubConnection(_readyFactory, await CreateAuthenticatedHubHandlerAsync(_readyFactory));
        var rawWatchlistEvents = new List<JsonElement>();
        var rawHomeEvents = new List<JsonElement>();

        connection.On<JsonElement>(MarketDataRealtimeContract.EventNames.WatchlistSnapshot, payload => rawWatchlistEvents.Add(payload));
        connection.On<JsonElement>(MarketDataRealtimeContract.EventNames.HomeRefreshHint, payload => rawHomeEvents.Add(payload));

        await connection.StartAsync();

        var homeAck = await connection.InvokeAsync<MarketDataSubscriptionAck>("SubscribeHome");
        homeAck.ScopeKind.ShouldBe(MarketDataRealtimeContract.ScopeKinds.Home);
        homeAck.RequiresAuthoritativeRefresh.ShouldBeTrue();

        var watchlistAck = await connection.InvokeAsync<MarketDataSubscriptionAck>("SubscribeWatchlist", new MarketDataWatchlistSubscriptionRequest(watchlist.WatchlistId));
        watchlistAck.ScopeKind.ShouldBe(MarketDataRealtimeContract.ScopeKinds.Watchlist);
        watchlistAck.ScopeKey.ShouldBe(watchlist.WatchlistId.ToString("D"));

        var bootstrapResponse = await client.PostAsync("/api/market-data/bootstrap/run", null);
        bootstrapResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await WaitForAsync(() => rawWatchlistEvents.Count >= 2 && rawHomeEvents.Count >= 1);

        var watchlistEvents = rawWatchlistEvents
            .Select(payload => JsonSerializer.Deserialize<MarketDataWatchlistSnapshotEvent>(payload.GetRawText(), Aegis.Shared.Serialization.AegisJson.CreateSerializerOptions()))
            .ToList();
        var homeEvents = rawHomeEvents
            .Select(payload => JsonSerializer.Deserialize<MarketDataHomeRefreshEvent>(payload.GetRawText(), Aegis.Shared.Serialization.AegisJson.CreateSerializerOptions()))
            .ToList();

        watchlistEvents.All(x => x is not null).ShouldBeTrue();
        homeEvents.All(x => x is not null).ShouldBeTrue();

        watchlistEvents[0]!.WatchlistId.ShouldBe(watchlist.WatchlistId);
        watchlistEvents[^1]!.Symbols.ShouldContain(x => x.Symbol == "AAPL");
        watchlistEvents[^1]!.RequiresRefresh.ShouldBeTrue();

        homeEvents[^1]!.ChangedScopes.ShouldContain(MarketDataRealtimeContract.ChangeScopes.BootstrapStatus);
        homeEvents[^1]!.ChangedScopes.ShouldContain(MarketDataRealtimeContract.ChangeScopes.DailyReadiness);
        homeEvents[^1]!.ChangedScopes.ShouldContain(MarketDataRealtimeContract.ChangeScopes.IntradayReadiness);

        AssertSnakeCaseHomeRefreshEvent(rawHomeEvents[^1]);
        AssertSnakeCaseWatchlistSnapshotEvent(rawWatchlistEvents[^1]);
    }

    [Fact]
    public async Task MarketDataHub_ShouldRejectUnknownWatchlistSubscription_ForAuthenticatedClient()
    {
        await ResetStateAsync(_factory);

        await using var connection = CreateHubConnection(_factory, await CreateAuthenticatedHubHandlerAsync(_factory));
        await connection.StartAsync();

        var error = await Should.ThrowAsync<Exception>(async () =>
            await connection.InvokeAsync<MarketDataSubscriptionAck>(
                "SubscribeWatchlist",
                new MarketDataWatchlistSubscriptionRequest(Guid.NewGuid())));

        error.Message.ShouldContain("watchlist_not_found");
    }

    [Fact]
    public async Task MarketDataHub_ShouldBindSnakeCaseWatchlistSubscriptionRequest_ForAuthenticatedClient()
    {
        await ResetStateAsync(_factory);
        using var client = await CreateAuthenticatedClientAsync(_factory);

        var watchlistResponse = await client.PostAsJsonAsync("/api/universe/watchlists", new CreateWatchlistRequest("SnakeCaseBindingWatchlist"));
        watchlistResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var watchlist = await watchlistResponse.Content.ReadAegisJsonAsync<WatchlistDetailView>();
        watchlist.ShouldNotBeNull();

        await using var connection = CreateHubConnection(_factory, await CreateAuthenticatedHubHandlerAsync(_factory));
        var watchlistEvents = new List<JsonElement>();
        connection.On<JsonElement>(MarketDataRealtimeContract.EventNames.WatchlistSnapshot, payload => watchlistEvents.Add(payload));

        await connection.StartAsync();

        var snakeCaseRequest = JsonSerializer.SerializeToElement(new { watchlist_id = watchlist.WatchlistId });
        var ack = await connection.InvokeAsync<MarketDataSubscriptionAck>("SubscribeWatchlist", snakeCaseRequest);

        ack.ScopeKind.ShouldBe(MarketDataRealtimeContract.ScopeKinds.Watchlist);
        ack.ScopeKey.ShouldBe(watchlist.WatchlistId.ToString("D"));

        await WaitForAsync(() => watchlistEvents.Count >= 1);
        watchlistEvents[0].TryGetProperty("watchlist_id", out var watchlistIdProperty).ShouldBeTrue();
        watchlistIdProperty.GetGuid().ShouldBe(watchlist.WatchlistId);
    }

    [Fact]
    public async Task MarketDataHub_ShouldSendInitialWatchlistSnapshot_AlignedWithAuthoritativeDailyBars()
    {
        await ResetStateAsync(_readyFactory);
        using var client = await CreateAuthenticatedClientAsync(_readyFactory);

        var watchlistResponse = await client.PostAsJsonAsync("/api/universe/watchlists", new CreateWatchlistRequest("InitialSnapshotWatchlist"));
        watchlistResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var watchlist = await watchlistResponse.Content.ReadAegisJsonAsync<WatchlistDetailView>();
        watchlist.ShouldNotBeNull();

        var addSymbolResponse = await client.PostAsJsonAsync($"/api/universe/watchlists/{watchlist.WatchlistId}/symbols", new AddSymbolToWatchlistRequest("AAPL"));
        addSymbolResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var bootstrapResponse = await client.PostAsync("/api/market-data/bootstrap/run", null);
        bootstrapResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var dailyBars = await client.GetAegisJsonAsync<DailyBarsView>("/api/market-data/daily-bars/AAPL");
        dailyBars.ShouldNotBeNull();
        dailyBars.Items.Count.ShouldBeGreaterThanOrEqualTo(2);

        var expectedCurrentPrice = dailyBars.Items[0].Close;
        var expectedPercentChange = ((dailyBars.Items[0].Close / dailyBars.Items[1].Close) - 1m) * 100m;

        await using var connection = CreateHubConnection(_readyFactory, await CreateAuthenticatedHubHandlerAsync(_readyFactory));
        var watchlistEvents = new List<MarketDataWatchlistSnapshotEvent>();
        connection.On<MarketDataWatchlistSnapshotEvent>(MarketDataRealtimeContract.EventNames.WatchlistSnapshot, payload => watchlistEvents.Add(payload));

        await connection.StartAsync();

        var ack = await connection.InvokeAsync<MarketDataSubscriptionAck>(
            "SubscribeWatchlist",
            new MarketDataWatchlistSubscriptionRequest(watchlist.WatchlistId));

        ack.ScopeKind.ShouldBe(MarketDataRealtimeContract.ScopeKinds.Watchlist);
        ack.ScopeKey.ShouldBe(watchlist.WatchlistId.ToString("D"));
        ack.DeliveryStrategy.ShouldBe(MarketDataRealtimeContract.DeliveryStrategies.CoalescedSnapshotDelta);
        ack.RequiresAuthoritativeRefresh.ShouldBeTrue();

        await WaitForAsync(() => watchlistEvents.Count >= 1);

        watchlistEvents[0].ContractVersion.ShouldBe(MarketDataRealtimeContract.ContractVersion);
        watchlistEvents[0].WatchlistId.ShouldBe(watchlist.WatchlistId);
        watchlistEvents[0].BatchSequence.ShouldBe(1);
        watchlistEvents[0].RequiresRefresh.ShouldBeTrue();

        var symbol = watchlistEvents[0].Symbols.ShouldHaveSingleItem();
        symbol.Symbol.ShouldBe("AAPL");
        symbol.CurrentPrice.ShouldBe(expectedCurrentPrice);
        symbol.PercentChange.ShouldBe(expectedPercentChange);
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

    private static HubConnection CreateHubConnection(WebApplicationFactory<Program> factory, HttpMessageHandler handler) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, MarketDataRealtimeContract.HubPath), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => handler;
            })
            .AddJsonProtocol(options => Aegis.Shared.Serialization.AegisJson.Configure(options.PayloadSerializerOptions))
            .Build();

    private static async Task<HttpMessageHandler> CreateAuthenticatedHubHandlerAsync(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("demo", "demo"));
        loginResponse.EnsureSuccessStatusCode();
        var cookieHeader = loginResponse.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("; ", values.Select(x => x.Split(';', 2)[0]))
            : throw new InvalidOperationException("Login response did not include an auth cookie.");

        return new CookieHeaderHandler(cookieHeader, factory.Server.CreateHandler());
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Timed out waiting for SignalR event delivery.");
    }

    private static void AssertSnakeCaseHomeRefreshEvent(JsonElement payload)
    {
        payload.TryGetProperty("contract_version", out _).ShouldBeTrue();
        payload.TryGetProperty("event_id", out _).ShouldBeTrue();
        payload.TryGetProperty("occurred_utc", out _).ShouldBeTrue();
        payload.TryGetProperty("requires_refresh", out _).ShouldBeTrue();
        payload.TryGetProperty("changed_scopes", out _).ShouldBeTrue();
        payload.TryGetProperty("contractVersion", out _).ShouldBeFalse();
        payload.TryGetProperty("eventId", out _).ShouldBeFalse();
        payload.TryGetProperty("occurredUtc", out _).ShouldBeFalse();
        payload.TryGetProperty("requiresRefresh", out _).ShouldBeFalse();
        payload.TryGetProperty("changedScopes", out _).ShouldBeFalse();
    }

    private static void AssertSnakeCaseWatchlistSnapshotEvent(JsonElement payload)
    {
        payload.TryGetProperty("contract_version", out _).ShouldBeTrue();
        payload.TryGetProperty("event_id", out _).ShouldBeTrue();
        payload.TryGetProperty("watchlist_id", out _).ShouldBeTrue();
        payload.TryGetProperty("batch_sequence", out _).ShouldBeTrue();
        payload.TryGetProperty("occurred_utc", out _).ShouldBeTrue();
        payload.TryGetProperty("as_of_utc", out _).ShouldBeTrue();
        payload.TryGetProperty("requires_refresh", out _).ShouldBeTrue();
        payload.TryGetProperty("symbols", out var symbols).ShouldBeTrue();
        payload.TryGetProperty("watchlistId", out _).ShouldBeFalse();
        payload.TryGetProperty("batchSequence", out _).ShouldBeFalse();
        payload.TryGetProperty("occurredUtc", out _).ShouldBeFalse();
        payload.TryGetProperty("asOfUtc", out _).ShouldBeFalse();
        payload.TryGetProperty("requiresRefresh", out _).ShouldBeFalse();

        var symbol = symbols.EnumerateArray().First();
        symbol.TryGetProperty("symbol", out _).ShouldBeTrue();
        symbol.TryGetProperty("current_price", out _).ShouldBeTrue();
        symbol.TryGetProperty("percent_change", out _).ShouldBeTrue();
        symbol.TryGetProperty("currentPrice", out _).ShouldBeFalse();
        symbol.TryGetProperty("percentChange", out _).ShouldBeFalse();
    }

    private sealed class CookieHeaderHandler(string cookieHeader, HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("Cookie", cookieHeader);
            return base.SendAsync(request, cancellationToken);
        }
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

    private static async Task MarkIntradayBarCorrectedAsync(WebApplicationFactory<Program> factory, string symbol, Instant barTimeUtc)
    {
        using var scope = factory.Services.CreateScope();
        var marketDataDbContext = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();
        var row = await marketDataDbContext.Bars.SingleAsync(x => x.Symbol == symbol && x.Interval == IntradayMarketDataHydrationService.IntradayInterval && x.BarTimeUtc == barTimeUtc);
        row.RuntimeState = "corrected";
        row.IsReconciled = false;
        await marketDataDbContext.SaveChangesAsync();
    }

    private static async Task AssertIntradayBarExistsAsync(WebApplicationFactory<Program> factory, string symbol, Instant barTimeUtc)
    {
        using var scope = factory.Services.CreateScope();
        var marketDataDbContext = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();
        var exists = await marketDataDbContext.Bars.AnyAsync(x => x.Symbol == symbol && x.Interval == IntradayMarketDataHydrationService.IntradayInterval && x.BarTimeUtc == barTimeUtc);
        exists.ShouldBeTrue();
    }

    private static async Task AssertIntradayBarReconciledAsync(WebApplicationFactory<Program> factory, string symbol, Instant barTimeUtc)
    {
        using var scope = factory.Services.CreateScope();
        var marketDataDbContext = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();
        var row = await marketDataDbContext.Bars.SingleAsync(x => x.Symbol == symbol && x.Interval == IntradayMarketDataHydrationService.IntradayInterval && x.BarTimeUtc == barTimeUtc);
        row.RuntimeState.ShouldBe("reconciled");
        row.IsReconciled.ShouldBeTrue();
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

    private sealed class RepairFetchFailingHistoricalBarProvider : IHistoricalBarProvider
    {
        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken) =>
            new DailyHistoryHistoricalBarProvider().GetDailyBarsAsync(request, cancellationToken);

        public Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken)
        {
            if (IsRepairRequest(request))
            {
                return Task.FromResult(HistoricalBarBatchResult.Failure(request.Symbol, request.Interval, "fake", "iex", "repair_failed", "repair fetch failed"));
            }

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, request.Interval, BuildIntradayBars(request.Symbol, request.Interval), "fake", "iex"));
        }
    }

    private sealed class RepairValidationFailingHistoricalBarProvider : IHistoricalBarProvider
    {
        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken) =>
            new DailyHistoryHistoricalBarProvider().GetDailyBarsAsync(request, cancellationToken);

        public Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken)
        {
            var bars = BuildIntradayBars(request.Symbol, request.Interval);

            if (IsRepairRequest(request) && request.FromUtc.HasValue)
            {
                bars = bars.Where(bar => bar.BarTimeUtc != request.FromUtc.Value).ToArray();
            }

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, request.Interval, bars, "fake", "iex"));
        }
    }

    private sealed class MateriallyCorrectedHistoricalBarProvider : IHistoricalBarProvider
    {
        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken) =>
            new DailyHistoryHistoricalBarProvider().GetDailyBarsAsync(request, cancellationToken);

        public Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken)
        {
            var correctedBarTimeUtc = Instant.FromUtc(2026, 3, 12, 15, 0);
            var bars = BuildIntradayBars(request.Symbol, request.Interval)
                .Select(bar => IsRepairRequest(request) && bar.BarTimeUtc == correctedBarTimeUtc
                    ? bar with { Close = bar.Close + 5m, High = bar.High + 5m, RuntimeState = "reconciled", IsReconciled = true }
                    : bar)
                .ToArray();

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, request.Interval, bars, "fake", "iex"));
        }
    }

    private static bool IsRepairRequest(IntradayBarRequest request) =>
        request.FromUtc.HasValue && request.FromUtc.Value >= Instant.FromUtc(2026, 3, 12, 14, 30);

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
