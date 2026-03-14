# Feature Acceptance

## Feature
- Feature ID: `feature-004`
- Title: `Alpaca realtime MarketData adapter`

## Tasks covered by this guide
- `task-001` - Alpaca SDK contract alignment
- `task-002` - Alpaca streaming client adapter
- `task-003` - Alpaca subscription diffing and capability reporting
- `task-004` - MarketData adapter integration and validation

## What this feature is ready to accept
- The real Alpaca realtime adapter is implemented behind the shared `IRealtimeMarketDataProvider` boundary.
- Shared contracts now cover normalized realtime bars, updated bars, trades, quotes, provider status, market status, corrections, cancels, and provider errors.
- The adapter applies replace-all desired subscription state as Alpaca-native subscribe/unsubscribe diffs.
- Backend wiring resolves and hosts the realtime provider through shared contracts, with runtime activation explicitly controlled by configuration.
- Missing/invalid realtime configuration now surfaces normalized provider status/error events instead of logger-only failures.

## How to run the app
Use the Aspire host from the repository root:

```bash
dotnet run --project src/Aegis.AppHost
```

Notes:
- Local bootstrap keeps realtime provider runtime disabled by default.
- To exercise the real provider runtime path, enable `MarketData__Realtime__EnableProviderRuntime=true` and provide Alpaca realtime configuration under `Alpaca__Realtime__...` before starting the host.
- This feature does not require browser-based acceptance because no user-facing UI path was added.

## Acceptance path A - required automated acceptance
Run these commands from the repository root:

```bash
dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtime --logger "console;verbosity=minimal"
dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj --filter FullyQualifiedName~MarketDataRealtimeProviderRuntimeTests --logger "console;verbosity=minimal"
dotnet test tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj --filter FullyQualifiedName~MarketDataRealtimeProviderHostedIntegrationTests --logger "console;verbosity=minimal"
dotnet build aegis.sln --no-restore
```

Expected outcomes:
- All commands pass.
- Alpaca adapter tests prove normalized event mapping, reconnect/start-stop behavior, bounded-channel backpressure, and feed-aware client configuration.
- Runtime/provider tests prove DI registration through `IRealtimeMarketDataProvider`, hosted start/stop behavior, and explicit no-start behavior when realtime runtime is disabled.
- Hosted integration tests prove normalized failure events for missing configuration and authentication failure.

## Acceptance path B - optional credential-backed smoke check
Only if Alpaca realtime credentials are available:

1. Set `MarketData__Realtime__EnableProviderRuntime=true`.
2. Provide valid `Alpaca__Realtime__ApiKey`, `Alpaca__Realtime__ApiSecret`, and desired environment/feed settings.
3. Start `Aegis.AppHost`.

Expected outcomes:
- The app starts with realtime provider runtime enabled.
- No immediate normalized `configuration_invalid` startup failure is emitted.
- The provider can be hosted through the shared runtime path rather than fake/bootstrap-only wiring.

## Acceptance caveats
- Live Alpaca websocket verification was not completed in the approved task artifacts because credentials were not available.
- Acceptance is therefore ready based on automated unit/integration/build evidence plus explicit documentation of the credential-gated gap.
- Realtime runtime remains intentionally disabled by default in local bootstrap until explicitly enabled.
