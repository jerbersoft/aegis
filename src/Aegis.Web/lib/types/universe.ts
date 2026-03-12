export type WatchlistSummaryView = {
  watchlistId: string;
  name: string;
  watchlistType: string;
  isSystem: boolean;
  isExecution: boolean;
  canRename: boolean;
  canDelete: boolean;
  symbolCount: number;
  createdUtc: string;
  updatedUtc: string;
};

export type WatchlistDetailView = WatchlistSummaryView;

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

export type WatchlistContentsView = {
  watchlistId: string;
  name: string;
  watchlistType: string;
  totalCount: number;
  items: WatchlistItemView[];
};

export type ExecutionRemovalBlockersView = {
  symbolId: string;
  ticker: string;
  canRemove: boolean;
  hasActiveStrategy: boolean;
  hasOpenPosition: boolean;
  hasOpenOrders: boolean;
  blockingReasonCodes: string[];
};

export type CreateWatchlistRequest = {
  name: string;
};

export type RenameWatchlistRequest = {
  name: string;
};

export type AddSymbolToWatchlistRequest = {
  symbol: string;
};
