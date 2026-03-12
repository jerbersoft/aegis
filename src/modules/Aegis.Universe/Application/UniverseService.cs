using Aegis.Shared.Contracts.Universe;
using Aegis.Shared.Enums;
using Aegis.Shared.Ports.MarketData;
using Aegis.Universe.Application.Abstractions;
using Aegis.Universe.Domain.Entities;
using Aegis.Universe.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Aegis.Universe.Application;

public sealed class UniverseService(
    UniverseDbContext dbContext,
    ISymbolReferenceProvider symbolReferenceProvider,
    IExecutionRemovalGuardService executionRemovalGuardService)
{
    public async Task<IReadOnlyList<WatchlistSummaryView>> GetWatchlistsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Watchlists
            .OrderByDescending(x => x.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant())
            .ThenBy(x => x.Name)
            .Select(x => new WatchlistSummaryView(
                x.WatchlistId,
                x.Name,
                WatchlistConventions.ToTypeValue(x.WatchlistType),
                x.IsSystem,
                x.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant(),
                x.IsMutable,
                x.IsMutable,
                x.WatchlistItems.Count,
                x.CreatedUtc,
                x.UpdatedUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<WatchlistDetailView?> GetWatchlistByIdAsync(Guid watchlistId, CancellationToken cancellationToken)
    {
        return await dbContext.Watchlists
            .Where(x => x.WatchlistId == watchlistId)
            .Select(x => new WatchlistDetailView(
                x.WatchlistId,
                x.Name,
                WatchlistConventions.ToTypeValue(x.WatchlistType),
                x.IsSystem,
                x.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant(),
                x.IsMutable,
                x.IsMutable,
                x.WatchlistItems.Count,
                x.CreatedUtc,
                x.UpdatedUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<WatchlistContentsView?> GetWatchlistContentsAsync(
        Guid watchlistId,
        string? search,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var watchlist = await dbContext.Watchlists
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.WatchlistId == watchlistId, cancellationToken);

        if (watchlist is null)
        {
            return null;
        }

        IQueryable<WatchlistItem> query = dbContext.WatchlistItems
            .AsNoTracking()
            .Include(x => x.Symbol)
            .Where(x => x.WatchlistId == watchlistId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Symbol.Ticker.Contains(normalizedSearch));
        }

        var sortDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLowerInvariant()) switch
        {
            "addedutc" => sortDescending
                ? query.OrderByDescending(x => x.AddedUtc)
                : query.OrderBy(x => x.AddedUtc),
            _ => sortDescending
                ? query.OrderByDescending(x => x.Symbol.Ticker)
                : query.OrderBy(x => x.Symbol.Ticker)
        };

        var rows = await query
            .Select(x => new
            {
                x.WatchlistItemId,
                x.WatchlistId,
                x.SymbolId,
                x.Symbol.Ticker,
                x.Symbol.AssetClass,
                x.AddedUtc,
                WatchlistCount = x.Symbol.WatchlistItems.Count,
                IsInExecution = x.Symbol.WatchlistItems.Any(y => y.Watchlist.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant())
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new WatchlistItemView(
                x.WatchlistItemId,
                x.WatchlistId,
                x.SymbolId,
                x.Ticker,
                x.AssetClass,
                x.AddedUtc,
                x.IsInExecution,
                x.WatchlistCount,
                null,
                null))
            .ToList();

        return new WatchlistContentsView(
            watchlist.WatchlistId,
            watchlist.Name,
            WatchlistConventions.ToTypeValue(watchlist.WatchlistType),
            items.Count,
            items);
    }

    public async Task<UniverseSymbolsView> GetUniverseSymbolsAsync(
        string? search,
        string? sortBy,
        string? sortDirection,
        Guid? watchlistId,
        bool executionOnly,
        CancellationToken cancellationToken)
    {
        IQueryable<Symbol> query = dbContext.Symbols.AsNoTracking().Where(x => x.WatchlistItems.Any());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Ticker.Contains(normalizedSearch));
        }

        if (watchlistId.HasValue)
        {
            query = query.Where(x => x.WatchlistItems.Any(y => y.WatchlistId == watchlistId.Value));
        }

        if (executionOnly)
        {
            query = query.Where(x => x.WatchlistItems.Any(y => y.Watchlist.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant()));
        }

        var sortDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLowerInvariant()) switch
        {
            "createdutc" => sortDescending ? query.OrderByDescending(x => x.CreatedUtc) : query.OrderBy(x => x.CreatedUtc),
            _ => sortDescending ? query.OrderByDescending(x => x.Ticker) : query.OrderBy(x => x.Ticker)
        };

        var items = await query
            .Select(x => new UniverseSymbolView(
                x.SymbolId,
                x.Ticker,
                x.AssetClass,
                x.WatchlistItems.Count,
                x.WatchlistItems.Any(y => y.Watchlist.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant()),
                x.CreatedUtc,
                x.UpdatedUtc))
            .ToListAsync(cancellationToken);

        return new UniverseSymbolsView(items.Count, items);
    }

    public async Task<SymbolMembershipView?> GetSymbolMembershipsAsync(Guid symbolId, CancellationToken cancellationToken)
    {
        var symbol = await dbContext.Symbols
            .AsNoTracking()
            .Where(x => x.SymbolId == symbolId)
            .Select(x => new SymbolMembershipView(
                x.SymbolId,
                x.Ticker,
                x.AssetClass,
                x.WatchlistItems.Any(),
                x.WatchlistItems.Any(y => y.Watchlist.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant()),
                x.WatchlistItems.Count,
                x.WatchlistItems
                    .OrderBy(y => y.Watchlist.Name)
                    .Select(y => new SymbolMembershipWatchlistView(
                        y.WatchlistId,
                        y.Watchlist.Name,
                        WatchlistConventions.ToTypeValue(y.Watchlist.WatchlistType),
                        y.Watchlist.IsSystem,
                        y.Watchlist.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant(),
                        y.AddedUtc))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return symbol;
    }

    public async Task<ExecutionWatchlistSymbolsView> GetExecutionWatchlistSymbolsAsync(
        string? search,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken)
    {
        var execution = await dbContext.Watchlists
            .AsNoTracking()
            .SingleAsync(x => x.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant(), cancellationToken);

        IQueryable<WatchlistItem> query = dbContext.WatchlistItems
            .AsNoTracking()
            .Include(x => x.Symbol)
            .Where(x => x.WatchlistId == execution.WatchlistId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(x => x.Symbol.Ticker.Contains(normalizedSearch));
        }

        var sortDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortBy?.ToLowerInvariant()) switch
        {
            "addedtoexecutionutc" => sortDescending ? query.OrderByDescending(x => x.AddedUtc) : query.OrderBy(x => x.AddedUtc),
            _ => sortDescending ? query.OrderByDescending(x => x.Symbol.Ticker) : query.OrderBy(x => x.Symbol.Ticker)
        };

        var rows = await query.Select(x => new
        {
            x.SymbolId,
            x.Symbol.Ticker,
            x.Symbol.AssetClass,
            x.AddedUtc
        }).ToListAsync(cancellationToken);

        var items = new List<ExecutionWatchlistSymbolView>(rows.Count);
        foreach (var row in rows)
        {
            var guardState = await executionRemovalGuardService.GetRemovalGuardStateAsync(row.SymbolId, cancellationToken);
            items.Add(new ExecutionWatchlistSymbolView(
                row.SymbolId,
                row.Ticker,
                row.AssetClass,
                row.AddedUtc,
                guardState.HasActiveStrategy,
                guardState.HasOpenPosition,
                guardState.HasOpenOrders,
                CanRemove(guardState),
                null,
                null));
        }

        return new ExecutionWatchlistSymbolsView(items.Count, items);
    }

    public async Task<ExecutionRemovalBlockersView?> GetExecutionRemovalBlockersAsync(Guid symbolId, CancellationToken cancellationToken)
    {
        var executionMembership = await dbContext.WatchlistItems
            .AsNoTracking()
            .Include(x => x.Symbol)
            .Include(x => x.Watchlist)
            .SingleOrDefaultAsync(
                x => x.SymbolId == symbolId && x.Watchlist.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant(),
                cancellationToken);

        if (executionMembership is null)
        {
            return null;
        }

        var guardState = await executionRemovalGuardService.GetRemovalGuardStateAsync(symbolId, cancellationToken);
        return ToExecutionRemovalBlockersView(executionMembership.SymbolId, executionMembership.Symbol.Ticker, guardState);
    }

    public async Task<UniverseCommandResult<WatchlistDetailView>> CreateWatchlistAsync(CreateWatchlistRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeWatchlistName(request.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return UniverseCommandResult<WatchlistDetailView>.Failure(
                "watchlist_name_conflict",
                "A watchlist name is required.",
                400);
        }

        var alreadyExists = await dbContext.Watchlists.AnyAsync(x => x.NormalizedName == normalizedName, cancellationToken);
        if (alreadyExists)
        {
            return UniverseCommandResult<WatchlistDetailView>.Failure(
                "watchlist_name_conflict",
                "A watchlist with that name already exists.",
                409);
        }

        var now = SystemClock.Instance.GetCurrentInstant();
        var watchlist = new Watchlist
        {
            WatchlistId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            NormalizedName = normalizedName,
            WatchlistType = WatchlistType.User,
            IsSystem = false,
            IsMutable = true,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        dbContext.Watchlists.Add(watchlist);
        await dbContext.SaveChangesAsync(cancellationToken);

        return UniverseCommandResult<WatchlistDetailView>.Success(
            ToWatchlistDetailView(watchlist, 0),
            UniverseStatusCodes.Created);
    }

    public async Task<UniverseCommandResult<WatchlistDetailView>> RenameWatchlistAsync(Guid watchlistId, RenameWatchlistRequest request, CancellationToken cancellationToken)
    {
        var watchlist = await dbContext.Watchlists.SingleOrDefaultAsync(x => x.WatchlistId == watchlistId, cancellationToken);
        if (watchlist is null)
        {
            return UniverseCommandResult<WatchlistDetailView>.Failure(
                "watchlist_not_found",
                "The watchlist was not found.",
                404);
        }

        if (!watchlist.IsMutable)
        {
            return UniverseCommandResult<WatchlistDetailView>.Failure(
                "watchlist_is_system_owned",
                "System watchlists cannot be renamed.",
                409);
        }

        var normalizedName = NormalizeWatchlistName(request.Name);
        var nameConflict = await dbContext.Watchlists.AnyAsync(
            x => x.WatchlistId != watchlistId && x.NormalizedName == normalizedName,
            cancellationToken);

        if (nameConflict)
        {
            return UniverseCommandResult<WatchlistDetailView>.Failure(
                "watchlist_name_conflict",
                "A watchlist with that name already exists.",
                409);
        }

        watchlist.Name = request.Name.Trim();
        watchlist.NormalizedName = normalizedName;
        watchlist.UpdatedUtc = SystemClock.Instance.GetCurrentInstant();
        await dbContext.SaveChangesAsync(cancellationToken);

        return UniverseCommandResult<WatchlistDetailView>.Success(ToWatchlistDetailView(watchlist, await dbContext.WatchlistItems.CountAsync(x => x.WatchlistId == watchlistId, cancellationToken)));
    }

    public async Task<UniverseCommandResult> DeleteWatchlistAsync(Guid watchlistId, CancellationToken cancellationToken)
    {
        var watchlist = await dbContext.Watchlists
            .Include(x => x.WatchlistItems)
            .SingleOrDefaultAsync(x => x.WatchlistId == watchlistId, cancellationToken);

        if (watchlist is null)
        {
            return UniverseCommandResult.Failure(
                "watchlist_not_found",
                "The watchlist was not found.",
                404);
        }

        if (!watchlist.IsMutable)
        {
            return UniverseCommandResult.Failure(
                "watchlist_is_system_owned",
                "System watchlists cannot be deleted.",
                409);
        }

        dbContext.Watchlists.Remove(watchlist);
        await dbContext.SaveChangesAsync(cancellationToken);

        return UniverseCommandResult.Success();
    }

    public async Task<UniverseCommandResult<WatchlistItemView>> AddSymbolToWatchlistAsync(Guid watchlistId, AddSymbolToWatchlistRequest request, CancellationToken cancellationToken)
    {
        var watchlist = await dbContext.Watchlists.SingleOrDefaultAsync(x => x.WatchlistId == watchlistId, cancellationToken);
        if (watchlist is null)
        {
            return UniverseCommandResult<WatchlistItemView>.Failure(
                "watchlist_not_found",
                "The watchlist was not found.",
                404);
        }

        var normalizedTicker = NormalizeTicker(request.Symbol);
        if (string.IsNullOrWhiteSpace(normalizedTicker))
        {
            return UniverseCommandResult<WatchlistItemView>.Failure(
                "invalid_symbol",
                "A valid symbol is required.",
                400);
        }

        var symbol = await dbContext.Symbols.SingleOrDefaultAsync(x => x.Ticker == normalizedTicker, cancellationToken);
        if (symbol is null)
        {
            var validationResult = await symbolReferenceProvider.ValidateSymbolAsync(new ValidateSymbolRequest(normalizedTicker), cancellationToken);
            if (!validationResult.IsValid)
            {
                return validationResult.ReasonCode switch
                {
                    "symbol_reference_unavailable" => UniverseCommandResult<WatchlistItemView>.Failure(
                        validationResult.ReasonCode,
                        "Symbol validation is currently unavailable.",
                        503),
                    "unsupported_asset_class" => UniverseCommandResult<WatchlistItemView>.Failure(
                        validationResult.ReasonCode,
                        "The symbol asset class is not supported.",
                        400),
                    _ => UniverseCommandResult<WatchlistItemView>.Failure(
                        "invalid_symbol",
                        "The symbol is not valid for this provider.",
                        400)
                };
            }

            var now = SystemClock.Instance.GetCurrentInstant();
            symbol = new Symbol
            {
                SymbolId = Guid.NewGuid(),
                Ticker = validationResult.NormalizedSymbol!,
                AssetClass = validationResult.AssetClass,
                IsActive = true,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            dbContext.Symbols.Add(symbol);
        }

        var existingMembership = await dbContext.WatchlistItems
            .AnyAsync(x => x.WatchlistId == watchlistId && x.SymbolId == symbol.SymbolId, cancellationToken);
        if (existingMembership)
        {
            return UniverseCommandResult<WatchlistItemView>.Failure(
                "symbol_already_in_watchlist",
                "The symbol is already in the watchlist.",
                409);
        }

        var watchlistItem = new WatchlistItem
        {
            WatchlistItemId = Guid.NewGuid(),
            WatchlistId = watchlistId,
            SymbolId = symbol.SymbolId,
            AddedUtc = SystemClock.Instance.GetCurrentInstant()
        };

        dbContext.WatchlistItems.Add(watchlistItem);
        if (dbContext.Entry(symbol).State == EntityState.Detached)
        {
            dbContext.Symbols.Attach(symbol);
        }

        symbol.UpdatedUtc = SystemClock.Instance.GetCurrentInstant();
        await dbContext.SaveChangesAsync(cancellationToken);

        var watchlistCount = await dbContext.WatchlistItems.CountAsync(x => x.SymbolId == symbol.SymbolId, cancellationToken);
        var isInExecution = await dbContext.WatchlistItems.AnyAsync(
            x => x.SymbolId == symbol.SymbolId && x.Watchlist.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant(),
            cancellationToken);

        return UniverseCommandResult<WatchlistItemView>.Success(
            new WatchlistItemView(
                watchlistItem.WatchlistItemId,
                watchlistId,
                symbol.SymbolId,
                symbol.Ticker,
                symbol.AssetClass,
                watchlistItem.AddedUtc,
                isInExecution,
                watchlistCount,
                null,
                null),
            UniverseStatusCodes.Created);
    }

    public async Task<UniverseCommandResult> RemoveSymbolFromWatchlistAsync(Guid watchlistId, Guid symbolId, CancellationToken cancellationToken)
    {
        var membership = await dbContext.WatchlistItems
            .Include(x => x.Watchlist)
            .Include(x => x.Symbol)
            .SingleOrDefaultAsync(x => x.WatchlistId == watchlistId && x.SymbolId == symbolId, cancellationToken);

        if (membership is null)
        {
            return UniverseCommandResult.Failure(
                "symbol_not_in_watchlist",
                "The symbol is not in the watchlist.",
                404);
        }

        if (membership.Watchlist.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant())
        {
            var guardState = await executionRemovalGuardService.GetRemovalGuardStateAsync(symbolId, cancellationToken);
            if (!guardState.GuardAvailable)
            {
                return UniverseCommandResult.Failure(
                    "execution_removal_guard_unavailable",
                    "Execution removal cannot be evaluated at this time.",
                    409);
            }

            if (guardState.HasActiveStrategy)
            {
                return UniverseCommandResult.Failure(
                    "execution_removal_blocked_active_strategy",
                    "The symbol cannot be removed from Execution while an active strategy is attached.",
                    409);
            }

            if (guardState.HasOpenPosition)
            {
                return UniverseCommandResult.Failure(
                    "execution_removal_blocked_open_position",
                    "The symbol cannot be removed from Execution while an open position exists.",
                    409);
            }

            if (guardState.HasOpenOrders)
            {
                return UniverseCommandResult.Failure(
                    "execution_removal_blocked_open_orders",
                    "The symbol cannot be removed from Execution while open orders exist.",
                    409);
            }

            if (guardState.HasAssignedStrategy)
            {
                var detached = await executionRemovalGuardService.DetachInactiveStrategyAssignmentAsync(symbolId, cancellationToken);
                if (!detached)
                {
                    return UniverseCommandResult.Failure(
                        "strategy_detach_failed",
                        "The inactive strategy assignment could not be detached.",
                        409);
                }
            }
        }

        dbContext.WatchlistItems.Remove(membership);
        membership.Symbol.UpdatedUtc = SystemClock.Instance.GetCurrentInstant();
        await dbContext.SaveChangesAsync(cancellationToken);
        return UniverseCommandResult.Success();
    }

    private static string NormalizeWatchlistName(string name) => name.Trim().ToUpperInvariant();

    private static string NormalizeTicker(string symbol) => symbol.Trim().ToUpperInvariant();

    private static WatchlistDetailView ToWatchlistDetailView(Watchlist watchlist, int symbolCount) =>
        new(
            watchlist.WatchlistId,
            watchlist.Name,
            WatchlistConventions.ToTypeValue(watchlist.WatchlistType),
            watchlist.IsSystem,
            watchlist.NormalizedName == WatchlistConventions.ExecutionName.ToUpperInvariant(),
            watchlist.IsMutable,
            watchlist.IsMutable,
            symbolCount,
            watchlist.CreatedUtc,
            watchlist.UpdatedUtc);

    private static bool CanRemove(ExecutionRemovalGuardState guardState) =>
        guardState.GuardAvailable && !guardState.HasActiveStrategy && !guardState.HasOpenPosition && !guardState.HasOpenOrders;

    private static ExecutionRemovalBlockersView ToExecutionRemovalBlockersView(Guid symbolId, string ticker, ExecutionRemovalGuardState guardState)
    {
        var codes = new List<string>();
        if (!guardState.GuardAvailable)
        {
            codes.Add("execution_removal_guard_unavailable");
        }
        else
        {
            if (guardState.HasActiveStrategy)
            {
                codes.Add("active_strategy_attached");
            }

            if (guardState.HasOpenPosition)
            {
                codes.Add("open_position_exists");
            }

            if (guardState.HasOpenOrders)
            {
                codes.Add("open_orders_exist");
            }
        }

        if (codes.Count == 0)
        {
            codes.Add("none");
        }

        return new ExecutionRemovalBlockersView(
            symbolId,
            ticker,
            CanRemove(guardState),
            guardState.HasActiveStrategy,
            guardState.HasOpenPosition,
            guardState.HasOpenOrders,
            codes);
    }
}
