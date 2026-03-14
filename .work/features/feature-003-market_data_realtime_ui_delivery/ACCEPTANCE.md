# Feature Acceptance — feature-003 MarketData realtime UI delivery

## Covered tasks

- `task-001` — `task-001-signalr_contract_and_delivery_model`
- `task-002` — `task-002-backend_marketdata_realtime_publishers`
- `task-003` — `task-003-web_realtime_consumers`

## Delivered

- Authenticated `SignalR` market-data hub delivery is in place at the approved backend path.
- Home now receives market-data refresh hints while keeping pull endpoints authoritative.
- Watchlists now renders live `current price` and `percent change` values from MarketData instead of placeholder fallback values.
- Realtime delivery uses the approved `snake_case` contract and bounded coalescing/throttling behavior.

## Run the feature

From the implementation worktree:

`/Users/herbertsabanal/Projects/.aegis-worktrees/feature-003-market_data_realtime_ui_delivery-impl-01`

1. Start Aspire: `dotnet run --project "src/Aegis.AppHost/Aegis.AppHost.csproj"`
2. Open the web app at `http://localhost:3001`
3. Sign in through the normal cookie-auth login flow.

If you need market-data-ready watchlist content for verification, use a watchlist with at least one symbol and run the existing MarketData bootstrap flow before checking realtime behavior.

## What to verify

### 1. Home realtime wiring
- Open `/home`.
- Confirm the `MarketData Bootstrap` surface is visible.
- After bootstrap completes, confirm Home shows realtime status copy indicating market-data refresh hints are active.

Expected outcome:

- Home shows `LIVE` and `Home refresh hints active`.

### 2. Watchlists live market values
- Open `/watchlists`.
- View a watchlist with at least one symbol.

Expected outcome:

- The page shows a live status badge, not an offline fallback.
- Watchlists shows `Live watchlist prices • as of ... UTC`.
- Symbol rows show numeric price and percent-change values.
- The previous placeholder/offline state does not appear (`OFFLINE` and `— / —` should not remain after bootstrap when realtime data is available).

### 3. Basic reconnect/fallback confidence
- Refresh the page or navigate away and back to `Home` and `Watchlists`.

Expected outcome:

- The pages recover without losing the current realtime wiring.
- Pull/bootstrap behavior still works if realtime updates are briefly unavailable.

## Acceptance evidence already recorded

- `npm test` in `src/Aegis.Web` passed (`14/14`).
- `npm run lint` in `src/Aegis.Web` passed.
- `dotnet test "aegis.sln"` passed (`58/58`).
- AppHost browser verification passed for authenticated `Home` and `Watchlists` flows.

## Caveats

- Push is intentionally not the source of truth; authoritative current state still comes from existing pull endpoints.
- Reconnect/resubscribe semantics and wire-contract details were primarily proven by automated tests; owner acceptance should focus on the visible Home and Watchlists behavior above.
