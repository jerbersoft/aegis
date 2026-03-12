namespace Aegis.MarketData.Application.Abstractions;

public interface IMarketDataSymbolDemandReader
{
    Task<IReadOnlyList<DailySymbolDemand>> GetDailyDemandAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<IntradaySymbolDemand>> GetIntradayDemandAsync(CancellationToken cancellationToken);
}

public sealed record DailySymbolDemand(
    string Symbol,
    string DemandTier,
    IReadOnlyList<string> ProfileKeys);

public sealed record IntradaySymbolDemand(
    string Symbol,
    string Interval,
    string DemandTier,
    IReadOnlyList<string> ProfileKeys);
