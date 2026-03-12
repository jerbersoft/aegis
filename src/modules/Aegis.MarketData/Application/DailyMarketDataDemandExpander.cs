using Aegis.MarketData.Application.Abstractions;

namespace Aegis.MarketData.Application;

public static class DailyMarketDataDemandExpander
{
    public const string BenchmarkSymbol = "SPY";
    public const string BenchmarkDemandTier = "benchmark_dependency";

    public static IReadOnlyList<DailySymbolDemand> Expand(IReadOnlyList<DailySymbolDemand> demand)
    {
        // Normalize first so later merging and dependency expansion work on a stable symbol/profile shape.
        var normalized = demand
            .Select(x => new DailySymbolDemand(
                x.Symbol.Trim().ToUpperInvariant(),
                x.DemandTier,
                x.ProfileKeys.Select(profile => profile.Trim()).Where(profile => !string.IsNullOrWhiteSpace(profile)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()))
            .Where(x => !string.IsNullOrWhiteSpace(x.Symbol))
            .ToList();

        var requiresBenchmark = normalized.Any(x =>
            x.ProfileKeys.Contains(DailyMarketDataHydrationService.DailyCoreProfileKey, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(x.Symbol, BenchmarkSymbol, StringComparison.OrdinalIgnoreCase));

        if (!requiresBenchmark)
        {
            return normalized
                .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
                .Select(MergeDemand)
                .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        normalized.Add(new DailySymbolDemand(BenchmarkSymbol, BenchmarkDemandTier, [DailyMarketDataHydrationService.DailyCoreProfileKey]));

        return normalized
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(MergeDemand)
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DailySymbolDemand MergeDemand(IGrouping<string, DailySymbolDemand> grouped)
    {
        var items = grouped.ToArray();
        // Keep the primary watchlist tier if present so benchmark-only symbols do not overwrite direct demand intent.
        var demandTier = items.Any(x => !string.Equals(x.DemandTier, BenchmarkDemandTier, StringComparison.OrdinalIgnoreCase))
            ? items.First(x => !string.Equals(x.DemandTier, BenchmarkDemandTier, StringComparison.OrdinalIgnoreCase)).DemandTier
            : BenchmarkDemandTier;

        var profileKeys = items
            .SelectMany(x => x.ProfileKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DailySymbolDemand(grouped.Key, demandTier, profileKeys);
    }
}
