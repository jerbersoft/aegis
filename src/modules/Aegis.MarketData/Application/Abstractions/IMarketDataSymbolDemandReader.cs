namespace Aegis.MarketData.Application.Abstractions;

public interface IMarketDataSymbolDemandReader
{
    Task<IReadOnlyList<string>> GetDailyWarmupSymbolsAsync(CancellationToken cancellationToken);
}
