"use client";

import type {
  MarketDataHomeRefreshEventView,
  MarketDataRealtimeConnectionState,
  MarketDataSubscriptionAckView,
  MarketDataWatchlistSnapshotEventView,
} from "./types/market-data";

const eventNames = {
  homeRefreshHint: "market_data_home_refresh_hint",
  watchlistSnapshot: "market_data_watchlist_snapshot",
} as const;

const methodNames = {
  subscribeHome: "SubscribeHome",
  unsubscribeHome: "UnsubscribeHome",
  subscribeWatchlist: "SubscribeWatchlist",
  unsubscribeWatchlist: "UnsubscribeWatchlist",
} as const;

export type HomeRealtimeEvent =
  | { kind: "subscription_ack"; ack: MarketDataSubscriptionAckView }
  | { kind: "refresh_hint"; event: MarketDataHomeRefreshEventView };

export type WatchlistRealtimeEvent =
  | { kind: "subscription_ack"; ack: MarketDataSubscriptionAckView }
  | { kind: "snapshot"; event: MarketDataWatchlistSnapshotEventView };

type ConnectionStateListener = (state: MarketDataRealtimeConnectionState) => void;
type HomeListener = (event: HomeRealtimeEvent) => void;
type WatchlistListener = (event: WatchlistRealtimeEvent) => void;

type MarketDataSubscriptionAckWire = {
  contract_version?: string;
  contractVersion?: string;
  scope_kind?: string;
  scopeKind?: string;
  scope_key?: string;
  scopeKey?: string;
  delivery_strategy?: string;
  deliveryStrategy?: string;
  requires_authoritative_refresh?: boolean;
  requiresAuthoritativeRefresh?: boolean;
  subscribed_utc?: string;
  subscribedUtc?: string;
};

type MarketDataHomeRefreshEventWire = {
  contract_version?: string;
  contractVersion?: string;
  event_id?: string;
  eventId?: string;
  occurred_utc?: string;
  occurredUtc?: string;
  requires_refresh?: boolean;
  requiresRefresh?: boolean;
  changed_scopes?: string[];
  changedScopes?: string[];
};

type MarketDataWatchlistSymbolSnapshotWire = {
  symbol: string;
  current_price?: number | null;
  currentPrice?: number | null;
  percent_change?: number | null;
  percentChange?: number | null;
};

type MarketDataWatchlistSnapshotEventWire = {
  contract_version?: string;
  contractVersion?: string;
  event_id?: string;
  eventId?: string;
  watchlist_id?: string;
  watchlistId?: string;
  batch_sequence?: number;
  batchSequence?: number;
  occurred_utc?: string;
  occurredUtc?: string;
  as_of_utc?: string;
  asOfUtc?: string;
  requires_refresh?: boolean;
  requiresRefresh?: boolean;
  symbols?: MarketDataWatchlistSymbolSnapshotWire[];
};

function normalizeSubscriptionAck(payload: unknown): MarketDataSubscriptionAckView {
  const wire = payload as MarketDataSubscriptionAckWire;

  return {
    contractVersion: wire.contractVersion ?? wire.contract_version ?? "",
    scopeKind: wire.scopeKind ?? wire.scope_kind ?? "",
    scopeKey: wire.scopeKey ?? wire.scope_key ?? "",
    deliveryStrategy: wire.deliveryStrategy ?? wire.delivery_strategy ?? "",
    requiresAuthoritativeRefresh: wire.requiresAuthoritativeRefresh ?? wire.requires_authoritative_refresh ?? false,
    subscribedUtc: wire.subscribedUtc ?? wire.subscribed_utc ?? "",
  };
}

function normalizeHomeRefreshEvent(payload: unknown): MarketDataHomeRefreshEventView {
  const wire = payload as MarketDataHomeRefreshEventWire;

  return {
    contractVersion: wire.contractVersion ?? wire.contract_version ?? "",
    eventId: wire.eventId ?? wire.event_id ?? "",
    occurredUtc: wire.occurredUtc ?? wire.occurred_utc ?? "",
    requiresRefresh: wire.requiresRefresh ?? wire.requires_refresh ?? false,
    changedScopes: wire.changedScopes ?? wire.changed_scopes ?? [],
  };
}

function normalizeWatchlistSnapshotEvent(payload: unknown): MarketDataWatchlistSnapshotEventView {
  const wire = payload as MarketDataWatchlistSnapshotEventWire;

  return {
    contractVersion: wire.contractVersion ?? wire.contract_version ?? "",
    eventId: wire.eventId ?? wire.event_id ?? "",
    watchlistId: wire.watchlistId ?? wire.watchlist_id ?? "",
    batchSequence: wire.batchSequence ?? wire.batch_sequence ?? 0,
    occurredUtc: wire.occurredUtc ?? wire.occurred_utc ?? "",
    asOfUtc: wire.asOfUtc ?? wire.as_of_utc ?? "",
    requiresRefresh: wire.requiresRefresh ?? wire.requires_refresh ?? false,
    symbols: (wire.symbols ?? []).map((item) => ({
      symbol: item.symbol,
      currentPrice: item.currentPrice ?? item.current_price ?? null,
      percentChange: item.percentChange ?? item.percent_change ?? null,
    })),
  };
}

function createWatchlistSubscriptionRequest(watchlistId: string) {
  return { watchlist_id: watchlistId };
}

export type MarketDataRealtimeConnectionLike = {
  state: string;
  start: () => Promise<void>;
  stop: () => Promise<void>;
  invoke: <T>(method: string, ...args: unknown[]) => Promise<T>;
  on: (eventName: string, handler: (...args: unknown[]) => void) => void;
  onreconnecting: (handler: (...args: unknown[]) => void) => void;
  onreconnected: (handler: (...args: unknown[]) => void) => void;
  onclose: (handler: (...args: unknown[]) => void) => void;
};

type ConnectionFactory = () => Promise<MarketDataRealtimeConnectionLike>;

const hubPath = "/hubs/market-data";
const serverHubUrl = `${(process.env.NEXT_PUBLIC_BACKEND_URL ?? "http://localhost:5078").replace(/\/$/, "")}${hubPath}`;

async function createSignalRConnection(): Promise<MarketDataRealtimeConnectionLike> {
  const signalR = await import("@microsoft/signalr");

  return new signalR.HubConnectionBuilder()
    .withUrl(typeof window === "undefined" ? serverHubUrl : hubPath, {
      withCredentials: true,
      transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents | signalR.HttpTransportType.LongPolling,
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();
}

export class MarketDataRealtimeClient {
  private connection: MarketDataRealtimeConnectionLike | null = null;
  private readonly connectionFactory: ConnectionFactory;
  private readonly connectionStateListeners = new Set<ConnectionStateListener>();
  private readonly homeListeners = new Set<HomeListener>();
  private readonly watchlistListeners = new Map<string, Set<WatchlistListener>>();
  private readonly subscribedWatchlists = new Set<string>();
  private homeSubscribed = false;
  private connectionState: MarketDataRealtimeConnectionState = "idle";
  private startPromise: Promise<void> | null = null;
  private initializationPromise: Promise<MarketDataRealtimeConnectionLike> | null = null;

  public constructor(connectionFactory: ConnectionFactory = createSignalRConnection) {
    this.connectionFactory = connectionFactory;
  }

  public getConnectionState(): MarketDataRealtimeConnectionState {
    return this.connectionState;
  }

  public subscribeConnectionState(listener: ConnectionStateListener): () => void {
    this.connectionStateListeners.add(listener);
    listener(this.connectionState);

    return () => {
      this.connectionStateListeners.delete(listener);
    };
  }

  public subscribeHome(listener: HomeListener): () => void {
    const shouldSubscribe = this.homeListeners.size === 0;
    this.homeListeners.add(listener);
    if (shouldSubscribe) {
      void this.ensureHomeSubscribedAsync();
    }

    return () => {
      this.homeListeners.delete(listener);
      if (this.homeListeners.size === 0) {
        void this.unsubscribeHomeAsync();
      }

      void this.stopIfIdleAsync();
    };
  }

  public subscribeWatchlist(watchlistId: string, listener: WatchlistListener): () => void {
    const listeners = this.watchlistListeners.get(watchlistId) ?? new Set<WatchlistListener>();
    const shouldSubscribe = listeners.size === 0;
    listeners.add(listener);
    this.watchlistListeners.set(watchlistId, listeners);

    if (shouldSubscribe) {
      void this.ensureWatchlistSubscribedAsync(watchlistId);
    }

    return () => {
      const currentListeners = this.watchlistListeners.get(watchlistId);
      if (!currentListeners) {
        return;
      }

      currentListeners.delete(listener);
      if (currentListeners.size === 0) {
        this.watchlistListeners.delete(watchlistId);
        void this.unsubscribeWatchlistAsync(watchlistId);
      }

      void this.stopIfIdleAsync();
    };
  }

  private hasActiveSubscriptions(): boolean {
    return this.homeListeners.size > 0 || this.watchlistListeners.size > 0;
  }

  private setConnectionState(state: MarketDataRealtimeConnectionState): void {
    this.connectionState = state;
    this.connectionStateListeners.forEach((listener) => listener(state));
  }

  private async getConnectionAsync(): Promise<MarketDataRealtimeConnectionLike> {
    if (this.connection) {
      return this.connection;
    }

    if (!this.initializationPromise) {
      this.initializationPromise = this.connectionFactory().then((connection) => {
        this.wireConnection(connection);
        this.connection = connection;
        return connection;
      }).finally(() => {
        this.initializationPromise = null;
      });
    }

    return this.initializationPromise;
  }

  private wireConnection(connection: MarketDataRealtimeConnectionLike): void {
    connection.on(eventNames.homeRefreshHint, (payload) => {
      const event = normalizeHomeRefreshEvent(payload);
      this.homeListeners.forEach((listener) => listener({ kind: "refresh_hint", event }));
    });

    connection.on(eventNames.watchlistSnapshot, (payload) => {
      const event = normalizeWatchlistSnapshotEvent(payload);
      const listeners = this.watchlistListeners.get(event.watchlistId);
      listeners?.forEach((listener) => listener({ kind: "snapshot", event }));
    });

    connection.onreconnecting(() => {
      this.setConnectionState("reconnecting");
    });

    connection.onreconnected(() => {
      this.setConnectionState("connected");
      void this.resubscribeAllAsync().catch(() => {
        this.setConnectionState("unavailable");
      });
    });

    connection.onclose(() => {
      this.startPromise = null;
      this.homeSubscribed = false;
      this.subscribedWatchlists.clear();
      this.setConnectionState(this.hasActiveSubscriptions() ? "unavailable" : "idle");
    });
  }

  private async ensureStartedAsync(): Promise<void> {
    if (!this.hasActiveSubscriptions()) {
      return;
    }

    const connection = await this.getConnectionAsync();
    if (connection.state === "Connected") {
      this.setConnectionState("connected");
      return;
    }

    if (connection.state === "Reconnecting") {
      this.setConnectionState("reconnecting");
      return;
    }

    if (this.startPromise) {
      return this.startPromise;
    }

    this.setConnectionState("connecting");
    this.startPromise = connection.start()
      .then(() => {
        this.setConnectionState("connected");
      })
      .catch((error) => {
        this.setConnectionState("unavailable");
        throw error;
      })
      .finally(() => {
        this.startPromise = null;
      });

    return this.startPromise;
  }

  private async ensureHomeSubscribedAsync(): Promise<void> {
    try {
      await this.ensureStartedAsync();
      const connection = await this.getConnectionAsync();
      if (connection.state !== "Connected" || this.homeListeners.size === 0 || this.homeSubscribed) {
        return;
      }

      const ack = normalizeSubscriptionAck(await connection.invoke<unknown>(methodNames.subscribeHome));
      this.homeSubscribed = true;
      this.homeListeners.forEach((listener) => listener({ kind: "subscription_ack", ack }));
    } catch {
      this.setConnectionState("unavailable");
    }
  }

  private async unsubscribeHomeAsync(): Promise<void> {
    if (!this.connection || this.connection.state !== "Connected") {
      return;
    }

    try {
      await this.connection.invoke<void>(methodNames.unsubscribeHome);
      this.homeSubscribed = false;
    } catch {
      this.setConnectionState("unavailable");
    }
  }

  private async ensureWatchlistSubscribedAsync(watchlistId: string): Promise<void> {
    try {
      await this.ensureStartedAsync();
      const connection = await this.getConnectionAsync();
      if (connection.state !== "Connected" || !this.watchlistListeners.has(watchlistId) || this.subscribedWatchlists.has(watchlistId)) {
        return;
      }

      // SignalR payloads must stay aligned with the backend's approved snake_case wire contract.
      const ack = normalizeSubscriptionAck(await connection.invoke<unknown>(methodNames.subscribeWatchlist, createWatchlistSubscriptionRequest(watchlistId)));
      this.subscribedWatchlists.add(watchlistId);
      this.watchlistListeners.get(watchlistId)?.forEach((listener) => listener({ kind: "subscription_ack", ack }));
    } catch {
      this.setConnectionState("unavailable");
    }
  }

  private async unsubscribeWatchlistAsync(watchlistId: string): Promise<void> {
    this.subscribedWatchlists.delete(watchlistId);
    if (!this.connection || this.connection.state !== "Connected") {
      return;
    }

    try {
      await this.connection.invoke<void>(methodNames.unsubscribeWatchlist, createWatchlistSubscriptionRequest(watchlistId));
    } catch {
      this.setConnectionState("unavailable");
    }
  }

  private async resubscribeAllAsync(): Promise<void> {
    const connection = await this.getConnectionAsync();
    if (connection.state !== "Connected") {
      return;
    }

    if (this.homeListeners.size > 0) {
      this.homeSubscribed = false;
      const ack = normalizeSubscriptionAck(await connection.invoke<unknown>(methodNames.subscribeHome));
      this.homeSubscribed = true;
      this.homeListeners.forEach((listener) => listener({ kind: "subscription_ack", ack }));
    } else {
      this.homeSubscribed = false;
    }

    this.subscribedWatchlists.clear();
    for (const [watchlistId, listeners] of this.watchlistListeners) {
      const ack = normalizeSubscriptionAck(await connection.invoke<unknown>(methodNames.subscribeWatchlist, createWatchlistSubscriptionRequest(watchlistId)));
      this.subscribedWatchlists.add(watchlistId);
      listeners.forEach((listener) => listener({ kind: "subscription_ack", ack }));
    }
  }

  private async stopIfIdleAsync(): Promise<void> {
    if (this.hasActiveSubscriptions() || !this.connection || this.connection.state === "Disconnected") {
      return;
    }

    this.subscribedWatchlists.clear();
    this.homeSubscribed = false;
    await this.connection.stop();
    this.setConnectionState("idle");
  }
}

let singletonClient: MarketDataRealtimeClient | null = null;

export function getMarketDataRealtimeClient(): MarketDataRealtimeClient {
  singletonClient ??= new MarketDataRealtimeClient();
  return singletonClient;
}
