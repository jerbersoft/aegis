# Implementation Summary

## Task classification

- behavior-changing

## What changed

- Updated `src/Aegis.Shared/Contracts/MarketData/MarketDataRealtimeContracts.cs` so the actual SignalR wire payload uses the documented `snake_case` field names via explicit JSON property names.
- Updated `src/Aegis.Backend/MarketData/MarketDataRealtimeOrchestrator.cs` so Home refresh hints schedule a deferred flush for in-window changes instead of silently suppressing them when no later publish occurs.
- Extended unit coverage in `tests/Aegis.MarketData.UnitTests/MarketDataRealtimeOrchestratorTests.cs` to verify:
  - raw serialized contract fields are `snake_case`
  - Home in-window changes are emitted by a later deferred refresh hint
  - explicit flush behavior still coalesces correctly at the throttle boundary
- Extended integration coverage in `tests/Aegis.MarketData.IntegrationTests/MarketDataApiTests.cs` to assert raw SignalR payload field names from received JSON, not only typed deserialization.

## Validation

- `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"` ✅ (`28` passed, `0` failed, `0` skipped)
- `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"` ✅ (`14` passed, `0` failed, `0` skipped)

## Requirement-focused verification

- Verified actual serialized watchlist snapshot payloads contain documented `snake_case` fields such as `contract_version`, `watchlist_id`, `batch_sequence`, `current_price`, and `percent_change`.
- Verified actual serialized Home refresh payloads contain documented `snake_case` fields such as `event_id`, `occurred_utc`, `requires_refresh`, and `changed_scopes`.
- Verified camelCase variants are absent in the raw received SignalR JSON payloads.
- Verified Home refresh throttling now preserves in-window changes by emitting a later coalesced refresh hint after the throttle window expires, even without another publish call.

## Notes for tester

- Server-side contract and throttling fixes are covered; no browser validation was added because the web realtime consumer remains out of scope for `task-003`.
