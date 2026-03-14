# Implementation Summary

## Task classification
- Behavior-changing: yes.
- Reason: this task changes realtime adapter subscription behavior and exposes provider capability metadata consumed by MarketData-facing code.

## What changed
- Updated `src/adapters/Aegis.Adapters.Alpaca/Services/AlpacaRealtimeMarketDataProvider.cs` to preserve replace-all desired-state semantics while translating them into additive Alpaca subscribe/unsubscribe diffs instead of full resubscribe churn.
- Added deterministic stream-key-based subscription tracking so unchanged subscriptions stay attached, removed subscriptions unsubscribe first, and added subscriptions subscribe afterward.
- Reworked `ApplySubscriptionSnapshotAsync(...)` so connected-session subscribe/unsubscribe failures are normalized into `RealtimeProviderErrorEvent` semantics instead of escaping as raw provider exceptions.
- Reworked shutdown/publish coordination so `StopAsync(...)` cancels a dedicated publish token and releases blocked bounded-channel writers rather than allowing stop to hang under backpressure.
- Stabilized `PublishEvent(...)` under suite execution by snapshotting the active publish token before the blocking write and using that same token in the cancellation filter, eliminating the race where `_publishCts` changed to `null` before the catch filter evaluated and let `OperationCanceledException` escape.
- Extended `src/Aegis.Shared/Ports/MarketData/RealtimeMarketDataContracts.cs` capability metadata to explicitly report historical-batch support, revision-event support, incremental subscription-change support, partial-failure behavior, and runtime symbol-limit metadata.
- Updated `src/adapters/Aegis.Adapters.Alpaca/Services/AlpacaRealtimeContractResolver.cs` to report Alpaca capability flags without leaking vendor-specific conditionals upstream.
- Extended `src/adapters/Aegis.Adapters.Alpaca/Services/AlpacaRealtimeContractMapper.cs` error normalization so rate-limit, subscription-limit, and rejected-symbol style failures map into stable shared error semantics.
- Added/updated unit tests in:
  - `tests/Aegis.Universe.UnitTests/AlpacaRealtimeMarketDataProviderTests.cs`
  - `tests/Aegis.Universe.UnitTests/AlpacaRealtimeContractMapperTests.cs`

## Requirement-focused verification
- Verified unchanged desired-state snapshots are no-op updates and do not trigger unnecessary unsubscribe/resubscribe calls.
- Verified additive expansion of desired subscriptions only subscribes the newly required Alpaca stream instead of replacing the entire set.
- Verified replacement/removal still unsubscribes stale streams before new subscriptions are applied.
- Verified connected-session subscription update failures now surface as normalized provider-error events instead of leaking raw exceptions through the shared boundary.
- Verified stop/shutdown cancels blocked bounded-channel writers and completes under backpressure.
- Verified the bounded-channel shutdown path remains stable when the full provider test suite runs, not just in isolated test execution.
- Verified capability reporting exposes the adapter-side support flags required by this task.
- Verified normalized provider-error mapping covers timeout, rate-limit, subscription-limit, and rejected-subscription style failures.

## Validation executed
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtimeContractMapperTests --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 20, Skipped: 0, Total: 20`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtimeMarketDataProviderTests --logger "console;verbosity=minimal"`
  - Intermediate run result: failed to compile because the new test used `Returns(...)` against a `ValueTask`; fixed by switching to `When(...).Do(...)`.
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtimeMarketDataProviderTests --logger "console;verbosity=minimal"`
  - Intermediate run result: timed out while proving shutdown under backpressure with the initial test shape; fixed by directly seeding provider internals for the bounded-channel shutdown case and finalizing the stop/backpressure assertion.
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj -m:1 --filter FullyQualifiedName~ApplySubscriptionSnapshotAsync_WhenLiveUpdateFails_ShouldEmitNormalizedProviderError --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj -m:1 --filter FullyQualifiedName~StopAsync_WhenEventChannelIsFull_ShouldNotHang --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj -m:1 --filter FullyQualifiedName~AlpacaRealtimeMarketDataProviderTests --logger "console;verbosity=minimal"`
  - Result: passed after the `PublishEvent(...)` token-snapshot race fix
  - Outcome: `Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj -m:1 --filter FullyQualifiedName~AlpacaRealtimeContractMapperTests --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 20, Skipped: 0, Total: 20`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj -m:1 --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 44, Skipped: 0, Total: 44`
- `dotnet build aegis.sln -m:1`
  - Result: passed
  - Outcome: `Build succeeded. 0 Warning(s), 0 Error(s)`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtimeMarketDataProviderTests --logger "console;verbosity=minimal"`
  - First run result: failed due to test setup (`NSubstitute CouldNotSetReturnDueToNoLastCallException` in new tests).
  - Follow-up: fixed test setup by capturing the market clock substitute before `Returns(...)`.
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtimeMarketDataProviderTests --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 42, Skipped: 0, Total: 42`

## Deferred / higher-level follow-up
- No live Alpaca credential-backed websocket verification was run in this task.
- Final MarketData integration wiring and end-to-end adapter consumption remain reserved for `task-004`.

## Notes for tester
- Focus on adapter-side subscription diff behavior, no-op updates, capability reporting, and normalized failure semantics.
- Live-provider validation is still useful but not required for this task’s unit-level implementation proof.
