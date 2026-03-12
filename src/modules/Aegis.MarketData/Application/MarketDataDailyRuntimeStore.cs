using NodaTime;

namespace Aegis.MarketData.Application;

public sealed class MarketDataDailyRuntimeStore
{
    private readonly object _gate = new();
    private DailyUniverseRuntimeSnapshot _snapshot = DailyUniverseRuntimeSnapshot.Empty(SystemClock.Instance.GetCurrentInstant());

    public DailyUniverseRuntimeSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    public DailySymbolRuntimeSnapshot? GetSymbol(string symbol)
    {
        lock (_gate)
        {
            return _snapshot.Symbols.FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void SetSnapshot(DailyUniverseRuntimeSnapshot snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
        }
    }
}
