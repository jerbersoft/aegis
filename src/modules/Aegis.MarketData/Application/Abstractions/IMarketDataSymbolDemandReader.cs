namespace Aegis.MarketData.Application.Abstractions;

public interface IMarketDataSymbolDemandReader
{
    Task<IReadOnlyList<DailySymbolDemand>> GetDailyDemandAsync(CancellationToken cancellationToken);
}

public sealed record DailySymbolDemand(
    string Symbol,
    string DemandTier,
    IReadOnlyList<string> ProfileKeys);
