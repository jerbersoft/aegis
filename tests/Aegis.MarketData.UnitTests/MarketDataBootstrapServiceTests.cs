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
            new IntradayMarketDataHydrationService(dbContext, demandReader, new MarketDataIntradayRuntimeStore(), clock),
            new MarketDataIntradayRuntimeStore(),
            clock);

        var status = await service.RunWarmupAsync(CancellationToken.None);

        status.ReadinessState.ShouldBe("ready");
        status.DailyDemandSymbolCount.ShouldBe(2);
        status.WarmedSymbolCount.ShouldBe(2);
        status.ReadySymbolCount.ShouldBe(2);
        status.NotReadySymbolCount.ShouldBe(0);
        status.PersistedBarCount.ShouldBeGreaterThanOrEqualTo(DailyMarketDataHydrationService.DailyCoreRequiredBarCount * 2);

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
            new IntradayMarketDataHydrationService(dbContext, demandReader, new MarketDataIntradayRuntimeStore(), clock),
            new MarketDataIntradayRuntimeStore(),
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
        SeedBars(dbContext, DailyMarketDataDemandExpander.BenchmarkSymbol, 220, clock.GetCurrentInstant());
        await dbContext.SaveChangesAsync();

        var hydrationService = new DailyMarketDataHydrationService(dbContext, demandReader, runtimeStore, clock);
        var intradayRuntimeStore = new MarketDataIntradayRuntimeStore();
        var service = new MarketDataBootstrapService(dbContext, demandReader, new StubHistoricalBarProvider(), new MarketDataBootstrapStateStore(), hydrationService, runtimeStore, new IntradayMarketDataHydrationService(dbContext, demandReader, intradayRuntimeStore, clock), intradayRuntimeStore, clock);

        await hydrationService.RebuildAsync(cancellationToken: CancellationToken.None);
        var readiness = await service.GetDailyReadinessAsync("AAPL", CancellationToken.None);

        readiness.ShouldNotBeNull();
        readiness.ReadinessState.ShouldBe("ready");
        readiness.RequiredBarCount.ShouldBe(DailyMarketDataHydrationService.DailyCoreRequiredBarCount);
        readiness.AvailableBarCount.ShouldBe(220);
        readiness.HasRequiredIndicatorState.ShouldBeTrue();
        readiness.HasBenchmarkDependency.ShouldBeTrue();
        readiness.BenchmarkSymbol.ShouldBe(DailyMarketDataDemandExpander.BenchmarkSymbol);
        readiness.BenchmarkReadinessState.ShouldBe("ready");
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
        var intradayRuntimeStore = new MarketDataIntradayRuntimeStore();
        var service = new MarketDataBootstrapService(dbContext, demandReader, new StubHistoricalBarProvider(), new MarketDataBootstrapStateStore(), hydrationService, runtimeStore, new IntradayMarketDataHydrationService(dbContext, demandReader, intradayRuntimeStore, clock), intradayRuntimeStore, clock);

        await hydrationService.RebuildAsync(cancellationToken: CancellationToken.None);
        var readiness = await service.GetDailyReadinessAsync("AAPL", CancellationToken.None);

        readiness.ShouldNotBeNull();
        readiness.ReadinessState.ShouldBe("not_ready");
        readiness.ReasonCode.ShouldBe("missing_required_bars");
        readiness.HasRequiredIndicatorState.ShouldBeFalse();
        readiness.AvailableBarCount.ShouldBe(50);
    }

    [Fact]
    public async Task GetDailyReadinessAsync_ShouldReturnBenchmarkNotReady_WhenBenchmarkHistoryIsInsufficient()
    {
        await using var dbContext = CreateDbContext();
        var runtimeStore = new MarketDataDailyRuntimeStore();
        var demandReader = new StubDemandReader(["AAPL"]);
        var clock = new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0));

        SeedBars(dbContext, "AAPL", 220, clock.GetCurrentInstant());
        SeedBars(dbContext, DailyMarketDataDemandExpander.BenchmarkSymbol, 50, clock.GetCurrentInstant());
        await dbContext.SaveChangesAsync();

        var hydrationService = new DailyMarketDataHydrationService(dbContext, demandReader, runtimeStore, clock);
        var intradayRuntimeStore = new MarketDataIntradayRuntimeStore();
        var service = new MarketDataBootstrapService(dbContext, demandReader, new StubHistoricalBarProvider(), new MarketDataBootstrapStateStore(), hydrationService, runtimeStore, new IntradayMarketDataHydrationService(dbContext, demandReader, intradayRuntimeStore, clock), intradayRuntimeStore, clock);

        await hydrationService.RebuildAsync(cancellationToken: CancellationToken.None);
        var readiness = await service.GetDailyReadinessAsync("AAPL", CancellationToken.None);

        readiness.ShouldNotBeNull();
        readiness.ReadinessState.ShouldBe("not_ready");
        readiness.ReasonCode.ShouldBe("benchmark_not_ready");
        readiness.HasRequiredIndicatorState.ShouldBeFalse();
        readiness.BenchmarkSymbol.ShouldBe(DailyMarketDataDemandExpander.BenchmarkSymbol);
        readiness.BenchmarkReadinessState.ShouldBe("not_ready");
    }

    [Fact]
    public async Task RebuildAsync_ShouldPopulateIndicatorState_WhenSymbolAndBenchmarkAreReady()
    {
        await using var dbContext = CreateDbContext();
        var runtimeStore = new MarketDataDailyRuntimeStore();
        var demandReader = new StubDemandReader(["AAPL"]);
        var clock = new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0));

        SeedBars(dbContext, "AAPL", 220, clock.GetCurrentInstant());
        SeedBars(dbContext, DailyMarketDataDemandExpander.BenchmarkSymbol, 220, clock.GetCurrentInstant());
        await dbContext.SaveChangesAsync();

        var hydrationService = new DailyMarketDataHydrationService(dbContext, demandReader, runtimeStore, clock);
        await hydrationService.RebuildAsync(cancellationToken: CancellationToken.None);

        var snapshot = runtimeStore.GetSymbol("AAPL");
        snapshot.ShouldNotBeNull();
        snapshot.IndicatorState.HasRequiredIndicatorState.ShouldBeTrue();
        snapshot.IndicatorState.Sma200.ShouldNotBeNull();
        snapshot.IndicatorState.Sma50.ShouldNotBeNull();
        snapshot.IndicatorState.Sma21.ShouldNotBeNull();
        snapshot.IndicatorState.Sma10.ShouldNotBeNull();
        snapshot.IndicatorState.Sma50Volume.ShouldNotBeNull();
        snapshot.IndicatorState.Sma21Volume.ShouldNotBeNull();
        snapshot.IndicatorState.RelVolume21.ShouldNotBeNull();
        snapshot.IndicatorState.RelVolume50.ShouldNotBeNull();
        snapshot.IndicatorState.DcrPercent.ShouldNotBeNull();
        snapshot.IndicatorState.Atr14Value.ShouldNotBeNull();
        snapshot.IndicatorState.Atr14Percent.ShouldNotBeNull();
        snapshot.IndicatorState.Adr14Value.ShouldNotBeNull();
        snapshot.IndicatorState.Adr14Percent.ShouldNotBeNull();
        snapshot.IndicatorState.Rs50.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetIntradayReadinessAsync_ShouldReturnReady_WhenExecutionHistoryExists()
    {
        await using var dbContext = CreateDbContext();
        var dailyRuntimeStore = new MarketDataDailyRuntimeStore();
        var intradayRuntimeStore = new MarketDataIntradayRuntimeStore();
        var demandReader = new StubDemandReader(["AAPL"], ["AAPL"]);
        var clock = new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0));

        SeedIntradayBars(dbContext, "AAPL", 11, 390, clock.GetCurrentInstant());
        await dbContext.SaveChangesAsync();

        var service = new MarketDataBootstrapService(
            dbContext,
            demandReader,
            new StubHistoricalBarProvider(),
            new MarketDataBootstrapStateStore(),
            new DailyMarketDataHydrationService(dbContext, demandReader, dailyRuntimeStore, clock),
            dailyRuntimeStore,
            new IntradayMarketDataHydrationService(dbContext, demandReader, intradayRuntimeStore, clock),
            intradayRuntimeStore,
            clock);

        await service.GetStatusAsync(CancellationToken.None);
        var readiness = await service.GetIntradayReadinessAsync("AAPL", CancellationToken.None);

        readiness.ShouldNotBeNull();
        readiness.ReadinessState.ShouldBe("ready");
        readiness.HasRequiredIndicatorState.ShouldBeTrue();
        readiness.HasRequiredVolumeBuzzReferenceHistory.ShouldBeTrue();
        readiness.AvailableVolumeBuzzReferenceSessionCount.ShouldBe(IntradayMarketDataHydrationService.VolumeBuzzReferenceSessionCount);
        readiness.VolumeBuzzPercent.ShouldNotBeNull();
        readiness.AvailableBarCount.ShouldBeGreaterThanOrEqualTo(IntradayMarketDataHydrationService.IntradayRequiredBarCount);
    }

    [Fact]
    public async Task GetIntradayReadinessAsync_ShouldReturnNotReady_WhenVolumeBuzzReferenceHistoryIsInsufficient()
    {
        await using var dbContext = CreateDbContext();
        var dailyRuntimeStore = new MarketDataDailyRuntimeStore();
        var intradayRuntimeStore = new MarketDataIntradayRuntimeStore();
        var demandReader = new StubDemandReader(["AAPL"], ["AAPL"]);
        var clock = new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0));

        SeedIntradayBars(dbContext, "AAPL", 2, 390, clock.GetCurrentInstant());
        await dbContext.SaveChangesAsync();

        var service = new MarketDataBootstrapService(
            dbContext,
            demandReader,
            new StubHistoricalBarProvider(),
            new MarketDataBootstrapStateStore(),
            new DailyMarketDataHydrationService(dbContext, demandReader, dailyRuntimeStore, clock),
            dailyRuntimeStore,
            new IntradayMarketDataHydrationService(dbContext, demandReader, intradayRuntimeStore, clock),
            intradayRuntimeStore,
            clock);

        await service.GetStatusAsync(CancellationToken.None);
        var readiness = await service.GetIntradayReadinessAsync("AAPL", CancellationToken.None);

        readiness.ShouldNotBeNull();
        readiness.ReadinessState.ShouldBe("not_ready");
        readiness.ReasonCode.ShouldBe("insufficient_volume_buzz_reference_history");
        readiness.HasRequiredIntradayBars.ShouldBeTrue();
        readiness.HasRequiredIndicatorState.ShouldBeFalse();
        readiness.HasRequiredVolumeBuzzReferenceHistory.ShouldBeFalse();
        readiness.AvailableVolumeBuzzReferenceSessionCount.ShouldBe(1);
        readiness.VolumeBuzzPercent.ShouldBeNull();
    }

    [Fact]
    public async Task GetIntradayReadinessAsync_ShouldComputeVolumeBuzzPercent_UsingSessionOffsetMatchedReferenceCurves()
    {
        await using var dbContext = CreateDbContext();
        var dailyRuntimeStore = new MarketDataDailyRuntimeStore();
        var intradayRuntimeStore = new MarketDataIntradayRuntimeStore();
        var demandReader = new StubDemandReader(["AAPL"], ["AAPL"]);
        var clock = new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0));

        SeedIntradayBarsWithSessionOffsetVolumes(
            dbContext,
            "AAPL",
            referenceSessionCount: IntradayMarketDataHydrationService.VolumeBuzzReferenceSessionCount,
            barsPerReferenceSession: 3,
            currentSessionVolumes: [100L, 200L, 300L],
            referenceSessionVolumes: [
                [10L, 20L, 30L], [10L, 20L, 30L], [10L, 20L, 30L], [10L, 20L, 30L], [10L, 20L, 30L],
                [10L, 20L, 30L], [10L, 20L, 30L], [10L, 20L, 30L], [10L, 20L, 30L], [10L, 20L, 30L]
            ],
            createdUtc: clock.GetCurrentInstant());
        await dbContext.SaveChangesAsync();

        var service = new MarketDataBootstrapService(
            dbContext,
            demandReader,
            new StubHistoricalBarProvider(),
            new MarketDataBootstrapStateStore(),
            new DailyMarketDataHydrationService(dbContext, demandReader, dailyRuntimeStore, clock),
            dailyRuntimeStore,
            new IntradayMarketDataHydrationService(dbContext, demandReader, intradayRuntimeStore, clock),
            intradayRuntimeStore,
            clock);

        await service.GetStatusAsync(CancellationToken.None);
        var readiness = await service.GetIntradayReadinessAsync("AAPL", CancellationToken.None);

        readiness.ShouldNotBeNull();
        readiness.ReadinessState.ShouldBe("not_ready");
        readiness.HasRequiredVolumeBuzzReferenceHistory.ShouldBeTrue();
        readiness.AvailableVolumeBuzzReferenceSessionCount.ShouldBe(IntradayMarketDataHydrationService.VolumeBuzzReferenceSessionCount);
        readiness.VolumeBuzzPercent.ShouldBe(1000m);
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
        var intradayRuntimeStore = new MarketDataIntradayRuntimeStore();
        var service = new MarketDataBootstrapService(
            dbContext,
            demandReader,
            provider,
            new MarketDataBootstrapStateStore(),
            new DailyMarketDataHydrationService(dbContext, demandReader, runtimeStore, clock),
            runtimeStore,
            new IntradayMarketDataHydrationService(dbContext, demandReader, intradayRuntimeStore, clock),
            intradayRuntimeStore,
            clock);

        var status = await service.RunWarmupAsync(CancellationToken.None);
        var readiness = await service.GetDailyReadinessAsync("AAPL", CancellationToken.None);

        status.ReadinessState.ShouldBe("ready");
        provider.Requests.ShouldContain(x => x.Symbol == "AAPL" && x.ToUtc == Instant.FromUtc(2025, 1, 1, 0, 0));
        readiness.ShouldNotBeNull();
        readiness.ReadinessState.ShouldBe("ready");
        readiness.AvailableBarCount.ShouldBeGreaterThanOrEqualTo(DailyMarketDataHydrationService.DailyCoreRequiredBarCount);
    }

    private sealed class StubDemandReader(IReadOnlyList<string> symbols, IReadOnlyList<string>? intradaySymbols = null) : IMarketDataSymbolDemandReader
    {
        public Task<IReadOnlyList<DailySymbolDemand>> GetDailyDemandAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DailySymbolDemand>>(symbols.Select(x => new DailySymbolDemand(x, "watchlist_symbol", [DailyMarketDataHydrationService.DailyCoreProfileKey])).ToArray());

        public Task<IReadOnlyList<IntradaySymbolDemand>> GetIntradayDemandAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IntradaySymbolDemand>>((intradaySymbols ?? []).Select(x => new IntradaySymbolDemand(x, IntradayMarketDataHydrationService.IntradayInterval, "execution_symbol", [IntradayMarketDataHydrationService.IntradayCoreProfileKey])).ToArray());
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

        public Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken)
        {
            var bars = BuildIntradayBars(request.Symbol, request.Interval, 11, 390);

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, request.Interval, bars, "fake", "iex"));
        }
    }

    private sealed class BackfillHistoricalBarProvider : IHistoricalBarProvider
    {
        public List<HistoricalBarRequest> Requests { get; } = [];

        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var count = string.Equals(request.Symbol, DailyMarketDataDemandExpander.BenchmarkSymbol, StringComparison.OrdinalIgnoreCase) ? 220 : 140;
            var start = string.Equals(request.Symbol, DailyMarketDataDemandExpander.BenchmarkSymbol, StringComparison.OrdinalIgnoreCase)
                ? Instant.FromUtc(2025, 1, 1, 0, 0)
                : Instant.FromUtc(2024, 8, 1, 0, 0);

            var bars = Enumerable.Range(0, count)
                .Select(index =>
                {
                    var barTime = start + Duration.FromDays(index);
                    return new HistoricalBarRecord(request.Symbol, "1day", barTime, 100 + index, 101 + index, 99 + index, 100 + index, 1000 + index, "regular", barTime.InUtc().Date, "reconciled", true);
                })
                .ToArray();

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, "1day", bars, "fake", "iex"));
        }

        public Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken)
        {
            var bars = BuildIntradayBars(request.Symbol, request.Interval, 11, 390);

            return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol, request.Interval, bars, "fake", "iex"));
        }
    }

    private sealed class FailingHistoricalBarProvider : IHistoricalBarProvider
    {
        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(HistoricalBarBatchResult.Failure(request.Symbol, "1day", "fake", "iex", "historical_data_unavailable", "boom"));

        public Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(HistoricalBarBatchResult.Failure(request.Symbol, request.Interval, "fake", "iex", "historical_data_unavailable", "boom"));
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

    private static void SeedIntradayBars(MarketDataDbContext dbContext, string symbol, int sessionCount, int barsPerSession, Instant createdUtc)
    {
        foreach (var bar in BuildIntradayBars(symbol, IntradayMarketDataHydrationService.IntradayInterval, sessionCount, barsPerSession))
        {
            dbContext.Bars.Add(new Aegis.MarketData.Domain.Entities.MarketDataBar
            {
                BarId = Guid.NewGuid(),
                Symbol = symbol,
                Interval = IntradayMarketDataHydrationService.IntradayInterval,
                BarTimeUtc = bar.BarTimeUtc,
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                Volume = bar.Volume,
                SessionType = bar.SessionType,
                MarketDate = bar.MarketDate,
                ProviderName = bar.Symbol == symbol ? "fake" : "unexpected",
                ProviderFeed = "iex",
                RuntimeState = bar.RuntimeState,
                IsReconciled = bar.IsReconciled,
                CreatedUtc = createdUtc,
                UpdatedUtc = createdUtc
            });
        }
    }

    private static void SeedIntradayBarsWithSessionOffsetVolumes(
        MarketDataDbContext dbContext,
        string symbol,
        int referenceSessionCount,
        int barsPerReferenceSession,
        IReadOnlyList<long> currentSessionVolumes,
        IReadOnlyList<IReadOnlyList<long>> referenceSessionVolumes,
        Instant createdUtc)
    {
        var startDate = new LocalDate(2026, 3, 1);

        for (var sessionIndex = 0; sessionIndex < referenceSessionCount; sessionIndex++)
        {
            AddIntradaySession(dbContext, symbol, startDate.PlusDays(sessionIndex), referenceSessionVolumes[sessionIndex], createdUtc, sessionIndex);
        }

        AddIntradaySession(dbContext, symbol, startDate.PlusDays(referenceSessionCount), currentSessionVolumes, createdUtc, referenceSessionCount);
    }

    private static void AddIntradaySession(MarketDataDbContext dbContext, string symbol, LocalDate marketDate, IReadOnlyList<long> volumes, Instant createdUtc, int sessionIndex)
    {
        var sessionOpen = Instant.FromUtc(marketDate.Year, marketDate.Month, marketDate.Day, 14, 30);
        for (var minuteIndex = 0; minuteIndex < volumes.Count; minuteIndex++)
        {
            var barTime = sessionOpen + Duration.FromMinutes(minuteIndex);
            var price = 100m + sessionIndex + (minuteIndex / 10m);
            dbContext.Bars.Add(new Aegis.MarketData.Domain.Entities.MarketDataBar
            {
                BarId = Guid.NewGuid(),
                Symbol = symbol,
                Interval = IntradayMarketDataHydrationService.IntradayInterval,
                BarTimeUtc = barTime,
                Open = price,
                High = price + 1m,
                Low = price - 1m,
                Close = price + 0.25m,
                Volume = volumes[minuteIndex],
                SessionType = "regular",
                MarketDate = marketDate,
                ProviderName = "fake",
                ProviderFeed = "iex",
                RuntimeState = "reconciled",
                IsReconciled = true,
                CreatedUtc = createdUtc,
                UpdatedUtc = createdUtc
            });
        }
    }

    private static HistoricalBarRecord[] BuildIntradayBars(string symbol, string interval, int sessionCount, int barsPerSession)
    {
        var startDate = new LocalDate(2026, 3, 2);
        return Enumerable.Range(0, sessionCount)
            .SelectMany(sessionIndex =>
            {
                var marketDate = startDate.PlusDays(sessionIndex);
                var sessionOpen = Instant.FromUtc(marketDate.Year, marketDate.Month, marketDate.Day, 14, 30);

                return Enumerable.Range(0, barsPerSession).Select(minuteIndex =>
                {
                    var barTime = sessionOpen + Duration.FromMinutes(minuteIndex);
                    var price = 100m + sessionIndex + (minuteIndex / 100m);
                    return new HistoricalBarRecord(symbol, interval, barTime, price, price + 1m, price - 1m, price + 0.25m, 1_000 + (sessionIndex * 100) + minuteIndex, "regular", marketDate, "reconciled", true);
                });
            })
            .ToArray();
    }
}
