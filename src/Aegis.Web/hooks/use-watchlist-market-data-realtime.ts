"use client";

import { useEffect, useMemo, useState } from "react";
import { getMarketDataRealtimeClient } from "@/lib/market-data-realtime";
import { MarketDataRealtimeConnectionState } from "@/lib/types/market-data";
import {
  applyWatchlistSnapshot,
  emptyWatchlistRealtimeMarketState,
  toWatchlistRealtimeConnectionState,
} from "./use-watchlist-market-data-realtime.helpers";

type Options = {
  watchlistId: string | null;
  enabled?: boolean;
  onAuthoritativeRefresh: () => Promise<void>;
};

export function useWatchlistMarketDataRealtime({ watchlistId, enabled = true, onAuthoritativeRefresh }: Options) {
  const client = useMemo(() => getMarketDataRealtimeClient(), []);
  const [liveConnectionState, setLiveConnectionState] = useState<MarketDataRealtimeConnectionState>(client.getConnectionState());
  const [lastUnavailableError, setLastUnavailableError] = useState<string | null>(null);
  const [marketData, setMarketData] = useState(emptyWatchlistRealtimeMarketState);
  const connectionState = toWatchlistRealtimeConnectionState(enabled, watchlistId, liveConnectionState);
  const error = enabled && watchlistId ? lastUnavailableError : null;
  const effectiveMarketData = marketData.watchlistId === watchlistId ? marketData : emptyWatchlistRealtimeMarketState;

  useEffect(() => {
    if (!enabled || !watchlistId) {
      return;
    }
    const unsubscribeConnectionState = client.subscribeConnectionState((state) => {
      setLiveConnectionState(state);
      if (state === "connected" || state === "idle") {
        setLastUnavailableError(null);
      }

      if (state === "unavailable") {
        setLastUnavailableError("Realtime watchlist prices are unavailable. Showing the latest pulled values.");
      }
    });

    const unsubscribeWatchlist = client.subscribeWatchlist(watchlistId, (event) => {
      if (event.kind === "subscription_ack") {
        if (event.ack.requiresAuthoritativeRefresh) {
          void onAuthoritativeRefresh();
        }

        return;
      }

      setMarketData((current) => {
        return applyWatchlistSnapshot(current, watchlistId, event.event);
      });
    });

    return () => {
      unsubscribeWatchlist();
      unsubscribeConnectionState();
    };
  }, [client, enabled, onAuthoritativeRefresh, watchlistId]);

  return {
    connectionState,
    error,
    marketDataBySymbol: effectiveMarketData.bySymbol,
    asOfUtc: effectiveMarketData.asOfUtc,
  };
}
