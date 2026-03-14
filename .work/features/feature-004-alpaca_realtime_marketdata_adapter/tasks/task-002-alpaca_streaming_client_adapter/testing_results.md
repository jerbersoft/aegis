# Testing Results

## Task
- Feature: `feature-004` — `feature-004-alpaca_realtime_marketdata_adapter`
- Task: `task-002` — `task-002-alpaca_streaming_client_adapter`

## Verification classification
- Behavior-changing: yes.
- Chosen scope: focused unit verification plus solution build.
- Why this scope: the rework fixes an adapter-runtime bug where configured realtime feed selection was not guaranteed to affect the actual Alpaca SDK websocket endpoint. This is most directly verified with deterministic unit tests around SDK client configuration plus the existing adapter regression suite; UI/browser coverage is not applicable for this task and live Alpaca verification was not available in the provided environment.

## What was verified
- Successful startup emits normalized provider-status events and market-status events before realtime payload delivery.
- Replace-all subscription snapshots translate into unsubscribe/subscribe behavior and detach stale handlers.
- Bounded channel behavior applies backpressure when capacity is exhausted.
- Connection failures surface as normalized `RealtimeProviderErrorEvent` values.
- Reconnect behavior re-establishes a new client session after socket close and reapplies the desired subscription snapshot.
- The configured realtime feed is now applied to the actual Alpaca SDK data-streaming client configuration used to create the websocket client, so runtime endpoint selection matches reported feed metadata/capabilities.
- Vendor SDK usage remains confined to the Alpaca adapter project; shared contracts remain vendor-neutral.

## Rework evidence reviewed
- `src/adapters/Aegis.Adapters.Alpaca/Services/AlpacaRealtimeClientFactory.cs` now builds an `AlpacaDataStreamingClientConfiguration`, overrides its websocket endpoint with the configured feed segment, and creates the SDK client from that configuration.
- `tests/Aegis.Universe.UnitTests/AlpacaRealtimeClientFactoryTests.cs` adds regression coverage for feed-specific runtime endpoint selection across paper/live environments.

## Validation executed
1. `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtimeClientFactoryTests --logger "console;verbosity=minimal"`
   - Result: passed
   - Outcome: `Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5`
2. `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtimeMarketDataProviderTests --logger "console;verbosity=minimal"`
   - Result: passed
   - Outcome: `Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5`
3. `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --logger "console;verbosity=minimal"`
   - Result: passed
   - Outcome: `Passed! - Failed: 0, Passed: 36, Skipped: 0, Total: 36`
4. `dotnet build aegis.sln`
   - Result: passed
   - Outcome: `Build succeeded. 0 Warning(s), 0 Error(s)`

## Additional evidence
- Source inspection confirmed `AlpacaRealtimeClientFactory.CreateDataStreamingClient(...)` now uses `BuildDataStreamingClientConfiguration(...)` followed by `ConfigurationExtensions.GetClient(configuration)`, so the feed-specific endpoint override is on the actual SDK runtime path.
- Source inspection confirmed `Alpaca.Markets` references are limited to `src/adapters/Aegis.Adapters.Alpaca/**` and test files; `src/Aegis.Shared/Ports/MarketData/RealtimeMarketDataContracts.cs` remains vendor-neutral.

## Skipped / deferred coverage
- Live Alpaca websocket verification was not executed because no task-scoped realtime credentials/session were configured in the provided environment.
- Browser/Aspire verification was not executed because this task does not introduce a user-facing browser path; adapter semantics were verified at the unit layer instead.
- Full MarketData integration-path consumption remains for later feature tasks; this task-level verification covered the adapter boundary itself.

## Outcome
- Result: pass
- Rework needed: no
- Notes: review-driven feed-selection rework is now covered with automated proof that the configured feed affects the actual Alpaca SDK websocket client configuration; live-provider environment coverage remains deferred and non-blocking for this task.
