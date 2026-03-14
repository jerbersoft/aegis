import assert from "node:assert/strict";
import test from "node:test";

import { applyWatchlistSnapshot, emptyWatchlistRealtimeMarketState, toWatchlistRealtimeConnectionState } from "./use-watchlist-market-data-realtime.helpers.ts";

test("applyWatchlistSnapshot ignores older batches and uppercases symbol keys", () => {
  const newer = applyWatchlistSnapshot(emptyWatchlistRealtimeMarketState, "watchlist-1", {
    batchSequence: 2,
    asOfUtc: "2026-03-14T10:02:00Z",
    symbols: [{ symbol: "aapl", currentPrice: 187.12, percentChange: 1.25 }],
  });

  const older = applyWatchlistSnapshot(newer, "watchlist-1", {
    batchSequence: 1,
    asOfUtc: "2026-03-14T10:01:00Z",
    symbols: [{ symbol: "msft", currentPrice: 400, percentChange: 2 }],
  });

  assert.equal(older.batchSequence, 2);
  assert.deepEqual(Object.keys(older.bySymbol), ["AAPL"]);
});

test("toWatchlistRealtimeConnectionState forces idle without active watchlist or when disabled", () => {
  assert.equal(toWatchlistRealtimeConnectionState(true, null, "connected"), "idle");
  assert.equal(toWatchlistRealtimeConnectionState(false, "watchlist-1", "connected"), "idle");
  assert.equal(toWatchlistRealtimeConnectionState(true, "watchlist-1", "connected"), "connected");
});

test("applyWatchlistSnapshot resets state when switching to a different watchlist", () => {
  const current = applyWatchlistSnapshot(emptyWatchlistRealtimeMarketState, "watchlist-1", {
    batchSequence: 10,
    asOfUtc: "2026-03-14T10:10:00Z",
    symbols: [{ symbol: "AAPL", currentPrice: 187.12, percentChange: 1.25 }],
  });

  const next = applyWatchlistSnapshot(current, "watchlist-2", {
    batchSequence: 1,
    asOfUtc: "2026-03-14T10:11:00Z",
    symbols: [{ symbol: "AMD", currentPrice: 177.45, percentChange: 2.1 }],
  });

  assert.equal(next.watchlistId, "watchlist-2");
  assert.equal(next.batchSequence, 1);
  assert.deepEqual(Object.keys(next.bySymbol), ["AMD"]);
});
