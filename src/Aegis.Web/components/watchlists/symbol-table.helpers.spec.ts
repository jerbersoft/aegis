import assert from "node:assert/strict";
import test from "node:test";

import { formatWatchlistPercentChange, formatWatchlistPrice, resolveWatchlistMarketValues } from "./symbol-table.helpers.ts";
import type { WatchlistItemView } from "../../lib/types/universe.test-types.ts";

const watchlistItem: WatchlistItemView = {
  watchlistItemId: "item-1",
  watchlistId: "watchlist-1",
  symbolId: "symbol-1",
  ticker: "AAPL",
  assetClass: "equity",
  addedUtc: "2026-03-14T10:00:00Z",
  isInExecution: true,
  watchlistCount: 1,
  currentPrice: 100,
  percentChange: -1,
};

test("resolveWatchlistMarketValues prefers live snapshot values over pulled item values", () => {
  const values = resolveWatchlistMarketValues(
    watchlistItem,
    {
      AAPL: {
        symbol: "AAPL",
        currentPrice: 187.12,
        percentChange: 1.25,
      },
    },
  );

  assert.deepEqual(values, {
    price: 187.12,
    percentChange: 1.25,
  });
});

test("watchlist value formatters show fallback dash when market data is unavailable", () => {
  assert.equal(formatWatchlistPrice(null), "—");
  assert.equal(formatWatchlistPercentChange(undefined), "—");
});
