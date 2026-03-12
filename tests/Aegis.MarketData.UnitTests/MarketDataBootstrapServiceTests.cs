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
        var service = new MarketDataBootstrapService(
            dbContext,
            new StubDemandReader(["AAPL"]),
            new StubHistoricalBarProvider(),
            new MarketDataBootstrapStateStore(),
            new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0)));

        var status = await service.RunWarmupAsync(CancellationToken.None);

        status.ReadinessState.ShouldBe("ready");
        status.DailyDemandSymbolCount.ShouldBe(1);
        status.WarmedSymbolCount.ShouldBe(1);
        status.PersistedBarCount.ShouldBe(2);

        var dailyBars = await service.GetDailyBarsAsync("AAPL", CancellationToken.None);
        dailyBars.ShouldNotBeNull();
        dailyBars.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task RunWarmupAsync_ShouldMarkNotReady_WhenProviderFails()
    {
        await using var dbContext = CreateDbContext();
        var service = new MarketDataBootstrapService(
            dbContext,
            new StubDemandReader(["AAPL"]),
            new FailingHistoricalBarProvider(),
            new MarketDataBootstrapStateStore(),
            new FakeClock(Instant.FromUtc(2026, 3, 12, 13, 0)));

        var status = await service.RunWarmupAsync(CancellationToken.None);

        status.ReadinessState.ShouldBe("not_ready");
        status.FailedSymbols.ShouldContain("AAPL");
        status.PersistedBarCount.ShouldBe(0);
    }

    private static MarketDataDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseInMemoryDatabase($"market-data-unit-tests-{Guid.NewGuid():N}")
            .Options;

        return new MarketDataDbContext(options);
    }

    private sealed class StubDemandReader(IReadOnlyList<string> symbols) : IMarketDataSymbolDemandReader
    {
        public Task<IReadOnlyList<string>> GetDailyWarmupSymbolsAsync(CancellationToken cancellationToken) => Task.FromResult(symbols);
    }

    private sealed class StubHistoricalBarProvider : IHistoricalBarProvider
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

    private sealed class FailingHistoricalBarProvider : IHistoricalBarProvider
    {
        public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(HistoricalBarBatchResult.Failure(request.Symbol, "1day", "fake", "iex", "historical_data_unavailable", "boom"));
    }

    private sealed class FakeClock(Instant now) : IClock
    {
        public Instant GetCurrentInstant() => now;
    }
}
