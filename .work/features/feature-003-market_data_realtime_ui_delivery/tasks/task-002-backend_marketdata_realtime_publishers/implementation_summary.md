## Classification

- behavior-changing

## What changed

- Updated `MarketDataWatchlistSubscriptionRequest` to bind the approved SignalR snake_case request field via `JsonPropertyName("watchlist_id")`, while preserving the existing typed constructor path.
- Reworked watchlist snapshot throttling into true coalescing with deferred flush behavior so in-window updates are retained and emitted after the throttle window even if no later publish arrives.
- Added requirement-focused tests for raw snake_case watchlist subscription binding and deferred watchlist snapshot flush behavior.

## Validation

- `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"` — passed (`29` passed, `0` failed, `0` skipped)
- `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"` — passed (`15` passed, `0` failed, `0` skipped)

## Requirement-focused verification

- Verified authenticated `SubscribeWatchlist` accepts the approved snake_case request payload `{ "watchlist_id": ... }` over SignalR and still returns the expected watchlist ack plus initial snapshot delivery.
- Verified watchlist updates published during the throttle window are coalesced and deferred, then emitted after the throttle window expires even when no subsequent publish triggers another send.

## Remaining for tester

- Re-run task-level review/testing against the updated backend behavior; browser/UI reaction remains downstream scope for `task-003`.
