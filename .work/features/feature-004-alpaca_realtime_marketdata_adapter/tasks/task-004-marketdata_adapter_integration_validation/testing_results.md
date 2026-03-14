# Testing Results

## Task
- Feature: `feature-004` — `feature-004-alpaca_realtime_marketdata_adapter`
- Task: `task-004` — `task-004-marketdata_adapter_integration_validation`
- Tester: `tester`
- Date: 2026-03-14

## Verification scope
- Classified as behavior-changing work.
- Chosen depth: unit + integration verification.
- Reason: rework specifically changes startup failure behavior so missing/invalid realtime configuration must now be proven observable through shared realtime-provider semantics instead of logger-only handling.

## Rework focus verified
- Verified missing realtime credentials now emit normalized shared `RealtimeProviderStatusEvent` and `RealtimeProviderErrorEvent` output before startup failure escapes.
- Verified hosted runner behavior does not swallow those configuration-failure events even though it still logs the startup exception.
- Verified hosted lifecycle coverage still preserves normalized auth-failure behavior alongside the new configuration-failure behavior.

## Code evidence reviewed
- `src/adapters/Aegis.Adapters.Alpaca/Services/AlpacaRealtimeMarketDataProvider.cs`
- `src/adapters/Aegis.Adapters.Alpaca/Services/AlpacaRealtimeContractMapper.cs`
- `tests/Aegis.Universe.UnitTests/AlpacaRealtimeMarketDataProviderTests.cs`
- `tests/Aegis.MarketData.UnitTests/MarketDataRealtimeProviderRuntimeTests.cs`
- `tests/Aegis.MarketData.IntegrationTests/MarketDataRealtimeProviderHostedIntegrationTests.cs`

## Exact validation commands and outcomes
1. `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~StartAsync_WhenCredentialsAreMissing_ShouldEmitNormalizedConfigurationFailureEvents --logger "console;verbosity=minimal"`
   - Result: passed
   - Outcome: `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1`

2. `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj --filter FullyQualifiedName~HostedRunner_ShouldLeaveConfigurationFailureEventsAvailableThroughProviderStream --logger "console;verbosity=minimal"`
   - Result: passed
   - Outcome: `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1`

3. `dotnet test tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj --filter FullyQualifiedName~HostLifecycle_ShouldSurfaceNormalizedFailureEvents_WhenRealtimeConfigurationIsMissing --logger "console;verbosity=minimal"`
   - Result: passed
   - Outcome: `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1`

4. `dotnet test tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj --filter FullyQualifiedName~HostLifecycle_ShouldSurfaceNormalizedFailureEvents_WhenAuthenticationFails --logger "console;verbosity=minimal"`
   - Result: passed
   - Outcome: `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1`

5. `dotnet test tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj --filter FullyQualifiedName~MarketDataRealtimeProviderHostedIntegrationTests --logger "console;verbosity=minimal"`
   - Result: passed
   - Outcome: `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`

6. `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj --filter FullyQualifiedName~MarketDataRealtimeProviderRuntimeTests --logger "console;verbosity=minimal"`
   - Result: passed
   - Outcome: `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`

7. `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtime --logger "console;verbosity=minimal"`
   - Result: passed
   - Outcome: `Passed! - Failed: 0, Passed: 36, Skipped: 0, Total: 36`

8. `dotnet build aegis.sln --no-restore`
   - Result: passed
   - Outcome: `Build succeeded. 0 Warning(s), 0 Error(s)`

## Requirement-focused result
- Pass: missing realtime configuration is now surfaced through normalized shared provider semantics, not just logger output.
- Pass: the emitted startup-failure semantics include both a shared provider status (`ConfigurationInvalid` / `configuration_invalid`) and a shared provider error (`invalid_operation`, non-transient).
- Pass: hosted runner lifecycle still leaves those events observable on the provider stream after logging the startup exception.
- Pass: auth-failure coverage still passes, so the rework did not regress the other normalized startup-failure path.

## Skipped / deferred coverage
- No browser verification was run.
  - Reason: task scope remains backend/runtime integration only, and `TASK.md` says browser verification is not expected unless a user-visible path was added.
- No live Alpaca credential-backed runtime verification was run.
  - Reason: credentials were not provided. This re-verification proves normalized missing/invalid configuration semantics through automated unit/integration evidence, but not live Alpaca websocket behavior.
- No Aspire-managed runtime verification was run.
  - Reason: the rework target was shared startup-failure semantics, which the focused unit/integration layers prove more directly and deterministically.

## Issues found
- None in the rework focus.

## Conclusion
- Rework verification passed.
- `testing_results.md` updated in the canonical main-workspace task folder.
