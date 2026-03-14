using Alpaca.Markets;
using Aegis.Adapters.Alpaca.Configuration;

namespace Aegis.Adapters.Alpaca.Services;

public interface IAlpacaRealtimeClientFactory
{
    IAlpacaDataStreamingClient CreateDataStreamingClient(AlpacaRealtimeOptions options);

    IAlpacaTradingClient CreateTradingClient(AlpacaRealtimeOptions options);
}
