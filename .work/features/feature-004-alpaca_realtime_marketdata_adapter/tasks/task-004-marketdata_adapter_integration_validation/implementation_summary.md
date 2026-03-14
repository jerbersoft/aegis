# Implementation Summary

## Task classification
- Behavior-changing: yes.
- Reason: this task changes backend runtime wiring so the Alpaca realtime adapter is registered as the MarketData realtime provider foundation and can be started through current application entry points.

## What changed
- Added `MarketDataRealtimeProviderRunner` in `src/Aegis.Backend/MarketData/MarketDataRealtimeProviderRunner.cs` to own application-host startup/shutdown of the shared `IRealtimeMarketDataProvider` runtime.
- Added `MarketDataRealtimeServiceCollectionExtensions` in `src/Aegis.Backend/MarketData/MarketDataRealtimeServiceCollectionExtensions.cs` so backend wiring registers the realtime provider only behind shared contracts (`IRealtimeMarketDataProvider`) while keeping Alpaca-specific construction in composition-root code.
- Updated `src/Aegis.Backend/Program.cs` to use the new realtime registration path instead of ad hoc singleton wiring.
- Extended `src/Aegis.Backend/MarketData/MarketDataRealtimeOptions.cs` with `EnableProviderRuntime` so real-provider runtime activation is explicit rather than an implicit fallback.
- Updated `src/Aegis.Backend/appsettings.json`, `src/Aegis.Backend/appsettings.Development.json`, and `src/Aegis.AppHost/AppHost.cs` so local bootstrap/orchestration keeps realtime provider runtime disabled by default while still wiring the real adapter into DI.
- Reworked `src/adapters/Aegis.Adapters.Alpaca/Services/AlpacaRealtimeMarketDataProvider.cs` so missing realtime credentials and other startup-time configuration faults publish normalized `RealtimeProviderStatusEvent` + `RealtimeProviderErrorEvent` output before the startup failure escapes.
- Updated `src/adapters/Aegis.Adapters.Alpaca/Services/AlpacaRealtimeContractMapper.cs` to normalize the new `ConfigurationInvalid` provider status into the shared status message `configuration_invalid`.
- Added `tests/Aegis.MarketData.UnitTests/MarketDataRealtimeProviderRuntimeTests.cs` covering:
  - DI registration through `IRealtimeMarketDataProvider`
  - hosted runner start/stop behavior when runtime is enabled
  - explicit no-start/no-stop behavior when runtime is disabled
-   - configuration-failure event visibility through the shared provider event stream even when the hosted runner logs the startup exception
- Added `tests/Aegis.Universe.UnitTests/AlpacaRealtimeMarketDataProviderTests.cs` coverage for missing-credentials startup failures so the adapter now has a regression test for normalized configuration-failure semantics.
- Updated `tests/Aegis.MarketData.IntegrationTests/MarketDataRealtimeProviderHostedIntegrationTests.cs` so hosted integration coverage includes the missing-configuration path alongside the existing auth-failure path.

## Requirement-focused verification
- Verified the backend now resolves the realtime adapter through the shared `IRealtimeMarketDataProvider` contract rather than vendor-specific types.
- Verified current runtime entry points can start and stop the registered realtime provider through hosted-service lifecycle wiring.
- Verified bootstrap/dev orchestration remains explicit by leaving realtime provider runtime disabled unless `MarketData:Realtime:EnableProviderRuntime=true` is configured.
- Verified missing realtime credentials now surface through shared normalized provider status/error events instead of logger-only behavior.
- Verified the hosted integration path preserves those normalized configuration-failure events so tester can validate the failure path through the shared provider contract.
- Verified the existing Alpaca realtime adapter unit suite still passes after the integration wiring change.

## Validation executed
- `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj --filter FullyQualifiedName~MarketDataRealtimeProviderRuntimeTests --logger "console;verbosity=minimal"`
  - First run: failed (`StartCalls` assertion and missing async-dispose handling in the new tests).
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtime --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 35, Skipped: 0, Total: 35`
- `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj --filter FullyQualifiedName~MarketDataRealtimeProviderRuntimeTests --logger "console;verbosity=minimal"`
  - Second run: failed (missing `IOptions<MarketDataRealtimeOptions>` registration in the new unit test container).
- `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj --filter FullyQualifiedName~MarketDataRealtimeProviderRuntimeTests --logger "console;verbosity=minimal"`
  - Third run: failed (missing logger registration in the new unit test container).
- `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj --filter FullyQualifiedName~MarketDataRealtimeProviderRuntimeTests --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 3, Skipped: 0, Total: 3`
- `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 32, Skipped: 0, Total: 32`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 44, Skipped: 0, Total: 44`
- `dotnet build aegis.sln --no-restore`
  - Result: passed
  - Outcome: `Build succeeded. 0 Warning(s), 0 Error(s)`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~StartAsync_WhenCredentialsAreMissing_ShouldEmitNormalizedConfigurationFailureEvents --logger "console;verbosity=minimal"`
  - First run: failed to compile because `AlpacaRealtimeOptions.ApiKey` / `ApiSecret` are init-only and the new test assigned them after construction.
- `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj --filter FullyQualifiedName~MarketDataRealtimeProviderRuntimeTests --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`
- `dotnet test tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj --filter FullyQualifiedName~MarketDataRealtimeProviderHostedIntegrationTests --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~StartAsync_WhenCredentialsAreMissing_ShouldEmitNormalizedConfigurationFailureEvents --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1`
- `dotnet test tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj --filter FullyQualifiedName~AlpacaRealtime --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 36, Skipped: 0, Total: 36`
- `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 33, Skipped: 0, Total: 33`
- `dotnet test tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj --filter FullyQualifiedName~MarketDataRealtimeProviderHostedIntegrationTests --logger "console;verbosity=minimal"`
  - Result: passed
  - Outcome: `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`
- `dotnet build aegis.sln --no-restore`
  - Result: passed
  - Outcome: `Build succeeded. 0 Warning(s), 0 Error(s)`

## Not executed / remaining for tester
- No developer-authored integration tests were added because this task’s developer scope is limited to application code and unit-level validation.
- No live Alpaca credential-backed runtime verification was executed; proving real websocket/auth behavior still requires credentialed tester/runtime validation.
- Tester should verify both enabled-runtime success behavior and the new configuration-failure path by observing shared `IRealtimeMarketDataProvider` events/status semantics when `MarketData:Realtime:EnableProviderRuntime=true` but realtime credentials are missing or invalid.
