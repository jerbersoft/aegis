using Aegis.Shared.Ports.MarketData;
using NodaTime;

namespace Aegis.Adapters.Alpaca.Services;

public sealed class FakeHistoricalBarProvider : IHistoricalBarProvider
{
    public Task<HistoricalBarBatchResult> GetDailyBarsAsync(HistoricalBarRequest request, CancellationToken cancellationToken)
    {
        var upperBound = request.ToUtc ?? SystemClock.Instance.GetCurrentInstant();
        var limit = Math.Max(request.Limit ?? 260, 220);
        var bars = Enumerable.Range(0, limit)
            .Select(index =>
            {
                var barTimeUtc = upperBound - Duration.FromDays(limit - index);
                var marketDate = barTimeUtc.InUtc().Date;
                var price = 100m + index;

                return new HistoricalBarRecord(
                    request.Symbol.Trim().ToUpperInvariant(),
                    "1day",
                    barTimeUtc,
                    price,
                    price + 1m,
                    price - 1m,
                    price + 0.5m,
                    1_000 + index,
                    "regular",
                    marketDate,
                    "reconciled",
                    true);
            })
            .ToArray();

        return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol.Trim().ToUpperInvariant(), "1day", bars, "fake", request.Feed ?? "iex"));
    }

    public Task<HistoricalBarBatchResult> GetIntradayBarsAsync(IntradayBarRequest request, CancellationToken cancellationToken)
    {
        var normalizedSymbol = request.Symbol.Trim().ToUpperInvariant();
        var sessionCount = Math.Max(((request.Limit ?? 4_680) + 389) / 390, 11);
        var latestSessionDate = (request.ToUtc ?? SystemClock.Instance.GetCurrentInstant()).InUtc().Date;
        var sessionDates = Enumerable.Range(0, sessionCount)
            .Select(offset => latestSessionDate.PlusDays(-(sessionCount - offset - 1)))
            .ToArray();

        var bars = sessionDates
            .SelectMany((marketDate, sessionIndex) => BuildSessionBars(normalizedSymbol, request.Interval, marketDate, sessionIndex))
            .ToArray();

        return Task.FromResult(HistoricalBarBatchResult.Success(normalizedSymbol, request.Interval, bars, "fake", request.Feed ?? "iex"));
    }

    private static IEnumerable<HistoricalBarRecord> BuildSessionBars(string symbol, string interval, LocalDate marketDate, int sessionIndex)
    {
        var sessionOpenUtc = Instant.FromUtc(marketDate.Year, marketDate.Month, marketDate.Day, 14, 30);

        for (var minuteIndex = 0; minuteIndex < 390; minuteIndex++)
        {
            var barTimeUtc = sessionOpenUtc + Duration.FromMinutes(minuteIndex);
            var price = 100m + sessionIndex + (minuteIndex / 100m);

            yield return new HistoricalBarRecord(
                symbol,
                interval,
                barTimeUtc,
                price,
                price + 0.25m,
                price - 0.25m,
                price + 0.1m,
                10_000 + (sessionIndex * 100) + minuteIndex,
                "regular",
                marketDate,
                "reconciled",
                true);
        }
    }
}
