using Aegis.MarketData.Application.Abstractions;
using Aegis.MarketData.Infrastructure;
using Aegis.Shared.Contracts.MarketData;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Aegis.MarketData.Application;

public sealed class IntradayMarketDataHydrationService(
    MarketDataDbContext dbContext,
    IMarketDataSymbolDemandReader demandReader,
    MarketDataIntradayRuntimeStore runtimeStore,
    IClock clock)
{
    private static readonly DateTimeZone MarketTimeZone = DateTimeZoneProviders.Tzdb["America/New_York"];
    public const string IntradayCoreProfileKey = "intraday_core";
    public const string IntradayInterval = "1min";
    public const int IntradayRequiredBarCount = 100;
    public const int IntradaySessionBarCount = 390;
    public const int VolumeBuzzReferenceSessionCount = 10;

    public async Task<IntradayUniverseRuntimeSnapshot> RebuildAsync(string? overrideReadinessState = null, string? overrideReasonCode = null, CancellationToken cancellationToken = default)
    {
        var asOfUtc = clock.GetCurrentInstant();
        var demand = await demandReader.GetIntradayDemandAsync(cancellationToken);
        var intradayDemand = demand
            .Where(x => string.Equals(x.Interval, IntradayInterval, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (intradayDemand.Length == 0)
        {
            var empty = new IntradayUniverseRuntimeSnapshot(IntradayInterval, IntradayCoreProfileKey, asOfUtc, overrideReadinessState ?? "not_requested", overrideReasonCode ?? "none", []);
            runtimeStore.SetSnapshot(empty);
            return empty;
        }

        var symbols = intradayDemand.Select(x => x.Symbol.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var rows = await dbContext.Bars
            .AsNoTracking()
            .Where(x => x.Interval == IntradayInterval && symbols.Contains(x.Symbol))
            .OrderBy(x => x.Symbol)
            .ThenBy(x => x.BarTimeUtc)
            .Select(x => new DailyBarView(
                x.Symbol,
                x.Interval,
                x.BarTimeUtc,
                x.Open,
                x.High,
                x.Low,
                x.Close,
                x.Volume,
                x.SessionType,
                x.MarketDate,
                x.ProviderName,
                x.ProviderFeed,
                x.RuntimeState,
                x.IsReconciled))
            .ToListAsync(cancellationToken);

        var grouped = rows
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<DailyBarView>)x.ToArray(), StringComparer.OrdinalIgnoreCase);

        var snapshots = symbols
            .Select(symbol => BuildSnapshot(symbol, grouped.TryGetValue(symbol, out var bars) ? bars : [], asOfUtc))
            .ToArray();

        var rollupReadiness = overrideReadinessState ?? (snapshots.Any(x => x.ReadinessState == "not_ready") ? "not_ready" : "ready");
        var rollupReason = overrideReasonCode ?? (snapshots.FirstOrDefault(x => x.ReadinessState == "not_ready")?.ReasonCode ?? "none");
        var snapshot = new IntradayUniverseRuntimeSnapshot(IntradayInterval, IntradayCoreProfileKey, asOfUtc, rollupReadiness, rollupReason, snapshots);
        runtimeStore.SetSnapshot(snapshot);
        return snapshot;
    }

    private static IReadOnlyList<DailyBarView> FilterToCurrentAndPriorSession(IReadOnlyList<DailyBarView> bars)
    {
        var marketDates = bars.Select(x => x.MarketDate).Distinct().OrderByDescending(x => x).Take(2).ToArray();
        return bars.Where(x => marketDates.Contains(x.MarketDate)).ToArray();
    }

    private static IntradaySymbolRuntimeSnapshot BuildSnapshot(string symbol, IReadOnlyList<DailyBarView> allBars, Instant asOfUtc)
    {
        var runtimeBars = FilterToCurrentAndPriorSession(allBars);
        var gapState = BuildGapState(runtimeBars, asOfUtc);
        var indicatorState = BuildIndicatorState(allBars, gapState);
        var availableBarCount = runtimeBars.Count;
        var hasRequiredBars = availableBarCount >= IntradayRequiredBarCount;
        var readinessState = hasRequiredBars && !gapState.HasGap && indicatorState.HasRequiredIndicatorState ? "ready" : "not_ready";
        var reasonCode = !hasRequiredBars
            ? "missing_required_intraday_bars"
            : gapState.ReasonCode is not null
                ? gapState.ReasonCode
            : indicatorState.VolumeBuzzReferenceState.HasRequiredReferenceHistory
                ? indicatorState.HasRequiredIndicatorState ? "none" : "awaiting_recompute"
                : "insufficient_volume_buzz_reference_history";

        return new IntradaySymbolRuntimeSnapshot(
            symbol,
            IntradayInterval,
            IntradayCoreProfileKey,
            IntradayRequiredBarCount,
            availableBarCount,
            runtimeBars.LastOrDefault()?.BarTimeUtc,
            readinessState,
            reasonCode,
            asOfUtc,
            indicatorState,
            runtimeBars,
            gapState.ActiveGapType,
            gapState.ActiveGapStartUtc);
    }

    private static IntradayComputedIndicatorState BuildIndicatorState(IReadOnlyList<DailyBarView> allBars, IntradayGapState gapState)
    {
        var runtimeBars = FilterToCurrentAndPriorSession(allBars);
        var ema30 = CalculateEma(runtimeBars, 30);
        var ema100 = CalculateEma(runtimeBars, 100);
        var volumeBuzzReferenceState = BuildVolumeBuzzReferenceState(allBars);
        var vwap = CalculateVwap(runtimeBars);
        var hasRequiredIndicatorState = !gapState.HasGap
                                        && ema30.HasValue
                                         && ema100.HasValue
                                         && volumeBuzzReferenceState.HasRequiredReferenceHistory
                                         && vwap.HasValue;

        return new IntradayComputedIndicatorState(ema30, ema100, volumeBuzzReferenceState.HasRequiredReferenceHistory ? CalculateVolumeBuzzPercent(volumeBuzzReferenceState) : null, vwap, hasRequiredIndicatorState, volumeBuzzReferenceState);
    }

    private static IntradayGapState BuildGapState(IReadOnlyList<DailyBarView> runtimeBars, Instant asOfUtc)
    {
        if (runtimeBars.Count == 0)
        {
            return IntradayGapState.None;
        }

        var retainedSessions = runtimeBars
            .GroupBy(x => x.MarketDate)
            .OrderBy(x => x.Key)
            .Select(group => group.OrderBy(x => x.BarTimeUtc).ToArray())
            .ToArray();

        if (retainedSessions.Length == 0)
        {
            return IntradayGapState.None;
        }

        var latestRetainedDate = retainedSessions[^1][0].MarketDate;
        var expectedBarTimes = retainedSessions
            .SelectMany(sessionBars => BuildExpectedBarTimes(sessionBars, latestRetainedDate, asOfUtc))
            .OrderBy(x => x)
            .ToArray();

        if (expectedBarTimes.Length == 0)
        {
            return IntradayGapState.None;
        }

        var actualBarTimes = runtimeBars
            .Select(x => x.BarTimeUtc)
            .ToHashSet();

        var missingBarTimes = expectedBarTimes
            .Where(barTime => !actualBarTimes.Contains(barTime))
            .ToArray();

        if (missingBarTimes.Length == 0)
        {
            return IntradayGapState.None;
        }

        var firstMissingIndex = Array.FindIndex(expectedBarTimes, barTime => !actualBarTimes.Contains(barTime));
        var trailingGap = firstMissingIndex >= 0
                          && expectedBarTimes
                              .Skip(firstMissingIndex)
                              .All(barTime => !actualBarTimes.Contains(barTime));

        return new IntradayGapState(
            trailingGap ? "trailing" : "internal",
            trailingGap ? "gap_trailing" : "gap_internal",
            missingBarTimes[0]);
    }

    private static IReadOnlyList<Instant> BuildExpectedBarTimes(IReadOnlyList<DailyBarView> sessionBars, LocalDate latestRetainedDate, Instant asOfUtc)
    {
        if (sessionBars.Count == 0)
        {
            return [];
        }

        var marketDate = sessionBars[0].MarketDate;
        var sessionOpenUtc = sessionBars[0].BarTimeUtc;
        var sessionCloseUtc = sessionOpenUtc + Duration.FromMinutes(IntradaySessionBarCount);
        var expectedEndExclusiveUtc = marketDate == latestRetainedDate
            ? DetermineCurrentSessionEndExclusive(sessionBars, marketDate, sessionCloseUtc, asOfUtc)
            : sessionCloseUtc;

        if (expectedEndExclusiveUtc <= sessionOpenUtc)
        {
            return [];
        }

        var expectedBarCount = (int)((expectedEndExclusiveUtc - sessionOpenUtc).TotalMinutes);
        return Enumerable.Range(0, expectedBarCount)
            .Select(offset => sessionOpenUtc + Duration.FromMinutes(offset))
            .ToArray();
    }

    private static Instant DetermineCurrentSessionEndExclusive(IReadOnlyList<DailyBarView> sessionBars, LocalDate marketDate, Instant sessionCloseUtc, Instant asOfUtc)
    {
        var currentMarketDate = asOfUtc.InZone(MarketTimeZone).Date;
        if (marketDate < currentMarketDate)
        {
            return sessionCloseUtc;
        }

        var lastObservedBarUtc = sessionBars[^1].BarTimeUtc + Duration.FromMinutes(1);
        if (asOfUtc >= sessionCloseUtc)
        {
            return sessionCloseUtc;
        }

        return MinInstant(sessionCloseUtc, MaxInstant(lastObservedBarUtc, FloorToMinute(asOfUtc)));
    }

    private static Instant FloorToMinute(Instant instant)
    {
        var secondsSinceEpoch = instant.ToUnixTimeSeconds();
        return Instant.FromUnixTimeSeconds(secondsSinceEpoch - (secondsSinceEpoch % 60));
    }

    private static Instant MinInstant(Instant left, Instant right) => left <= right ? left : right;

    private static Instant MaxInstant(Instant left, Instant right) => left >= right ? left : right;

    private static IntradayVolumeBuzzReferenceState BuildVolumeBuzzReferenceState(IReadOnlyList<DailyBarView> bars)
    {
        if (bars.Count == 0)
        {
            return new IntradayVolumeBuzzReferenceState(VolumeBuzzReferenceSessionCount, 0, null, null, null, []);
        }

        var orderedSessions = bars
            .GroupBy(x => x.MarketDate)
            .OrderBy(x => x.Key)
            .Select(group => group.OrderBy(x => x.BarTimeUtc).ToArray())
            .ToArray();

        if (orderedSessions.Length == 0)
        {
            return new IntradayVolumeBuzzReferenceState(VolumeBuzzReferenceSessionCount, 0, null, null, null, []);
        }

        var currentSession = orderedSessions[^1];
        var currentSessionOffset = currentSession.Length - 1;
        if (currentSessionOffset < 0)
        {
            return new IntradayVolumeBuzzReferenceState(VolumeBuzzReferenceSessionCount, 0, null, null, null, []);
        }

        var currentSessionCumulativeVolume = currentSession.Sum(x => x.Volume);
        // Volume buzz compares cumulative volume at the current session offset, so each reference session only contributes when it has reached the same closed-bar offset.
        var historicalCurves = orderedSessions
            .Take(Math.Max(0, orderedSessions.Length - 1))
            .Reverse()
            .Select(BuildCumulativeCurve)
            .Where(curve => curve.Count > currentSessionOffset)
            .Take(VolumeBuzzReferenceSessionCount)
            .Select(curve => (IReadOnlyList<long>)curve)
            .ToArray();

        decimal? historicalAverage = historicalCurves.Length == 0
            ? null
            : historicalCurves.Average(curve => (decimal)curve[currentSessionOffset]);

        return new IntradayVolumeBuzzReferenceState(
            VolumeBuzzReferenceSessionCount,
            historicalCurves.Length,
            currentSessionOffset,
            currentSessionCumulativeVolume,
            historicalAverage,
            historicalCurves);
    }

    private static decimal? CalculateVolumeBuzzPercent(IntradayVolumeBuzzReferenceState referenceState)
    {
        if (!referenceState.CurrentSessionCumulativeVolume.HasValue
            || !referenceState.HistoricalAverageCumulativeVolumeAtOffset.HasValue
            || referenceState.HistoricalAverageCumulativeVolumeAtOffset.Value == 0)
        {
            return null;
        }

        return (referenceState.CurrentSessionCumulativeVolume.Value / referenceState.HistoricalAverageCumulativeVolumeAtOffset.Value) * 100m;
    }

    private static IReadOnlyList<long> BuildCumulativeCurve(IReadOnlyList<DailyBarView> bars)
    {
        var cumulativeCurve = new long[bars.Count];
        long runningVolume = 0;

        for (var index = 0; index < bars.Count; index++)
        {
            runningVolume += bars[index].Volume;
            cumulativeCurve[index] = runningVolume;
        }

        return cumulativeCurve;
    }

    private static decimal? CalculateEma(IReadOnlyList<DailyBarView> bars, int period)
    {
        if (bars.Count < period)
        {
            return null;
        }

        var window = bars.ToArray();
        var multiplier = 2m / (period + 1m);
        var ema = window.Take(period).Average(x => x.Close);

        for (var index = period; index < window.Length; index++)
        {
            ema = ((window[index].Close - ema) * multiplier) + ema;
        }

        return ema;
    }

    private static decimal? CalculateVwap(IReadOnlyList<DailyBarView> bars)
    {
        if (bars.Count == 0)
        {
            return null;
        }

        decimal weightedPriceVolume = 0;
        long totalVolume = 0;

        foreach (var bar in bars)
        {
            var typicalPrice = (bar.High + bar.Low + bar.Close) / 3m;
            weightedPriceVolume += typicalPrice * bar.Volume;
            totalVolume += bar.Volume;
        }

        return totalVolume == 0 ? null : weightedPriceVolume / totalVolume;
    }
}
