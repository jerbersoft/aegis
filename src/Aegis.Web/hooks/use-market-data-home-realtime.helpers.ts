import type { MarketDataRealtimeConnectionState } from "../lib/types/market-data";

export function toHomeRealtimeDisabledState(enabled: boolean, currentState: MarketDataRealtimeConnectionState): MarketDataRealtimeConnectionState {
  return enabled ? currentState : "idle";
}
