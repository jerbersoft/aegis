using Aegis.Shared.Enums;

namespace Aegis.Shared.Contracts.Universe;

public sealed record CreateWatchlistRequest(string Name);

public sealed record RenameWatchlistRequest(string Name);

public sealed record AddSymbolToWatchlistRequest(string Symbol);

public sealed record WatchlistSummaryView(
    Guid WatchlistId,
    string Name,
    string WatchlistType,
    bool IsSystem,
    bool IsExecution,
    bool CanRename,
    bool CanDelete,
    int SymbolCount,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record WatchlistDetailView(
    Guid WatchlistId,
    string Name,
    string WatchlistType,
    bool IsSystem,
    bool IsExecution,
    bool CanRename,
    bool CanDelete,
    int SymbolCount,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record WatchlistItemView(
    Guid WatchlistItemId,
    Guid WatchlistId,
    Guid SymbolId,
    string Ticker,
    string AssetClass,
    DateTimeOffset AddedUtc,
    bool IsInExecution,
    int WatchlistCount,
    decimal? CurrentPrice,
    decimal? PercentChange);

public sealed record WatchlistContentsView(
    Guid WatchlistId,
    string Name,
    string WatchlistType,
    int TotalCount,
    IReadOnlyList<WatchlistItemView> Items);

public sealed record SymbolMembershipWatchlistView(
    Guid WatchlistId,
    string Name,
    string WatchlistType,
    bool IsSystem,
    bool IsExecution,
    DateTimeOffset AddedUtc);

public sealed record SymbolMembershipView(
    Guid SymbolId,
    string Ticker,
    string AssetClass,
    bool IsInUniverse,
    bool IsInExecution,
    int WatchlistCount,
    IReadOnlyList<SymbolMembershipWatchlistView> Watchlists);

public sealed record UniverseSymbolView(
    Guid SymbolId,
    string Ticker,
    string AssetClass,
    int WatchlistCount,
    bool IsInExecution,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record UniverseSymbolsView(
    int TotalCount,
    IReadOnlyList<UniverseSymbolView> Items);

public sealed record ExecutionWatchlistSymbolView(
    Guid SymbolId,
    string Ticker,
    string AssetClass,
    DateTimeOffset AddedToExecutionUtc,
    bool HasActiveStrategy,
    bool HasOpenPosition,
    bool HasOpenOrders,
    bool CanRemoveFromExecution,
    decimal? CurrentPrice,
    decimal? PercentChange);

public sealed record ExecutionWatchlistSymbolsView(
    int TotalCount,
    IReadOnlyList<ExecutionWatchlistSymbolView> Items);

public sealed record ExecutionRemovalCheckView(
    Guid SymbolId,
    string Ticker,
    bool CanRemove,
    IReadOnlyList<string> BlockingReasonCodes);

public sealed record ExecutionRemovalBlockersView(
    Guid SymbolId,
    string Ticker,
    bool CanRemove,
    bool HasActiveStrategy,
    bool HasOpenPosition,
    bool HasOpenOrders,
    IReadOnlyList<string> BlockingReasonCodes);

public static class WatchlistConventions
{
    public const string ExecutionName = "Execution";

    public static bool IsExecution(string name) =>
        string.Equals(name, ExecutionName, StringComparison.OrdinalIgnoreCase);

    public static string ToTypeValue(WatchlistType watchlistType) => watchlistType switch
    {
        WatchlistType.System => "system",
        WatchlistType.User => "user",
        _ => throw new ArgumentOutOfRangeException(nameof(watchlistType), watchlistType, null)
    };
}
