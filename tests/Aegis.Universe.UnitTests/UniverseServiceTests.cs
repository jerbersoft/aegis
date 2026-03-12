using Aegis.Shared.Contracts.Universe;
using Aegis.Shared.Ports.MarketData;
using Aegis.Universe.Application;
using Aegis.Universe.Application.Abstractions;
using Aegis.Universe.Domain.Entities;
using Aegis.Universe.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Aegis.Universe.UnitTests;

public sealed class UniverseServiceTests
{
    [Fact]
    public async Task AddSymbolToWatchlist_ShouldCreateSymbolUsingNormalizedProviderValue_WhenSymbolIsNew()
    {
        await using var dbContext = CreateDbContext();
        await UniverseDbInitializer.EnsureInitializedAsync(dbContext, CancellationToken.None);

        var watchlist = new Watchlist
        {
            WatchlistId = Guid.NewGuid(),
            Name = "Growth",
            NormalizedName = "GROWTH",
            WatchlistType = Aegis.Shared.Enums.WatchlistType.User,
            IsSystem = false,
            IsMutable = true,
            CreatedUtc = NodaTime.SystemClock.Instance.GetCurrentInstant(),
            UpdatedUtc = NodaTime.SystemClock.Instance.GetCurrentInstant()
        };

        dbContext.Watchlists.Add(watchlist);
        await dbContext.SaveChangesAsync();

        var symbolReferenceProvider = Substitute.For<ISymbolReferenceProvider>();
        symbolReferenceProvider
            .ValidateSymbolAsync(Arg.Any<ValidateSymbolRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValidatedSymbolResult.Valid("AAPL", "us_equities", "fake", "Apple Inc."));

        var guardService = Substitute.For<IExecutionRemovalGuardService>();
        var service = new UniverseService(dbContext, symbolReferenceProvider, guardService);

        var result = await service.AddSymbolToWatchlistAsync(watchlist.WatchlistId, new AddSymbolToWatchlistRequest("aapl"), CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        result.StatusCode.ShouldBe(UniverseStatusCodes.Created);
        result.Value.ShouldNotBeNull();
        result.Value.Ticker.ShouldBe("AAPL");

        var persistedSymbol = await dbContext.Symbols.SingleAsync();
        persistedSymbol.Ticker.ShouldBe("AAPL");
    }

    [Fact]
    public async Task AddSymbolToWatchlist_ShouldReturnServiceUnavailable_WhenSymbolReferenceIsUnavailable()
    {
        await using var dbContext = CreateDbContext();
        await UniverseDbInitializer.EnsureInitializedAsync(dbContext, CancellationToken.None);

        var watchlist = new Watchlist
        {
            WatchlistId = Guid.NewGuid(),
            Name = "Growth",
            NormalizedName = "GROWTH",
            WatchlistType = Aegis.Shared.Enums.WatchlistType.User,
            IsSystem = false,
            IsMutable = true,
            CreatedUtc = NodaTime.SystemClock.Instance.GetCurrentInstant(),
            UpdatedUtc = NodaTime.SystemClock.Instance.GetCurrentInstant()
        };

        dbContext.Watchlists.Add(watchlist);
        await dbContext.SaveChangesAsync();

        var symbolReferenceProvider = Substitute.For<ISymbolReferenceProvider>();
        symbolReferenceProvider
            .ValidateSymbolAsync(Arg.Any<ValidateSymbolRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValidatedSymbolResult.Invalid("symbol_reference_unavailable", "fake"));

        var guardService = Substitute.For<IExecutionRemovalGuardService>();
        var service = new UniverseService(dbContext, symbolReferenceProvider, guardService);

        var result = await service.AddSymbolToWatchlistAsync(watchlist.WatchlistId, new AddSymbolToWatchlistRequest("AAPL"), CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.StatusCode.ShouldBe(503);
        result.ErrorCode.ShouldBe("symbol_reference_unavailable");
        (await dbContext.Symbols.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RemoveSymbolFromExecution_ShouldFailClosed_WhenGuardStateIsUnavailable()
    {
        await using var dbContext = CreateDbContext();
        await UniverseDbInitializer.EnsureInitializedAsync(dbContext, CancellationToken.None);

        var execution = await dbContext.Watchlists.SingleAsync(x => x.NormalizedName == "EXECUTION");
        var symbol = new Symbol
        {
            SymbolId = Guid.NewGuid(),
            Ticker = "AAPL",
            AssetClass = "us_equities",
            IsActive = true,
            CreatedUtc = NodaTime.SystemClock.Instance.GetCurrentInstant(),
            UpdatedUtc = NodaTime.SystemClock.Instance.GetCurrentInstant()
        };

        dbContext.Symbols.Add(symbol);
        dbContext.WatchlistItems.Add(new WatchlistItem
        {
            WatchlistItemId = Guid.NewGuid(),
            WatchlistId = execution.WatchlistId,
            SymbolId = symbol.SymbolId,
            AddedUtc = NodaTime.SystemClock.Instance.GetCurrentInstant()
        });
        await dbContext.SaveChangesAsync();

        var symbolReferenceProvider = Substitute.For<ISymbolReferenceProvider>();
        var guardService = Substitute.For<IExecutionRemovalGuardService>();
        guardService.GetRemovalGuardStateAsync(symbol.SymbolId, Arg.Any<CancellationToken>())
            .Returns(new ExecutionRemovalGuardState(false, false, false, false, false));

        var service = new UniverseService(dbContext, symbolReferenceProvider, guardService);

        var result = await service.RemoveSymbolFromWatchlistAsync(execution.WatchlistId, symbol.SymbolId, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.StatusCode.ShouldBe(409);
        result.ErrorCode.ShouldBe("execution_removal_guard_unavailable");
        (await dbContext.WatchlistItems.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task RemoveSymbolFromExecution_ShouldDetachInactiveStrategy_WhenRemovalIsAllowed()
    {
        await using var dbContext = CreateDbContext();
        await UniverseDbInitializer.EnsureInitializedAsync(dbContext, CancellationToken.None);

        var execution = await dbContext.Watchlists.SingleAsync(x => x.NormalizedName == "EXECUTION");
        var symbol = new Symbol
        {
            SymbolId = Guid.NewGuid(),
            Ticker = "AAPL",
            AssetClass = "us_equities",
            IsActive = true,
            CreatedUtc = NodaTime.SystemClock.Instance.GetCurrentInstant(),
            UpdatedUtc = NodaTime.SystemClock.Instance.GetCurrentInstant()
        };

        dbContext.Symbols.Add(symbol);
        dbContext.WatchlistItems.Add(new WatchlistItem
        {
            WatchlistItemId = Guid.NewGuid(),
            WatchlistId = execution.WatchlistId,
            SymbolId = symbol.SymbolId,
            AddedUtc = NodaTime.SystemClock.Instance.GetCurrentInstant()
        });
        await dbContext.SaveChangesAsync();

        var symbolReferenceProvider = Substitute.For<ISymbolReferenceProvider>();
        var guardService = Substitute.For<IExecutionRemovalGuardService>();
        guardService.GetRemovalGuardStateAsync(symbol.SymbolId, Arg.Any<CancellationToken>())
            .Returns(new ExecutionRemovalGuardState(true, true, false, false, false));
        guardService.DetachInactiveStrategyAssignmentAsync(symbol.SymbolId, Arg.Any<CancellationToken>())
            .Returns(true);

        var service = new UniverseService(dbContext, symbolReferenceProvider, guardService);

        var result = await service.RemoveSymbolFromWatchlistAsync(execution.WatchlistId, symbol.SymbolId, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        await guardService.Received(1).DetachInactiveStrategyAssignmentAsync(symbol.SymbolId, Arg.Any<CancellationToken>());
        (await dbContext.WatchlistItems.CountAsync()).ShouldBe(0);
    }

    private static UniverseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<UniverseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new UniverseDbContext(options);
    }
}
