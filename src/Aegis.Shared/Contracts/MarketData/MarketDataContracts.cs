using NodaTime;

namespace Aegis.Shared.Contracts.MarketData;

public sealed record MarketDataBootstrapStatusView(
    string ReadinessState,
    int DailyDemandSymbolCount,
    int WarmedSymbolCount,
    int PersistedBarCount,
    Instant? LastWarmupUtc,
    IReadOnlyList<string> DemandSymbols,
    IReadOnlyList<string> FailedSymbols);

public sealed record DailyBarView(
    string Symbol,
    string Interval,
    Instant BarTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    string SessionType,
    LocalDate MarketDate,
    string ProviderName,
    string ProviderFeed,
    string RuntimeState,
    bool IsReconciled);

public sealed record DailyBarsView(
    string Symbol,
    int TotalCount,
    IReadOnlyList<DailyBarView> Items);
