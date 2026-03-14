using Aegis.MarketData.Application;
using Aegis.Backend.MarketData;
using Aegis.Shared.Contracts.Common;

namespace Aegis.Backend.Endpoints;

public static class MarketDataEndpoints
{
    public static IEndpointRouteBuilder MapMarketDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/market-data").RequireAuthorization();

        group.MapGet("/bootstrap/status", async (MarketDataBootstrapService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetStatusAsync(cancellationToken)));

        group.MapPost("/bootstrap/run", async (MarketDataBootstrapService service, MarketDataRealtimeNotifier notifier, CancellationToken cancellationToken) =>
        {
            var result = await service.RunWarmupAsync(cancellationToken);
            await notifier.PublishMarketDataRefreshAsync(cancellationToken);
            return Results.Ok(result);
        });

        group.MapGet("/daily/readiness", async (MarketDataBootstrapService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetDailyReadinessAsync(cancellationToken)));

        group.MapGet("/daily/readiness/{symbol}", async (string symbol, MarketDataBootstrapService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetDailyReadinessAsync(symbol, cancellationToken);
            return result is null
                ? Results.NotFound(new ApiErrorResponse("daily_readiness_not_found", "No daily readiness was found for the requested symbol."))
                : Results.Ok(result);
        });

        group.MapGet("/intraday/readiness", async (MarketDataBootstrapService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetIntradayReadinessAsync(cancellationToken)));

        group.MapGet("/intraday/readiness/{symbol}", async (string symbol, MarketDataBootstrapService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetIntradayReadinessAsync(symbol, cancellationToken);
            return result is null
                ? Results.NotFound(new ApiErrorResponse("intraday_readiness_not_found", "No intraday readiness was found for the requested symbol."))
                : Results.Ok(result);
        });

        group.MapGet("/daily-bars/{symbol}", async (string symbol, MarketDataBootstrapService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetDailyBarsAsync(symbol, cancellationToken);
            return result is null
                ? Results.NotFound(new ApiErrorResponse("daily_bars_not_found", "No daily bars were found for the requested symbol."))
                : Results.Ok(result);
        });

        return endpoints;
    }
}
