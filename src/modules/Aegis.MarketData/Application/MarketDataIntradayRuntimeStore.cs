using NodaTime;

namespace Aegis.MarketData.Application;

public sealed class MarketDataIntradayRuntimeStore
{
    private readonly object _gate = new();
    private IntradayUniverseRuntimeSnapshot _snapshot = IntradayUniverseRuntimeSnapshot.Empty(SystemClock.Instance.GetCurrentInstant());

    public IntradayUniverseRuntimeSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    public IntradaySymbolRuntimeSnapshot? GetSymbol(string symbol)
    {
        lock (_gate)
        {
            return _snapshot.Symbols.FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void SetSnapshot(IntradayUniverseRuntimeSnapshot snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
        }
    }
}
