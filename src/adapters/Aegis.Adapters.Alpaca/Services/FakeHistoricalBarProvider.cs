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
        var upperBound = request.ToUtc ?? SystemClock.Instance.GetCurrentInstant();
        var limit = Math.Max(request.Limit ?? 780, 390);
        var bars = Enumerable.Range(0, limit)
            .Select(index =>
            {
                var barTimeUtc = upperBound - Duration.FromMinutes(limit - index);
                var marketDate = barTimeUtc.InUtc().Date;
                var price = 100m + (index / 10m);
                return new HistoricalBarRecord(
                    request.Symbol.Trim().ToUpperInvariant(),
                    request.Interval,
                    barTimeUtc,
                    price,
                    price + 0.25m,
                    price - 0.25m,
                    price + 0.1m,
                    10_000 + index,
                    "regular",
                    marketDate,
                    "reconciled",
                    true);
            })
            .ToArray();

        return Task.FromResult(HistoricalBarBatchResult.Success(request.Symbol.Trim().ToUpperInvariant(), request.Interval, bars, "fake", request.Feed ?? "iex"));
    }
}
