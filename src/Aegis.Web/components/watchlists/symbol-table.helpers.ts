import type { MarketDataWatchlistSymbolSnapshotView } from "../../lib/types/market-data.ts";
import type { WatchlistItemView } from "../../lib/types/universe.ts";

export function resolveWatchlistMarketValues(
  item: WatchlistItemView,
  marketDataBySymbol: Record<string, MarketDataWatchlistSymbolSnapshotView>,
) {
  const liveMarketData = marketDataBySymbol[item.ticker.toUpperCase()];

  return {
    price: liveMarketData?.currentPrice ?? item.currentPrice,
    percentChange: liveMarketData?.percentChange ?? item.percentChange,
  };
}

export function formatWatchlistPrice(value: number | null | undefined) {
  return typeof value === "number" ? `$${value.toFixed(2)}` : "—";
}

export function formatWatchlistPercentChange(value: number | null | undefined) {
  return typeof value === "number" ? `${value.toFixed(2)}%` : "—";
}
