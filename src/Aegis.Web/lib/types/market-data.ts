export type MarketDataBootstrapStatusView = {
  readinessState: string;
  reasonCode: string;
  profileKey: string;
  dailyDemandSymbolCount: number;
  warmedSymbolCount: number;
  readySymbolCount: number;
  notReadySymbolCount: number;
  persistedBarCount: number;
  asOfUtc: string;
  lastWarmupUtc?: string | null;
  demandSymbols: string[];
  failedSymbols: string[];
};

export type DailySymbolReadinessView = {
  symbol: string;
  profileKey: string;
  asOfUtc: string;
  readinessState: string;
  reasonCode: string;
  hasRequiredDailyBars: boolean;
  hasRequiredIndicatorState: boolean;
  hasBenchmarkDependency: boolean;
  benchmarkSymbol?: string | null;
  benchmarkReadinessState?: string | null;
  requiredBarCount: number;
  availableBarCount: number;
  lastFinalizedBarUtc?: string | null;
  lastStateChangedUtc: string;
};

export type DailyUniverseReadinessView = {
  profileKey: string;
  asOfUtc: string;
  readinessState: string;
  reasonCode: string;
  totalSymbolCount: number;
  readySymbolCount: number;
  notReadySymbolCount: number;
  symbols: DailySymbolReadinessView[];
};

export type IntradaySymbolReadinessView = {
  symbol: string;
  interval: string;
  profileKey: string;
  asOfUtc: string;
  readinessState: string;
  reasonCode: string;
  hasRequiredIntradayBars: boolean;
  hasRequiredIndicatorState: boolean;
  volumeBuzzPercent?: number | null;
  hasRequiredVolumeBuzzReferenceHistory: boolean;
  requiredVolumeBuzzReferenceSessionCount: number;
  availableVolumeBuzzReferenceSessionCount: number;
  requiredBarCount: number;
  availableBarCount: number;
  lastFinalizedBarUtc?: string | null;
  lastStateChangedUtc: string;
  activeGapType?: string | null;
  activeGapStartUtc?: string | null;
  hasActiveRepair: boolean;
  pendingRecompute: boolean;
  earliestAffectedBarUtc?: string | null;
};

export type IntradayUniverseReadinessView = {
  interval: string;
  profileKey: string;
  asOfUtc: string;
  readinessState: string;
  reasonCode: string;
  totalSymbolCount: number;
  readySymbolCount: number;
  notReadySymbolCount: number;
  activeRepairSymbolCount: number;
  pendingRecomputeSymbolCount: number;
  earliestAffectedBarUtc?: string | null;
  symbols: IntradaySymbolReadinessView[];
};

export type DailyBarView = {
  symbol: string;
  interval: string;
  barTimeUtc: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  sessionType: string;
  marketDate: string;
  providerName: string;
  providerFeed: string;
  runtimeState: string;
  isReconciled: boolean;
};

export type DailyBarsView = {
  symbol: string;
  totalCount: number;
  items: DailyBarView[];
};

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
