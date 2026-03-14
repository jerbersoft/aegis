"use client";

import { useCallback, useEffect, useState } from "react";
import { getDailyReadiness, getIntradayReadiness, getMarketDataBootstrapStatus, runMarketDataBootstrap } from "@/lib/api/market-data";
import { DailyUniverseReadinessView, IntradayUniverseReadinessView, MarketDataBootstrapStatusView } from "@/lib/types/market-data";

export function useMarketDataBootstrap() {
  const [status, setStatus] = useState<MarketDataBootstrapStatusView | null>(null);
  const [dailyReadiness, setDailyReadiness] = useState<DailyUniverseReadinessView | null>(null);
  const [intradayReadiness, setIntradayReadiness] = useState<IntradayUniverseReadinessView | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      // Bootstrap status and readiness are separate read models, so keep them in sync with one refresh call.
      const [bootstrapStatus, readiness, intraday] = await Promise.all([getMarketDataBootstrapStatus(), getDailyReadiness(), getIntradayReadiness()]);
      setStatus(bootstrapStatus);
      setDailyReadiness(readiness);
      setIntradayReadiness(intraday);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load MarketData status.");
    } finally {
      setIsLoading(false);
    }
  }, []);

  const runBootstrap = useCallback(async () => {
    setIsRefreshing(true);
    setError(null);
    try {
      // Re-read readiness after bootstrap because the write response and the derived runtime snapshot can diverge briefly.
      const bootstrapStatus = await runMarketDataBootstrap();
      setStatus(bootstrapStatus);
      setDailyReadiness(await getDailyReadiness());
      setIntradayReadiness(await getIntradayReadiness());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to run MarketData bootstrap.");
    } finally {
      setIsRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return { status, dailyReadiness, intradayReadiness, isLoading, isRefreshing, error, refresh, runBootstrap };
}
