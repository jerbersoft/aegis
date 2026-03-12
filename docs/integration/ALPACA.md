# Alpaca Market Data Integration Research

## 1) Purpose

This document evaluates `Alpaca` against the current `Aegis` MarketData requirements and defines a provider capability checklist for comparing market-data vendors.

This is a research and planning document, not an implementation contract.

Current status note:

- The repository currently contains `src/adapters/Aegis.Adapters.Alpaca`, but that project is only being used for a fake `ISymbolReferenceProvider` bootstrap implementation.
- This document should not be read as evidence that the full Alpaca market-data adapter has been implemented.

## 2) Sources reviewed

- `https://github.com/alpacahq/alpaca-trade-api-csharp`
- `https://docs.alpaca.markets/docs/about-market-data-api`
- `https://docs.alpaca.markets/docs/historical-stock-data-1`
- `https://docs.alpaca.markets/docs/streaming-market-data`
- `https://docs.alpaca.markets/docs/real-time-stock-pricing-data`
- `https://docs.alpaca.markets/docs/market-data-faq`
- `https://olegra.github.io/Alpaca.Markets/api/Alpaca.Markets.IAlpacaDataClient.html`
- `https://olegra.github.io/Alpaca.Markets/api/Alpaca.Markets.IAlpacaDataStreamingClient.html`
- `https://olegra.github.io/Alpaca.Markets/api/Alpaca.Markets.IStreamingClient.html`
- `https://olegra.github.io/Alpaca.Markets/api/Alpaca.Markets.HistoricalBarsRequest.html`
- `https://olegra.github.io/Alpaca.Markets/api/Alpaca.Markets.MarketDataFeed.html`

## 3) Aegis provider capability checklist

### A. Coverage and product fit

- supports `US equities`
- supports realtime trades
- supports realtime quotes
- supports realtime intraday bars
- supports historical daily bars
- supports historical intraday bars for `1-min` and potentially future deferred intervals such as `5-min` and `15-min`
- supports pre-market and post-market coverage
- supports benchmark symbols such as `SPY`
- supports a large tracked universe, including a target scale around `4,000` symbols

### B. Historical retrieval

- supports multi-symbol historical requests
- supports pagination or page-token traversal
- supports explicit time filtering
- supports sufficient historical rate limits for warmup and repair
- supports history deep enough for at least:
  - `300` daily bars
  - `20` days intraday retention target
- supports symbol rename or corporate-action-aware lookup, or provides a practical workaround
- clearly specifies feed and source selection semantics

### C. Realtime streaming

- supports stable realtime streaming protocol(s)
- supports symbol-level subscriptions
- supports sufficient symbol subscriptions for production scale
- supports minute-bar stream
- supports quote stream
- supports trade stream
- supports market-status, halt, `LULD`, or equivalent operational signals
- has clear connection-limit rules
- has clear slow-client or backpressure behavior
- supports safe reconnect and resubscribe, either natively or through adapter-owned recovery

### D. Bar correctness and canonicality

- documents how bars are constructed
- provides provider-generated bars so `Aegis` does not aggregate its own bars
- defines whether emitted minute bars may later be revised
- emits correction or updated-bar events when prior bar state changes
- documents late-trade handling
- supports session semantics compatible with `Aegis`
- supports daily-bar semantics compatible with `Aegis` `RTH`-only policy, or clearly documents any mismatch

### E. Operational resilience

- exposes explicit auth, subscription, and limit errors
- distinguishes insufficient subscription from transient failures
- provides sandbox or test environment
- provides usable `.NET` SDK and/or protocol compatibility for `.NET 10` integration
- has enough documentation quality for implementation and troubleshooting
- exposes provider signals useful for `Aegis` readiness, alerts, and recovery behavior

### F. Economic and plan viability

- production plan includes `SIP` or equivalent full-market coverage
- production plan supports enough websocket symbols
- historical RPM supports startup warmup and gap repair
- connection limits fit the single-process modular-monolith architecture
- overall cost is acceptable for v1

## 4) Status scale for provider comparison

Use these values when comparing providers:

- `meets`
- `partial`
- `unknown`
- `does_not_meet`

Recommended comparison columns:

- capability
- why Aegis needs it
- required for v1
- provider status
- evidence
- notes and adapter impact

## 5) Alpaca capability matrix

| Capability | Required for v1 | Alpaca status | Evidence | Notes / adapter impact |
| --- | --- | --- | --- | --- |
| `US equities` support | yes | meets | Alpaca market data docs | In scope for v1 |
| Realtime trades | yes | meets | stock websocket docs | Available |
| Realtime quotes | yes | meets | stock websocket docs | Available |
| Realtime minute bars | yes | meets | `bars` channel in stock websocket docs | Available |
| Historical daily bars | yes | meets | historical stock data docs and SDK historical bars support | Available |
| Historical intraday bars | yes | meets | historical stock data docs and SDK historical bars support | Suitable for active v1 `1-min` use and future deferred intervals, though exact runtime validation is still advisable |
| Pre-market and post-market intraday coverage | yes | partial | realtime minute bars explicitly include pre-market and aftermarket trades | Historical extended-hours semantics still need more direct validation for Aegis policy |
| Full-market production feed | yes | meets on paid plan | `SIP` feed docs | Free `IEX` is not sufficient for production correctness |
| Free-tier production viability | no | does_not_meet | basic/free plan docs: `IEX` only with symbol limits | Not suitable for Aegis production market-data design |
| Large symbol streaming scale | yes | partial | paid plans describe unlimited symbols, but docs reviewed do not prove practical 4,000-symbol behavior | Needs load and throughput validation |
| Multi-symbol historical retrieval | yes | meets | SDK `HistoricalBarsRequest(IEnumerable<String>, ...)` | Good fit for warmup and repair batching |
| Historical rate limits | yes | partial | plan docs show `200/min` basic and up to `10,000/min` paid; broker plans vary | Depends on selected plan |
| Symbol rename handling | no | partial | market data FAQ documents historical `asof` support; live stream requires resubscribe to new symbol | Adapter must own live rename handling |
| Trade corrections | yes | meets | corrections channel in websocket docs and SDK correction subscriptions | Good fit for canonical correction handling |
| Updated bars for late trades | yes | meets | `updatedBars` channel in websocket docs and SDK updated-bar subscriptions | Important for bar-finality design |
| `LULD` and trading status | no | meets | websocket docs and SDK support status and `LULD` subscriptions | Useful for operational state and alerts |
| Connection-limit suitability | yes | partial | websocket docs indicate most subscriptions allow only `1` connection per endpoint | Works if Aegis uses a single shared stream per provider/feed |
| Reconnect and resubscribe contract | yes | unknown | reviewed SDK docs expose connect/disconnect/events but did not verify automatic recovery semantics | Adapter should own recovery unless proven otherwise |
| `.NET` SDK maturity | yes | meets | official C# SDK repo and generated API docs | Strong integration fit for approved stack |
| `BOATS` / `overnight` feed access from SDK | no | unknown | market docs mention these feeds; reviewed SDK feed enum docs only clearly show `Iex`, `Sip`, and `Otc` | Needs explicit validation before design depends on it |
| Daily-bar compatibility with Aegis `RTH`-only rule | yes | unknown | reviewed docs did not fully confirm this against Aegis daily-bar policy | Needs direct verification |

## 6) Key findings

### Verified strengths

- `Alpaca` clearly supports the core `Aegis` needs for equities market data through both historical HTTP APIs and realtime websocket streams.
- `Alpaca` provides trade, quote, minute-bar, daily-bar, updated-bar, correction, cancellation, status, and `LULD` data.
- `Alpaca` documents how its bars are constructed from trades, including trade-condition effects on `OHLC`, volume, trade count, and `VWAP`.
- The official C# SDK is a strong fit for the approved `Aegis` backend stack.
- Historical APIs and SDK contracts support multi-symbol bar requests, which is valuable for warmup and repair workflows.

### Important caveats

- Production use requires a paid `SIP`-capable plan. Free `IEX` access is not sufficient for scanner or trading correctness.
- Docs reviewed indicate connection limits are often `1` stream per endpoint, so the `Aegis` adapter should assume a single shared stream with internal fan-out.
- `Alpaca` websocket subscriptions are additive subscribe/unsubscribe operations. `Aegis` can still keep replace-all desired-state semantics internally, but the adapter must translate desired state into diffs.
- Realtime bar handling is not immutable: `updatedBars` exist specifically for late trades that change previously emitted minute bars.

## 7) Agreed Aegis-to-Alpaca contract direction

### Feed and environment policy

- `Aegis` keeps one stable MarketData contract regardless of whether the active `Alpaca` feed is `IEX` or `SIP`.
- Readiness semantics do not change by feed.
- Feed choice changes data completeness, scale confidence, and production confidence; it does not change the readiness contract itself.
- `Aegis` should expose market-data operating mode separately from readiness.
- v1 operating mode should distinguish at least:
  - `full`
  - `limited`
- `limited` mode is intended for UI/operator visibility when the active provider/feed environment has known capability restrictions.

### Canonical Alpaca sources by channel

- Historical stock bars API is the canonical source for persisted daily bars.
- Historical stock bars API is the canonical source for persisted intraday warmup, backfill, repair, and reconciliation.
- Websocket `bars` is the canonical first publication of newly closed minute bars.
- Websocket `updatedBars` is the canonical revision stream for previously emitted minute bars.
- Websocket `dailyBars` is live session-progress data only and is not the canonical persisted daily-bar source.
- Websocket `trades` is the canonical live trade-event stream, but not a bar-construction input.
- Websocket `quotes` is the canonical live quote stream, but not a bar-construction input.
- Websocket `statuses`, `lulds`, corrections, and cancel/errors are canonical provider operational and revalidation signals.

### Alpaca subscription orchestration contract

- `Aegis` keeps replace-all desired subscription semantics internally.
- The `Alpaca` adapter is responsible for translating desired state into additive subscribe/unsubscribe diffs.
- v1 assumes one shared `Alpaca` websocket connection for the active equities feed, with internal fan-out handled by `MarketData`.

#### Tier model

- `daily_only_retained`
  - symbol is explicitly retained for daily-history coverage
  - symbol is not in any watchlist
  - no live subscriptions
- `watchlist_symbol`
  - symbol is in any watchlist except `Execution`
  - live subscriptions: `bars`, `updatedBars`
- `trading_active`
  - symbol is in the `Execution` watchlist
  - live subscriptions: `bars`, `updatedBars`, `trades`, `quotes`, `status`, `LULD`

Tier precedence:

- `trading_active` > `watchlist_symbol` > `daily_only_retained`

Rules:

- If `bars` are subscribed for a symbol, `updatedBars` must also be subscribed.
- Promotions into higher tiers are immediate.
- Subscription recompute is immediate with short debounce/coalescing.

#### `Execution` watchlist guard rules

- A symbol cannot be removed from `Execution` while an attached strategy is still active.
- A symbol cannot be removed from `Execution` while an open position exists.
- A symbol cannot be removed from `Execution` while open orders exist.
- If `Execution` removal is allowed because the assigned strategy is inactive, the strategy assignment is detached as part of the same business operation before the adapter reacts to the resulting downgraded market-data demand.

#### `Execution` removal behavior

- Valid removal from `Execution` ends `trading_active` behavior immediately.
- The symbol then falls back to:
  - `watchlist_symbol` if it remains in another watchlist
  - `daily_only_retained` if it is explicitly retained and not in any watchlist
  - removed otherwise
- Richer realtime channels may remain during an `execution_exit_grace` transport teardown window.
- `execution_exit_grace` does not preserve `trading_active` business behavior, readiness semantics, or trading eligibility.
- v1 default `execution_exit_grace` is `5` minutes.

#### Non-`Execution` watchlist removal behavior

- Removal from non-`Execution` watchlists uses a grace-based teardown before downgrade/removal.
- v1 standard grace period is `10` minutes.
- After grace expiry, the symbol downgrades to `daily_only_retained` if explicitly retained, or is removed from live demand otherwise.

### Bar finality and correction behavior

- `Alpaca` minute bars should be treated as authoritative realtime truth, but not assumed immutable.
- On first provider close publication from websocket `bars`, `Aegis` should persist the minute bar immediately and treat it as usable for live readiness and indicators.
- Newly closed realtime minute bars should begin in `RevisionEligible` state.
- `updatedBars` should be treated as authoritative revisions to previously emitted minute bars.
- When a materially changed `updatedBar` arrives, `Aegis` should upsert the bar and recompute dependent state from the affected bar forward for the affected symbol/interval.
- Trade corrections and cancel/error events should not trigger Aegis-side trade aggregation; they should drive revalidation and repair behavior.
- Realtime revisionability by itself should not make readiness fail.
- Historical bar retrieval remains the strongest reconciliation truth and should advance bars into `Reconciled` state when confirmed or overwritten.
- Recommended v1 default revision window: `90` seconds after bar close, configurable.

## 8) Architectural implications for Aegis

If `Alpaca` is selected, the `Aegis` market-data adapter should assume:

- a single shared websocket connection per required market-data feed
- adapter-owned subscription diffing from `Aegis` target state into `Alpaca` subscribe/unsubscribe calls
- correction-aware processing for:
  - trade corrections
  - trade cancel/errors
  - updated bars
- tiered live-channel policy based on `daily_only_retained`, `watchlist_symbol`, and `trading_active`
- `Execution` removal guard enforcement before subscription downgrade is permitted
- explicit handling for slow-client conditions and provider-imposed stream limits
- explicit separation between readiness and `limited` operating mode
- no dependency on `IEX` data for production signoff, even though the runtime contract remains the same

The most important design consequence is that `Aegis` must model provider bar revisions cleanly. `updatedBars` and correction events mean previously emitted realtime state may need recompute from the affected bar forward.

## 9) Open validation items

The following items remain open and should be validated before final provider selection or adapter contract finalization:

1. Verify exact historical bar timeframe coverage and pagination behavior in practice.
2. Verify whether `Alpaca` daily bars align with the `Aegis` requirement that daily bars are `RTH`-only.
3. Verify whether `BOATS` and `overnight` feeds are available through the C# SDK or require raw API usage.
4. Verify reconnect and resubscribe expectations for the C# SDK and whether any automatic recovery exists.
5. Run scale-oriented validation for:
   - one shared stream connection
   - high symbol counts
   - subscription churn
   - slow-client behavior
   - updated-bar and correction handling

## 10) Current recommendation

Current evaluation: `partial`

Rationale:

- `Alpaca` appears viable for `Aegis` MarketData at a capability level.
- It already demonstrates strong support for the core v1 equities use cases.
- It should not yet be treated as fully approved for `Aegis` market-data selection until the open validation items above are resolved.

Recommended next steps:

1. Use this checklist and matrix as the standard comparison frame for all market-data providers under consideration.
2. Validate `Alpaca` against the remaining open items.
3. Finalize the `Alpaca` subscription orchestration contract.

## 11) Related documents

- `docs/PROJECT.md`
- `docs/ARCHITECTURE.md`
- `docs/FLOWS.md`
- `docs/modules/MARKET_DATA.md`
- `docs/contracts/MARKET_DATA_READINESS.md`
