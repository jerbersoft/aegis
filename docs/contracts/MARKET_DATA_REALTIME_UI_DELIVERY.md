# MarketData Realtime UI Delivery Contract

## 1) Scope

This document defines the v1 SignalR contract for MarketData-driven UI delivery.

It currently covers only:

- Home MarketData widget refresh signaling
- Watchlist current-price and percent-change delivery

Pull endpoints remain authoritative.
Push delivery exists only to reduce refresh latency and provide bounded, coalesced UI update hints.

## 2) Hub topology

v1 uses a single authenticated hub:

- path: `/hubs/market-data`

Rationale:

- current realtime scope is limited to MarketData-driven UI surfaces
- a single cookie-authenticated browser connection avoids extra connection/session cost
- hub methods and group scopes provide enough isolation without per-feature hub proliferation

## 3) Authentication and session behavior

- Hub connections use the existing backend cookie-auth session.
- Anonymous hub connections are rejected.
- The browser must establish the normal `/api/auth/login` cookie session before connecting.
- On reconnect, the browser must assume prior subscriptions are lost and resubscribe.

## 4) Subscription model

The hub exposes bounded surface-oriented subscriptions rather than per-symbol subscriptions.

Supported v1 subscriptions:

- `SubscribeHome()`
- `SubscribeWatchlist({ watchlistId })`
- `UnsubscribeHome()`
- `UnsubscribeWatchlist({ watchlistId })`

Server grouping:

- `market-data:home`
- `market-data:watchlist:{watchlistId}`

Rules:

- Home is a single shared scope for MarketData widget refresh hints.
- Watchlists subscribe by watchlist id, not by symbol.
- Watchlist payloads are limited to the symbols already present in that watchlist.
- The server may send an initial watchlist snapshot immediately after subscription.

## 5) Event contract

Contract version:

- `v1`

Serialized event names use singular `snake_case`.

### `market_data_home_refresh_hint`

- delivery strategy: `refresh_hint`
- semantics: tells the client to re-query authoritative REST endpoints for Home MarketData state

Payload:

- `contract_version`
- `event_id`
- `occurred_utc`
- `requires_refresh`
- `changed_scopes`

Current `changed_scopes` values:

- `bootstrap_status`
- `daily_readiness`
- `intraday_readiness`

### `market_data_watchlist_snapshot`

- delivery strategy: `coalesced_snapshot_delta`
- semantics: bounded watchlist-scoped market value snapshot for already-subscribed watchlist symbols
- payload is still non-authoritative; the client may re-query if it needs canonical current view state

Payload:

- `contract_version`
- `event_id`
- `watchlist_id`
- `batch_sequence`
- `occurred_utc`
- `as_of_utc`
- `requires_refresh`
- `symbols`

`symbols[]` fields:

- `symbol`
- `current_price`
- `percent_change`

## 6) Authoritative pull vs push semantics

- REST/readiness endpoints remain the source of truth.
- Home uses push only as a refresh trigger.
- Watchlist push carries a compact surface payload for faster UI replacement of placeholder values, but it does not replace authoritative pull APIs.
- Clients should treat reconnect as a state-boundary event and re-query current REST state after resubscription.

## 7) Throttling and coalescing

v1 server throttles are intentionally surface-based:

- Home refresh hints: at most once per 1000 ms per hub instance
- Watchlist snapshots: at most once per 750 ms per watchlist per hub instance

Rules:

- multiple Home changes inside the throttle window are coalesced into one refresh-hint event
- watchlist fan-out is grouped by watchlist id rather than symbol
- clients must tolerate skipped intermediate snapshots and use the latest delivered batch only
- `batch_sequence` is monotonic per watchlist subscription scope and allows clients to ignore older arrivals

## 8) Reconnection expectations

- SignalR automatic reconnect is expected on the web client.
- After reconnect, the client must resubscribe to Home and any active watchlist scopes.
- After reconnect and resubscription, the client should re-query authoritative REST data before trusting local transient state.

## 9) Compatibility posture

- v1 is additive-first.
- existing fields and event names are stable for v1 consumers.
- new optional fields may be added without changing event names.
- incompatible payload or semantic changes require a new contract version/event family rather than silent mutation.

## 10) Current implementation status

The current backend implementation locks the contract with:

- authenticated hub mapping
- subscription acknowledgements
- home refresh-hint emission on MarketData bootstrap refresh events
- watchlist snapshot emission with current price and percent change derived from MarketData runtime state

Future realtime publishers may emit the same event contract from live MarketData pipelines without changing the v1 wire model.
