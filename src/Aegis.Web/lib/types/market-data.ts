export type MarketDataBootstrapStatusView = {
  readinessState: string;
  dailyDemandSymbolCount: number;
  warmedSymbolCount: number;
  persistedBarCount: number;
  lastWarmupUtc?: string | null;
  demandSymbols: string[];
  failedSymbols: string[];
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
