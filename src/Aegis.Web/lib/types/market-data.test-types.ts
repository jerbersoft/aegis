export type MarketDataRealtimeConnectionState = "idle" | "connecting" | "connected" | "reconnecting" | "unavailable";

export type MarketDataSubscriptionAckView = {
  contractVersion: string;
  scopeKind: string;
  scopeKey: string;
  deliveryStrategy: string;
  requiresAuthoritativeRefresh: boolean;
  subscribedUtc: string;
};

export type MarketDataWatchlistSubscriptionRequest = {
  watchlistId: string;
};

export type MarketDataHomeRefreshEventView = {
  contractVersion: string;
  eventId: string;
  occurredUtc: string;
  requiresRefresh: boolean;
  changedScopes: string[];
};

export type MarketDataWatchlistSymbolSnapshotView = {
  symbol: string;
  currentPrice: number | null;
  percentChange: number | null;
};

export type MarketDataWatchlistSnapshotEventView = {
  contractVersion: string;
  eventId: string;
  watchlistId: string;
  batchSequence: number;
  occurredUtc: string;
  asOfUtc: string;
  requiresRefresh: boolean;
  symbols: MarketDataWatchlistSymbolSnapshotView[];
};
