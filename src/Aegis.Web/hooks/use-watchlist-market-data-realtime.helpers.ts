import type { MarketDataRealtimeConnectionState, MarketDataWatchlistSymbolSnapshotView } from "../lib/types/market-data";

export type WatchlistRealtimeMarketState = {
  watchlistId: string | null;
  bySymbol: Record<string, MarketDataWatchlistSymbolSnapshotView>;
  asOfUtc: string | null;
  batchSequence: number | null;
};

export const emptyWatchlistRealtimeMarketState: WatchlistRealtimeMarketState = {
  watchlistId: null,
  bySymbol: {},
  asOfUtc: null,
  batchSequence: null,
};

export function toWatchlistRealtimeConnectionState(enabled: boolean, watchlistId: string | null, currentState: MarketDataRealtimeConnectionState) {
  return enabled && watchlistId ? currentState : "idle";
}

export function applyWatchlistSnapshot(
  current: WatchlistRealtimeMarketState,
  watchlistId: string,
  snapshot: {
    batchSequence: number;
    asOfUtc: string;
    symbols: MarketDataWatchlistSymbolSnapshotView[];
  },
): WatchlistRealtimeMarketState {
  if (current.watchlistId !== watchlistId) {
    return {
      watchlistId,
      bySymbol: snapshot.symbols.reduce<Record<string, MarketDataWatchlistSymbolSnapshotView>>((accumulator, item) => {
        accumulator[item.symbol.toUpperCase()] = item;
        return accumulator;
      }, {}),
      asOfUtc: snapshot.asOfUtc,
      batchSequence: snapshot.batchSequence,
    };
  }

  if (current.batchSequence !== null && snapshot.batchSequence < current.batchSequence) {
    return current;
  }

  const bySymbol = snapshot.symbols.reduce<Record<string, MarketDataWatchlistSymbolSnapshotView>>((accumulator, item) => {
    accumulator[item.symbol.toUpperCase()] = item;
    return accumulator;
  }, {});

  return {
    watchlistId,
    bySymbol,
    asOfUtc: snapshot.asOfUtc,
    batchSequence: snapshot.batchSequence,
  };
}
