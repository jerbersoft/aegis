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
