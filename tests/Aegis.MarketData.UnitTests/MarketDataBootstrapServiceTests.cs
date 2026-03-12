using Aegis.MarketData.Application;
using Aegis.MarketData.Application.Abstractions;
using Aegis.MarketData.Infrastructure;
using Aegis.Shared.Ports.MarketData;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Shouldly;

namespace Aegis.MarketData.UnitTests;

public sealed class MarketDataBootstrapServiceTests
{
    [Fact]
    public async Task RunWarmupAsync_ShouldPersistBarsAndMarkReady_WhenProviderSucceeds()
    {
        await using var dbContext = CreateDbContext();
        var runtimeStore = new MarketDataDailyRuntimeStore();
        var demandReader = new StubDemandReader(["AAPL"]);
        var clock = new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0));
        var service = new MarketDataBootstrapService(
            dbContext,
            demandReader,
            new StubHistoricalBarProvider(),
            new MarketDataBootstrapStateStore(),
            new DailyMarketDataHydrationService(dbContext, demandReader, runtimeStore, clock),
            runtimeStore,
            clock);

        var status = await service.RunWarmupAsync(CancellationToken.None);

        status.ReadinessState.ShouldBe("ready");
        status.DailyDemandSymbolCount.ShouldBe(1);
        status.WarmedSymbolCount.ShouldBe(1);
        status.ReadySymbolCount.ShouldBe(1);
        status.NotReadySymbolCount.ShouldBe(0);
        status.PersistedBarCount.ShouldBeGreaterThanOrEqualTo(DailyMarketDataHydrationService.DailyCoreRequiredBarCount);

        var dailyBars = await service.GetDailyBarsAsync("AAPL", CancellationToken.None);
        dailyBars.ShouldNotBeNull();
        dailyBars.TotalCount.ShouldBeGreaterThanOrEqualTo(DailyMarketDataHydrationService.DailyCoreRequiredBarCount);
    }

    [Fact]
    public async Task RunWarmupAsync_ShouldMarkNotReady_WhenProviderFails()
    {
        await using var dbContext = CreateDbContext();
        var runtimeStore = new MarketDataDailyRuntimeStore();
        var demandReader = new StubDemandReader(["AAPL"]);
        var clock = new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0));
        var service = new MarketDataBootstrapService(
            dbContext,
            demandReader,
            new FailingHistoricalBarProvider(),
            new MarketDataBootstrapStateStore(),
            new DailyMarketDataHydrationService(dbContext, demandReader, runtimeStore, clock),
            runtimeStore,
            clock);

        var status = await service.RunWarmupAsync(CancellationToken.None);

        status.ReadinessState.ShouldBe("not_ready");
        status.FailedSymbols.ShouldContain("AAPL");
        status.PersistedBarCount.ShouldBe(0);
        status.ReasonCode.ShouldBe("missing_required_bars");
    }

    private static MarketDataDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseInMemoryDatabase($"market-data-unit-tests-{Guid.NewGuid():N}")
            .Options;

        return new MarketDataDbContext(options);
    }

    [Fact]
    public async Task GetDailyReadinessAsync_ShouldReturnReady_WhenSufficientHistoryExists()
    {
        await using var dbContext = CreateDbContext();
        var runtimeStore = new MarketDataDailyRuntimeStore();
        var demandReader = new StubDemandReader(["AAPL"]);
        var clock = new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0));

        SeedBars(dbContext, "AAPL", 220, clock.GetCurrentInstant());
        await dbContext.SaveChangesAsync();

        var hydrationService = new DailyMarketDataHydrationService(dbContext, demandReader, runtimeStore, clock);
        var service = new MarketDataBootstrapService(dbContext, demandReader, new StubHistoricalBarProvider(), new MarketDataBootstrapStateStore(), hydrationService, runtimeStore, clock);

        await hydrationService.RebuildAsync(cancellationToken: CancellationToken.None);
        var readiness = await service.GetDailyReadinessAsync("AAPL", CancellationToken.None);

        readiness.ShouldNotBeNull();
        readiness.ReadinessState.ShouldBe("ready");
        readiness.RequiredBarCount.ShouldBe(DailyMarketDataHydrationService.DailyCoreRequiredBarCount);
        readiness.AvailableBarCount.ShouldBe(220);
    }

    [Fact]
    public async Task GetDailyReadinessAsync_ShouldReturnNotReady_WhenHistoryIsInsufficient()
    {
        await using var dbContext = CreateDbContext();
        var runtimeStore = new MarketDataDailyRuntimeStore();
        var demandReader = new StubDemandReader(["AAPL"]);
        var clock = new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0));

        SeedBars(dbContext, "AAPL", 50, clock.GetCurrentInstant());
        await dbContext.SaveChangesAsync();

        var hydrationService = new DailyMarketDataHydrationService(dbContext, demandReader, runtimeStore, clock);
        var service = new MarketDataBootstrapService(dbContext, demandReader, new StubHistoricalBarProvider(), new MarketDataBootstrapStateStore(), hydrationService, runtimeStore, clock);

        await hydrationService.RebuildAsync(cancellationToken: CancellationToken.None);
        var readiness = await service.GetDailyReadinessAsync("AAPL", CancellationToken.None);

        readiness.ShouldNotBeNull();
        readiness.ReadinessState.ShouldBe("not_ready");
        readiness.ReasonCode.ShouldBe("missing_required_bars");
        readiness.AvailableBarCount.ShouldBe(50);
    }

    [Fact]
    public async Task RunWarmupAsync_ShouldBackfillOlderHistory_WhenPersistedBarsAreInsufficient()
    {
        await using var dbContext = CreateDbContext();
        var runtimeStore = new MarketDataDailyRuntimeStore();
        var demandReader = new StubDemandReader(["AAPL"]);
        var clock = new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0));

        SeedBars(dbContext, "AAPL", 120, clock.GetCurrentInstant());
        await dbContext.SaveChangesAsync();

        var provider = new BackfillHistoricalBarProvider();
        var service = new MarketDataBootstrapService(
            dbContext,
            demandReader,
            provider,
            new MarketDataBootstrapStateStore(),
            new DailyMarketDataHydrationService(dbContext, demandReader, runtimeStore, clock),
            runtimeStore,
            clock);

        var status = await service.RunWarmupAsync(CancellationToken.None);
        var readiness = await service.GetDailyReadinessAsync("AAPL", CancellationToken.None);

        status.ReadinessState.ShouldBe("ready");
        provider.LastRequest.ShouldNotBeNull();
        provider.LastRequest.ToUtc.ShouldNotBeNull();
        provider.LastRequest.ToUtc.Value.ShouldBe(Instant.FromUtc(2025, 1, 1, 0, 0));
        readiness.ShouldNotBeNull();
        readiness.ReadinessState.ShouldBe("ready");
        readiness.AvailableBarCount.ShouldBeGreaterThanOrEqualTo(DailyMarketDataHydrationService.DailyCoreRequiredBarCount);
    }

    private sealed class StubDemandReader(IReadOnlyList<string> symbols) : IMarketDataSymbolDemandReader
    {
        public Task<IReadOnlyList<DailySymbolDemand>> GetDailyDemandAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DailySymbolDemand>>(symbols.Select(x => new DailySymbolDemand(x, "watchlist_symbol", [DailyMarketDataHydrationService.DailyCoreProfileKey])).ToArray());
    }

    private sealed class StubHistoricalBarProvider : IHistoricalBarProvider
    {
        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken)
        {
            var bars = Enumerable.Range(0, 220)
                .Select(index =>
                {
                    var barTime = Instant.FromUtc(2025, 1, 1, 0, 0) + Duration.FromDays(index);
                    return new HistoricalBarRecord(request.Symbol, "1day", barTime, 100 + index, 105 + index, 99 + index, 104 + index, 1000 + index, "regular", barTime.InUtc().Date, "reconciled", true);
                })
                .ToArray();

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, "1day", bars, "fake", "iex"));
        }
    }

    private sealed class BackfillHistoricalBarProvider : IHistoricalBarProvider
    {
        public HistoricalBarRequest? LastRequest { get; private set; }

        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var bars = Enumerable.Range(0, 140)
                .Select(index =>
                {
                    var barTime = Instant.FromUtc(2024, 8, 1, 0, 0) + Duration.FromDays(index);
                    return new HistoricalBarRecord(request.Symbol, "1day", barTime, 100 + index, 101 + index, 99 + index, 100 + index, 1000 + index, "regular", barTime.InUtc().Date, "reconciled", true);
                })
                .ToArray();

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, "1day", bars, "fake", "iex"));
        }
    }

    private sealed class FailingHistoricalBarProvider : IHistoricalBarProvider
    {
        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(HistoricalBarBatchResult.Failure(request.Symbol, "1day", "fake", "iex", "historical_data_unavailable", "boom"));
    }

    private sealed class FakeClock(Instant now) : IClock
    {
        public Instant GetCurrentInstant() => now;
    }

    private static void SeedBars(MarketDataDbContext dbContext, string symbol, int count, Instant createdUtc)
    {
        for (var index = 0; index < count; index++)
        {
            var barTime = Instant.FromUtc(2025, 1, 1, 0, 0) + Duration.FromDays(index);
            dbContext.Bars.Add(new Aegis.MarketData.Domain.Entities.MarketDataBar
            {
                BarId = Guid.NewGuid(),
                Symbol = symbol,
                Interval = "1day",
                BarTimeUtc = barTime,
                Open = 100 + index,
                High = 101 + index,
                Low = 99 + index,
                Close = 100 + index,
                Volume = 1000 + index,
                SessionType = "regular",
                MarketDate = barTime.InUtc().Date,
                ProviderName = "fake",
                ProviderFeed = "iex",
                RuntimeState = "reconciled",
                IsReconciled = true,
                CreatedUtc = createdUtc,
                UpdatedUtc = createdUtc
            });
        }
    }
}
