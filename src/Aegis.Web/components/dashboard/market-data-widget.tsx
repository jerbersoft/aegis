"use client";

import { WidgetCard } from "./widget-card";
import { Button } from "@/components/ui/button";
import { useMarketDataBootstrap } from "@/hooks/use-market-data-bootstrap";

export function MarketDataWidget() {
  const { status, isLoading, isRefreshing, error, runBootstrap } = useMarketDataBootstrap();

  return (
    <WidgetCard title="MarketData Bootstrap">
      {isLoading ? (
        <p className="text-sm text-slate-400">Loading MarketData status…</p>
      ) : status ? (
        <div className="space-y-3">
          <div className="grid gap-3 sm:grid-cols-3">
            <div>
              <p className="text-xs uppercase tracking-wide text-slate-500">Readiness</p>
              <p className="text-lg font-semibold text-slate-100">{status.readinessState}</p>
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-slate-500">Demand Symbols</p>
              <p className="text-lg font-semibold text-slate-100">{status.dailyDemandSymbolCount}</p>
            </div>
            <div>
              <p className="text-xs uppercase tracking-wide text-slate-500">Persisted Bars</p>
              <p className="text-lg font-semibold text-slate-100">{status.persistedBarCount}</p>
            </div>
          </div>

          <div>
            <p className="text-xs uppercase tracking-wide text-slate-500">Demand Scope</p>
            <p className="text-sm text-slate-300">{status.demandSymbols.length > 0 ? status.demandSymbols.join(", ") : "No symbols currently require daily warmup."}</p>
          </div>

          {status.failedSymbols.length > 0 ? (
            <div>
              <p className="text-xs uppercase tracking-wide text-red-400">Failed Symbols</p>
              <p className="text-sm text-red-300">{status.failedSymbols.join(", ")}</p>
            </div>
          ) : null}

          <div className="flex items-center justify-between">
            <p className="text-xs text-slate-500">
              Last warmup: {status.lastWarmupUtc ? new Date(status.lastWarmupUtc).toLocaleString() : "Not run"}
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
