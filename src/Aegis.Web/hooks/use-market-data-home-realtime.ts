"use client";

import { useEffect, useMemo, useState } from "react";
import { getMarketDataRealtimeClient } from "@/lib/market-data-realtime";
import { MarketDataRealtimeConnectionState } from "@/lib/types/market-data";
import { toHomeRealtimeDisabledState } from "./use-market-data-home-realtime.helpers";

type Options = {
  enabled?: boolean;
  onAuthoritativeRefresh: () => Promise<void>;
};

export function useMarketDataHomeRealtime({ enabled = true, onAuthoritativeRefresh }: Options) {
  const client = useMemo(() => getMarketDataRealtimeClient(), []);
  const [liveConnectionState, setLiveConnectionState] = useState<MarketDataRealtimeConnectionState>(client.getConnectionState());
  const [lastUnavailableError, setLastUnavailableError] = useState<string | null>(null);
  const [lastEventUtc, setLastEventUtc] = useState<string | null>(null);
  const connectionState = toHomeRealtimeDisabledState(enabled, liveConnectionState);
  const error = enabled ? lastUnavailableError : null;

  useEffect(() => {
    if (!enabled) {
      return;
    }

    const unsubscribeConnectionState = client.subscribeConnectionState((state) => {
      setLiveConnectionState(state);
      if (state === "connected" || state === "idle") {
        setLastUnavailableError(null);
      }

      if (state === "unavailable") {
        setLastUnavailableError("Realtime MarketData updates are currently unavailable. Using manual refresh.");
      }
    });

    const unsubscribeHome = client.subscribeHome((event) => {
      if (event.kind === "subscription_ack") {
        if (event.ack.requiresAuthoritativeRefresh) {
          void onAuthoritativeRefresh();
        }

        return;
      }

      setLastEventUtc(event.event.occurredUtc);
      if (event.event.requiresRefresh) {
        void onAuthoritativeRefresh();
      }
    });

    return () => {
      unsubscribeHome();
      unsubscribeConnectionState();
    };
  }, [client, enabled, onAuthoritativeRefresh]);

  return { connectionState, error, lastEventUtc };
}
