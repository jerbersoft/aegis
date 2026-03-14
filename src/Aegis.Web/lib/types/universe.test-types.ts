export type WatchlistItemView = {
  watchlistItemId: string;
  watchlistId: string;
  symbolId: string;
  ticker: string;
  assetClass: string;
  addedUtc: string;
  isInExecution: boolean;
  watchlistCount: number;
  currentPrice?: number | null;
  percentChange?: number | null;
};
