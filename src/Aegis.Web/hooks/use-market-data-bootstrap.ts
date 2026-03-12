"use client";

import { useEffect, useState } from "react";
import { getDailyReadiness, getMarketDataBootstrapStatus, runMarketDataBootstrap } from "@/lib/api/market-data";
import { DailyUniverseReadinessView, MarketDataBootstrapStatusView } from "@/lib/types/market-data";

export function useMarketDataBootstrap() {
  const [status, setStatus] = useState<MarketDataBootstrapStatusView | null>(null);
  const [dailyReadiness, setDailyReadiness] = useState<DailyUniverseReadinessView | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    setIsLoading(true);
    setError(null);
    try {
      const [bootstrapStatus, readiness] = await Promise.all([getMarketDataBootstrapStatus(), getDailyReadiness()]);
      setStatus(bootstrapStatus);
      setDailyReadiness(readiness);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load MarketData status.");
    } finally {
      setIsLoading(false);
    }
  }

  async function runBootstrap() {
    setIsRefreshing(true);
    setError(null);
    try {
      const bootstrapStatus = await runMarketDataBootstrap();
      setStatus(bootstrapStatus);
      setDailyReadiness(await getDailyReadiness());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to run MarketData bootstrap.");
    } finally {
      setIsRefreshing(false);
    }
  }

  useEffect(() => {
    void refresh();
  }, []);

  return { status, dailyReadiness, isLoading, isRefreshing, error, refresh, runBootstrap };
}
