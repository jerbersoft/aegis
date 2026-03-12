using Aegis.Shared.Contracts.Common;
using Aegis.Shared.Contracts.Universe;
using Aegis.Universe.Application;

namespace Aegis.Backend.Endpoints;

public static class UniverseEndpoints
{
    public static IEndpointRouteBuilder MapUniverseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/universe").RequireAuthorization();

        group.MapGet("/watchlists", async (UniverseService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetWatchlistsAsync(cancellationToken)));

        group.MapGet("/watchlists/{watchlistId:guid}", async (Guid watchlistId, UniverseService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetWatchlistByIdAsync(watchlistId, cancellationToken);
            return result is null
                ? Results.NotFound(ToApiError("watchlist_not_found", "The watchlist was not found."))
                : Results.Ok(result);
        });

        group.MapPost("/watchlists", async (CreateWatchlistRequest request, UniverseService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateWatchlistAsync(request, cancellationToken);
            return ToHttpResult(result);
        });

        group.MapPut("/watchlists/{watchlistId:guid}", async (Guid watchlistId, RenameWatchlistRequest request, UniverseService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RenameWatchlistAsync(watchlistId, request, cancellationToken);
            return ToHttpResult(result);
        });

        group.MapDelete("/watchlists/{watchlistId:guid}", async (Guid watchlistId, UniverseService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteWatchlistAsync(watchlistId, cancellationToken);
            return ToHttpResult(result);
        });

        group.MapGet("/watchlists/{watchlistId:guid}/symbols", async (
            Guid watchlistId,
            string? search,
            string? sortBy,
            string? sortDirection,
            UniverseService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetWatchlistContentsAsync(watchlistId, search, sortBy, sortDirection, cancellationToken);
            return result is null
                ? Results.NotFound(ToApiError("watchlist_not_found", "The watchlist was not found."))
                : Results.Ok(result);
        });

        group.MapPost("/watchlists/{watchlistId:guid}/symbols", async (
            Guid watchlistId,
            AddSymbolToWatchlistRequest request,
            UniverseService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.AddSymbolToWatchlistAsync(watchlistId, request, cancellationToken);
            return ToHttpResult(result);
        });

        group.MapDelete("/watchlists/{watchlistId:guid}/symbols/{symbolId:guid}", async (
            Guid watchlistId,
            Guid symbolId,
            UniverseService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RemoveSymbolFromWatchlistAsync(watchlistId, symbolId, cancellationToken);
            return ToHttpResult(result);
        });

        group.MapGet("/symbols", async (
            string? search,
            string? sortBy,
            string? sortDirection,
            Guid? watchlistId,
            bool executionOnly,
            UniverseService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetUniverseSymbolsAsync(search, sortBy, sortDirection, watchlistId, executionOnly, cancellationToken)));

        group.MapGet("/symbols/{symbolId:guid}/memberships", async (Guid symbolId, UniverseService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetSymbolMembershipsAsync(symbolId, cancellationToken);
            return result is null
                ? Results.NotFound(ToApiError("symbol_not_found", "The symbol was not found."))
                : Results.Ok(result);
        });

        group.MapGet("/execution/symbols", async (
            string? search,
            string? sortBy,
            string? sortDirection,
            UniverseService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetExecutionWatchlistSymbolsAsync(search, sortBy, sortDirection, cancellationToken)));

        group.MapGet("/execution/symbols/{symbolId:guid}/removal-blockers", async (Guid symbolId, UniverseService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetExecutionRemovalBlockersAsync(symbolId, cancellationToken);
            return result is null
                ? Results.NotFound(ToApiError("symbol_not_in_watchlist", "The symbol is not in the Execution watchlist."))
                : Results.Ok(result);
        });

        return endpoints;
    }

    private static IResult ToHttpResult(UniverseCommandResult result) =>
        result.Succeeded
            ? Results.StatusCode(result.StatusCode)
            : Results.Json(ToApiError(result.ErrorCode!, result.ErrorMessage!), statusCode: result.StatusCode);

    private static IResult ToHttpResult<T>(UniverseCommandResult<T> result) =>
        result.Succeeded
            ? Results.Json(result.Value, statusCode: result.StatusCode)
            : Results.Json(ToApiError(result.ErrorCode!, result.ErrorMessage!), statusCode: result.StatusCode);

    private static ApiErrorResponse ToApiError(string code, string message) => new(code, message);
}
