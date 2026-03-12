using Aegis.MarketData.Application.Abstractions;
using Aegis.MarketData.Domain.Entities;
using Aegis.MarketData.Infrastructure;
using Aegis.Shared.Contracts.MarketData;
using Aegis.Shared.Ports.MarketData;
using Microsoft.EntityFrameworkCore;

namespace Aegis.MarketData.Application;

public sealed class MarketDataBootstrapService(
    MarketDataDbContext dbContext,
    IMarketDataSymbolDemandReader demandReader,
    IHistoricalBarProvider historicalBarProvider,
    MarketDataBootstrapStateStore stateStore,
    TimeProvider timeProvider)
{
    private const string DailyInterval = "1day";

    public async Task<MarketDataBootstrapStatusView> RunWarmupAsync(CancellationToken cancellationToken)
    {
        var symbols = (await demandReader.GetDailyWarmupSymbolsAsync(cancellationToken))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (symbols.Length == 0)
        {
            var emptyStatus = await BuildStatusAsync("not_requested", [], symbols, cancellationToken, null);
            stateStore.SetStatus(emptyStatus);
            return emptyStatus;
        }

        var failedSymbols = new List<string>();
        var now = timeProvider.GetUtcNow();
        var fromUtc = now.AddDays(-365);

        foreach (var symbol in symbols)
        {
            var batch = await historicalBarProvider.GetDailyBarsAsync(
                new HistoricalBarRequest(symbol, fromUtc, now, 365, "iex"),
                cancellationToken);

            if (!batch.Succeeded)
            {
                failedSymbols.Add(symbol);
                continue;
            }

            foreach (var bar in batch.Bars)
            {
                var existing = await dbContext.Bars.SingleOrDefaultAsync(
                    x => x.Symbol == bar.Symbol && x.Interval == bar.Interval && x.BarTimeUtc == bar.BarTimeUtc,
                    cancellationToken);

                if (existing is null)
                {
                    dbContext.Bars.Add(new MarketDataBar
                    {
                        BarId = Guid.NewGuid(),
                        Symbol = bar.Symbol,
                        Interval = bar.Interval,
                        BarTimeUtc = bar.BarTimeUtc,
                        Open = bar.Open,
                        High = bar.High,
                        Low = bar.Low,
                        Close = bar.Close,
                        Volume = bar.Volume,
                        SessionType = bar.SessionType,
                        MarketDate = bar.MarketDate,
                        ProviderName = batch.ProviderName,
                        ProviderFeed = batch.ProviderFeed ?? string.Empty,
                        RuntimeState = bar.RuntimeState,
                        IsReconciled = bar.IsReconciled,
                        CreatedUtc = now,
                        UpdatedUtc = now
                    });
                }
                else
                {
                    existing.Open = bar.Open;
                    existing.High = bar.High;
                    existing.Low = bar.Low;
                    existing.Close = bar.Close;
                    existing.Volume = bar.Volume;
                    existing.SessionType = bar.SessionType;
                    existing.MarketDate = bar.MarketDate;
                    existing.ProviderName = batch.ProviderName;
                    existing.ProviderFeed = batch.ProviderFeed ?? string.Empty;
                    existing.RuntimeState = bar.RuntimeState;
                    existing.IsReconciled = bar.IsReconciled;
                    existing.UpdatedUtc = now;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var readinessState = failedSymbols.Count == 0 ? "ready" : "not_ready";
        var status = await BuildStatusAsync(readinessState, failedSymbols, symbols, cancellationToken, now);
        stateStore.SetStatus(status);
        return status;
    }

    public async Task<MarketDataBootstrapStatusView> GetStatusAsync(CancellationToken cancellationToken)
    {
        var current = stateStore.GetStatus();
        var symbols = (await demandReader.GetDailyWarmupSymbolsAsync(cancellationToken))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return await BuildStatusAsync(current.ReadinessState, current.FailedSymbols, symbols, cancellationToken, current.LastWarmupUtc);
    }

    public async Task<DailyBarsView?> GetDailyBarsAsync(string symbol, CancellationToken cancellationToken)
    {
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            return null;
        }

        var items = await dbContext.Bars
            .AsNoTracking()
            .Where(x => x.Symbol == normalizedSymbol && x.Interval == DailyInterval)
            .OrderByDescending(x => x.BarTimeUtc)
            .Select(x => new DailyBarView(
                x.Symbol,
                x.Interval,
                x.BarTimeUtc,
                x.Open,
                x.High,
                x.Low,
                x.Close,
                x.Volume,
                x.SessionType,
                x.MarketDate,
                x.ProviderName,
                x.ProviderFeed,
                x.RuntimeState,
                x.IsReconciled))
            .ToListAsync(cancellationToken);

        return items.Count == 0 ? null : new DailyBarsView(normalizedSymbol, items.Count, items);
    }

    private async Task<MarketDataBootstrapStatusView> BuildStatusAsync(
        string readinessState,
        IReadOnlyCollection<string> failedSymbols,
        IReadOnlyList<string> demandSymbols,
        CancellationToken cancellationToken,
        DateTimeOffset? lastWarmupUtc)
    {
        var persistedBarCount = await dbContext.Bars.CountAsync(cancellationToken);
        var warmedSymbolCount = demandSymbols.Count == 0
            ? 0
            : await dbContext.Bars
                .AsNoTracking()
                .Where(x => x.Interval == DailyInterval && demandSymbols.Contains(x.Symbol))
                .Select(x => x.Symbol)
                .Distinct()
                .CountAsync(cancellationToken);

        return new MarketDataBootstrapStatusView(
            readinessState,
            demandSymbols.Count,
            warmedSymbolCount,
            persistedBarCount,
            lastWarmupUtc,
            demandSymbols,
            failedSymbols.Order(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
