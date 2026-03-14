import assert from "node:assert/strict";
import test from "node:test";

import { MarketDataRealtimeClient } from "./market-data-realtime.ts";
import type { MarketDataHomeRefreshEventView, MarketDataSubscriptionAckView, MarketDataWatchlistSnapshotEventView } from "./types/market-data.test-types.ts";

type Handler = (...args: unknown[]) => void;

class FakeConnection {
  public state = "Disconnected" as "Disconnected" | "Connected" | "Reconnecting";
  public readonly invocations: Array<{ method: string; args: unknown[] }> = [];
  private readonly handlers = new Map<string, Handler>();
  private reconnectingHandler: Handler | null = null;
  private reconnectedHandler: Handler | null = null;
  private closeHandler: Handler | null = null;

  async start() {
    this.state = "Connected";
  }

  async stop() {
    this.state = "Disconnected";
  }

  async invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    this.invocations.push({ method, args });

    if (method === "SubscribeHome") {
      return homeAck as T;
    }

    if (method === "SubscribeWatchlist") {
      return watchlistAck as T;
    }

    return undefined as T;
  }

  on(eventName: string, handler: Handler) {
    this.handlers.set(eventName, handler);
  }

  onreconnecting(handler: Handler) {
    this.reconnectingHandler = handler;
  }

  onreconnected(handler: Handler) {
    this.reconnectedHandler = handler;
  }

  onclose(handler: Handler) {
    this.closeHandler = handler;
  }

  emit(eventName: string, payload: unknown) {
    this.handlers.get(eventName)?.(payload);
  }

  async reconnect() {
    this.state = "Reconnecting";
    await this.reconnectingHandler?.();
    this.state = "Connected";
    await this.reconnectedHandler?.();
  }

  async close() {
    this.state = "Disconnected";
    await this.closeHandler?.();
  }
}

async function flushAsyncWork() {
  await new Promise((resolve) => setImmediate(resolve));
  await Promise.resolve();
}

const homeAck: MarketDataSubscriptionAckView = {
  contractVersion: "v1",
  scopeKind: "home",
  scopeKey: "home",
  deliveryStrategy: "refresh_hint",
  requiresAuthoritativeRefresh: true,
  subscribedUtc: "2026-03-14T10:00:00Z",
};

const watchlistAck: MarketDataSubscriptionAckView = {
  contractVersion: "v1",
  scopeKind: "watchlist",
  scopeKey: "watchlist-1",
  deliveryStrategy: "coalesced_snapshot_delta",
  requiresAuthoritativeRefresh: true,
  subscribedUtc: "2026-03-14T10:00:00Z",
};

const homeEvent: MarketDataHomeRefreshEventView = {
  contractVersion: "v1",
  eventId: "event-1",
  occurredUtc: "2026-03-14T10:01:00Z",
  requiresRefresh: true,
  changedScopes: ["bootstrap_status"],
};

const watchlistEvent: MarketDataWatchlistSnapshotEventView = {
  contractVersion: "v1",
  eventId: "event-2",
  watchlistId: "watchlist-1",
  batchSequence: 2,
  occurredUtc: "2026-03-14T10:02:00Z",
  asOfUtc: "2026-03-14T10:02:00Z",
  requiresRefresh: true,
  symbols: [
    {
      symbol: "AAPL",
      currentPrice: 187.12,
      percentChange: 1.25,
    },
  ],
};

test("MarketDataRealtimeClient subscribes, emits events, and resubscribes after reconnect", async () => {
  const connection = new FakeConnection();
  const client = new MarketDataRealtimeClient(async () => connection as never);
  const connectionStates: string[] = [];
  const homeEvents: string[] = [];
  const watchlistEvents: string[] = [];

  client.subscribeConnectionState((state) => {
    connectionStates.push(state);
  });

  client.subscribeHome((event) => {
    homeEvents.push(event.kind);
  });

  client.subscribeWatchlist("watchlist-1", (event) => {
    watchlistEvents.push(event.kind);
  });

  await flushAsyncWork();

  connection.emit("market_data_home_refresh_hint", homeEvent);
  connection.emit("market_data_watchlist_snapshot", watchlistEvent);
  await connection.reconnect();
  await flushAsyncWork();

  const invocationCounts = connection.invocations.reduce<Record<string, number>>((accumulator, item) => {
    accumulator[item.method] = (accumulator[item.method] ?? 0) + 1;
    return accumulator;
  }, {});

  assert.deepEqual(invocationCounts, {
    SubscribeHome: 2,
    SubscribeWatchlist: 2,
  });
  assert.ok(connectionStates.includes("connecting"));
  assert.ok(connectionStates.includes("connected"));
  assert.ok(connectionStates.includes("reconnecting"));
  assert.deepEqual(homeEvents, ["subscription_ack", "refresh_hint", "subscription_ack"]);
  assert.deepEqual(watchlistEvents, ["subscription_ack", "snapshot", "subscription_ack"]);
});

test("MarketDataRealtimeClient marks active subscriptions unavailable after close", async () => {
  const connection = new FakeConnection();
  const client = new MarketDataRealtimeClient(async () => connection as never);
  const connectionStates: string[] = [];

  client.subscribeConnectionState((state) => {
    connectionStates.push(state);
  });

  client.subscribeHome(() => {
    // no-op
  });

  await flushAsyncWork();
  await connection.close();

  assert.equal(connectionStates.at(-1), "unavailable");
});

test("MarketDataRealtimeClient sends snake_case watchlist requests and normalizes snake_case payloads", async () => {
  const connection = new FakeConnection();
  const client = new MarketDataRealtimeClient(async () => connection as never);
  const acknowledgements: MarketDataSubscriptionAckView[] = [];
  const snapshots: MarketDataWatchlistSnapshotEventView[] = [];

  client.subscribeWatchlist("watchlist-1", (event) => {
    if (event.kind === "subscription_ack") {
      acknowledgements.push(event.ack);
      return;
    }

    snapshots.push(event.event);
  });

  await flushAsyncWork();

  connection.emit("market_data_watchlist_snapshot", {
    contract_version: "v1",
    event_id: "event-3",
    watchlist_id: "watchlist-1",
    batch_sequence: 3,
    occurred_utc: "2026-03-14T10:03:00Z",
    as_of_utc: "2026-03-14T10:03:00Z",
    requires_refresh: true,
    symbols: [
      {
        symbol: "AMD",
        current_price: 177.45,
        percent_change: 2.1,
      },
    ],
  });

  await flushAsyncWork();

  assert.deepEqual(connection.invocations[0], {
    method: "SubscribeWatchlist",
    args: [{ watchlist_id: "watchlist-1" }],
  });
  assert.equal(acknowledgements[0]?.requiresAuthoritativeRefresh, true);
  assert.equal(snapshots[0]?.watchlistId, "watchlist-1");
  assert.equal(snapshots[0]?.symbols[0]?.currentPrice, 177.45);
  assert.equal(snapshots[0]?.symbols[0]?.percentChange, 2.1);
});
