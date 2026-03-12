"use client";

import { WidgetCard } from "./widget-card";
import { Button } from "@/components/ui/button";
import { useMarketDataBootstrap } from "@/hooks/use-market-data-bootstrap";

export function MarketDataWidget() {
  const { status, dailyReadiness, intradayReadiness, isLoading, isRefreshing, error, runBootstrap } = useMarketDataBootstrap();
  // Surface degraded symbols first so the top-level rollup and the visible detail rows stay easy to reconcile.
  const readinessSymbols = dailyReadiness
    ? [...dailyReadiness.symbols].sort((left, right) => {
        if (left.readinessState === right.readinessState) {
          return left.symbol.localeCompare(right.symbol);
        }

        if (left.readinessState === "not_ready") {
          return -1;
        }

        if (right.readinessState === "not_ready") {
          return 1;
        }

        return left.symbol.localeCompare(right.symbol);
      })
    : [];

  return (
    <WidgetCard title="MarketData Bootstrap">
      {isLoading ? (
        <p className="text-sm text-slate-400">Loading MarketData status…</p>
      ) : status ? (
        <div className="space-y-3">
            <div className="grid gap-3 sm:grid-cols-3">
              <div>
                <p className="text-xs uppercase tracking-wide text-slate-500">Readiness</p>
                <p className="text-lg font-semibold text-slate-100">{dailyReadiness?.readinessState ?? status.readinessState}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-slate-500">Ready Symbols</p>
                <p className="text-lg font-semibold text-slate-100">{dailyReadiness?.readySymbolCount ?? status.readySymbolCount}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wide text-slate-500">Not Ready Symbols</p>
                <p className="text-lg font-semibold text-slate-100">{dailyReadiness?.notReadySymbolCount ?? status.notReadySymbolCount}</p>
              </div>
            </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <div>
              <p className="text-xs uppercase tracking-wide text-slate-500">Demand Symbols</p>
              <p className="text-lg font-semibold text-slate-100">{status.dailyDemandSymbolCount}</p>
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-slate-500">Persisted Bars</p>
              <p className="text-lg font-semibold text-slate-100">{status.persistedBarCount}</p>
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-slate-500">Reason</p>
              <p className="text-sm font-semibold text-slate-200">{dailyReadiness?.reasonCode ?? status.reasonCode}</p>
            </div>
          </div>

          <div>
            <p className="text-xs uppercase tracking-wide text-slate-500">Demand Scope</p>
            <p className="text-sm text-slate-300">{status.demandSymbols.length > 0 ? status.demandSymbols.join(", ") : "No symbols currently require daily warmup."}</p>
          </div>

          {intradayReadiness && intradayReadiness.totalSymbolCount > 0 ? (
            <div>
              <p className="text-xs uppercase tracking-wide text-slate-500">Intraday Readiness</p>
              <p className="text-sm text-slate-300">
                {intradayReadiness.readySymbolCount} ready / {intradayReadiness.notReadySymbolCount} not ready ({intradayReadiness.interval})
              </p>
              <div className="mt-2 space-y-1 text-sm text-slate-300">
                {intradayReadiness.symbols.slice(0, 3).map((symbol) => (
                  <p key={`${symbol.symbol}-${symbol.interval}`}>
                    <span className="font-semibold text-slate-100">{symbol.symbol}</span>: {symbol.readinessState} ({symbol.availableBarCount}/{symbol.requiredBarCount})
                    {symbol.hasRequiredIndicatorState ? " • indicators ready" : " • indicators pending"}
                  </p>
                ))}
              </div>
            </div>
          ) : null}

          {dailyReadiness && readinessSymbols.length > 0 ? (
            <div>
              <p className="text-xs uppercase tracking-wide text-slate-500">Daily Readiness Detail</p>
              <div className="mt-2 space-y-1 text-sm text-slate-300">
                {readinessSymbols.slice(0, 5).map((symbol) => (
                  <p key={symbol.symbol}>
                    <span className="font-semibold text-slate-100">{symbol.symbol}</span>: {symbol.readinessState} ({symbol.availableBarCount}/{symbol.requiredBarCount})
                    {symbol.hasBenchmarkDependency && symbol.benchmarkSymbol ? ` • benchmark ${symbol.benchmarkSymbol}: ${symbol.benchmarkReadinessState ?? "unknown"}` : ""}
                    {symbol.hasRequiredIndicatorState ? " • indicators ready" : " • indicators pending"}
                  </p>
                ))}
              </div>
            </div>
          ) : null}

          {status.failedSymbols.length > 0 ? (
            <div>
              <p className="text-xs uppercase tracking-wide text-red-400">Failed Symbols</p>
              <p className="text-sm text-red-300">{status.failedSymbols.join(", ")}</p>
            </div>
          ) : null}

            <div className="flex items-center justify-between">
              <p className="text-xs text-slate-500">
               Last warmup: {status.lastWarmupUtc ?? "Not run"}
              </p>
              <Button onClick={() => void runBootstrap()} type="button">
                {isRefreshing ? "Refreshing…" : "Refresh"}
            </Button>
          </div>
        </div>
      ) : (
        <p className="text-sm text-slate-400">MarketData status is unavailable.</p>
      )}

      {error ? <p className="mt-3 text-sm text-red-400">{error}</p> : null}
    </WidgetCard>
  );
}
