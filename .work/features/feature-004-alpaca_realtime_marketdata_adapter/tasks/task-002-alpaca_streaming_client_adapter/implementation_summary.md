# Implementation Summary

## Task classification
- Behavior-changing: yes.
- Reason: this task adds the realtime Alpaca streaming provider runtime, subscription application, reconnect loop behavior, and normalized event emission through the shared MarketData realtime boundary.

## What changed
- Added `AlpacaRealtimeMarketDataProvider` in `src/adapters/Aegis.Adapters.Alpaca/Services/AlpacaRealtimeMarketDataProvider.cs` implementing `IRealtimeMarketDataProvider` with:
  - SDK-backed connect/authenticate/disconnect lifecycle
  - bounded `ChannelReader<RealtimeMarketDataEvent>` delivery
  - adapter-owned reconnect loop with backoff
  - replace-all subscription snapshot application against Alpaca additive subscribe/unsubscribe APIs
  - normalized provider status, market status, trade, quote, bar, correction, cancel, and provider-error events
- Added `IAlpacaRealtimeClientFactory` and `AlpacaRealtimeClientFactory` so SDK client creation stays inside the adapter and is unit-testable.
- Reworked `AlpacaRealtimeClientFactory` so realtime feed selection is applied to the actual Alpaca SDK websocket endpoint, not just reported in metadata/capabilities.
- Added `BuildDataStreamingClientConfiguration(...)` in `AlpacaRealtimeClientFactory` to override the SDK environment default websocket path with the configured feed-specific endpoint (`iex`/`sip`/`otc`).
- Extended `AlpacaRealtimeContractMapper` failure normalization to cover websocket/socket/http failure classes and more accurate transient classification.
- Wired realtime adapter services in `src/Aegis.Backend/Program.cs` and added `Alpaca:Realtime` configuration in backend appsettings files.
- Added unit tests in `tests/Aegis.Universe.UnitTests/AlpacaRealtimeMarketDataProviderTests.cs` covering:
  - connect/authenticate + initial event/status emission
  - live subscription replacement behavior
  - bounded-channel backpressure behavior
  - normalized failure emission on connection failure
- Added `tests/Aegis.Universe.UnitTests/AlpacaRealtimeClientFactoryTests.cs` covering feed-specific runtime endpoint selection for both paper/live environments.

## Requirement-focused verification
- Verified the provider emits normalized provider-status and market-status events on successful startup before downstream realtime data is consumed.
- Verified desired replace-all subscriptions are translated into unsubscribe/subscribe operations and old handlers stop emitting after replacement.
- Verified bounded channel behavior blocks producers when capacity is exhausted rather than dropping into unbounded callback work.
- Verified runtime connection failure is surfaced as a normalized `RealtimeProviderErrorEvent` without leaking Alpaca SDK types beyond the adapter.
- Verified the configured realtime feed now changes the actual Alpaca SDK websocket endpoint used at runtime, removing the prior mismatch between reported feed metadata and effective connection behavior.

## Validation executed
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtimeClientFactoryTests --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtimeMarketDataProviderTests --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj`
  - Result: passed
  - Outcome: `Passed!  - Failed: 0, Passed: 36, Skipped: 0, Total: 36`
- `dotnet build aegis.sln`
  - Result: passed
  - Outcome: `Build succeeded. 0 Warning(s), 0 Error(s)`

## Deferred / not fully verified
- No live Alpaca credential or websocket session verification was executed in this task.
- No MarketData integration-path consumption was implemented or validated here; that remains for later feature tasks/tester coverage.
- Subscription diff optimization and explicit capability-orchestration behavior remain reserved for `task-003`; this task only implements the minimal replace-all subscription path needed by the streaming adapter.

## Notes for tester
- Focus tester validation on adapter lifecycle behavior, normalized event semantics, reconnect/error surfacing, and confirmation that no vendor SDK types cross the shared boundary.
- Live-provider validation is still needed with real credentials/environment because current proof is unit-level plus solution build only.
