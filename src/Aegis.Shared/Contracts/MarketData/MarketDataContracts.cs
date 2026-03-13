using NodaTime;

namespace Aegis.Shared.Contracts.MarketData;

public sealed record MarketDataBootstrapStatusView(
    string ReadinessState,
    string ReasonCode,
    string ProfileKey,
    int DailyDemandSymbolCount,
    int WarmedSymbolCount,
    int ReadySymbolCount,
    int NotReadySymbolCount,
    int PersistedBarCount,
    Instant AsOfUtc,
    Instant? LastWarmupUtc,
    IReadOnlyList<string> DemandSymbols,
    IReadOnlyList<string> FailedSymbols);

public sealed record DailySymbolReadinessView(
    string Symbol,
    string ProfileKey,
    Instant AsOfUtc,
    string ReadinessState,
    string ReasonCode,
    bool HasRequiredDailyBars,
    bool HasRequiredIndicatorState,
    bool HasBenchmarkDependency,
    string? BenchmarkSymbol,
    string? BenchmarkReadinessState,
    int RequiredBarCount,
    int AvailableBarCount,
    Instant? LastFinalizedBarUtc,
    Instant LastStateChangedUtc);

public sealed record DailyUniverseReadinessView(
    string ProfileKey,
    Instant AsOfUtc,
    string ReadinessState,
    string ReasonCode,
    int TotalSymbolCount,
    int ReadySymbolCount,
    int NotReadySymbolCount,
    IReadOnlyList<DailySymbolReadinessView> Symbols);

public sealed record IntradaySymbolReadinessView(
    string Symbol,
    string Interval,
    string ProfileKey,
    Instant AsOfUtc,
    string ReadinessState,
    string ReasonCode,
    bool HasRequiredIntradayBars,
    bool HasRequiredIndicatorState,
    decimal? VolumeBuzzPercent,
    bool HasRequiredVolumeBuzzReferenceHistory,
    int RequiredVolumeBuzzReferenceSessionCount,
    int AvailableVolumeBuzzReferenceSessionCount,
    int RequiredBarCount,
    int AvailableBarCount,
    Instant? LastFinalizedBarUtc,
    Instant LastStateChangedUtc,
    string? ActiveGapType,
    Instant? ActiveGapStartUtc,
    bool HasActiveRepair,
    bool PendingRecompute,
    Instant? EarliestAffectedBarUtc);

public sealed record IntradayUniverseReadinessView(
    string Interval,
    string ProfileKey,
    Instant AsOfUtc,
    string ReadinessState,
    string ReasonCode,
    int TotalSymbolCount,
    int ReadySymbolCount,
    int NotReadySymbolCount,
    int ActiveRepairSymbolCount,
    int PendingRecomputeSymbolCount,
    Instant? EarliestAffectedBarUtc,
    IReadOnlyList<IntradaySymbolReadinessView> Symbols);

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
