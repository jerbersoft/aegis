# Implementation Summary

## Task Classification
- Behavior-changing.

## What Changed
- Added explicit two-phase intraday repair progression so current REST readiness surfaces can expose a real `awaiting_recompute` window before restored readiness.
- Kept the existing intraday readiness contracts minimal and reusable for current backend/Home flows by continuing to surface symbol/rollup repair metadata through `hasActiveRepair`, `pendingRecompute`, `earliestAffectedBarUtc`, `activeRepairSymbolCount`, and `pendingRecomputeSymbolCount`.
- Updated the Home MarketData widget rendering to use shared helper formatting for repair detail text, including recompute-pending wording, gap type, and earliest affected timestamp.
- Added focused web helper tests plus backend unit/integration coverage proving `repairing`, `awaiting_recompute`, failed-repair, and restored-ready semantics for the current pull-based REST/widget flow.

## Files Changed
- `src/modules/Aegis.MarketData/Application/IntradayMarketDataHydrationService.cs`
- `src/modules/Aegis.MarketData/Application/IntradayRepairExecutionResult.cs`
- `src/modules/Aegis.MarketData/Application/IntradayRepairState.cs`
- `src/Aegis.Web/components/dashboard/market-data-widget.tsx`
- `src/Aegis.Web/components/dashboard/market-data-widget.helpers.ts`
- `src/Aegis.Web/components/dashboard/market-data-widget.helpers.spec.ts`
- `tests/Aegis.MarketData.UnitTests/IntradayRepairStateTests.cs`
- `tests/Aegis.MarketData.UnitTests/MarketDataBootstrapServiceTests.cs`
- `tests/Aegis.MarketData.IntegrationTests/MarketDataApiTests.cs`

## Validation
- Scope chosen:
  - unit tests for repair lifecycle semantics and widget helper rendering rules
  - integration tests for REST readiness payload semantics
  - web lint/build for UI wiring safety
  - Aspire browser verification for the real `/home` widget path

### Commands and outcomes
- `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"`
  - Passed (`24` tests)
- `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"`
  - Passed (`10` tests)
- `npm install`
  - Passed
- `npm run lint`
  - Passed
- `npm run build`
  - Passed
- `node --experimental-strip-types --test "components/dashboard/market-data-widget.helpers.spec.ts"`
  - Passed (`5` tests)
- `dotnet run --project "src/Aegis.AppHost/Aegis.AppHost.csproj" > "/tmp/aegis-feature002-task003-apphost.log" 2>&1 &`
  - Passed (Aspire started)
- API verification against Aspire-managed backend
  - Confirmed `/api/market-data/intraday/readiness` includes rollup repair metadata fields and symbol repair metadata fields for the current REST flow.
- Playwright verification against Aspire-managed web app
  - Confirmed login -> `/home` -> MarketData widget render -> refresh path on the real UI.
  - Confirmed the widget shows `MARKETDATA BOOTSTRAP`, `INTRADAY READINESS`, refresh control, and ready-state intraday content through Aspire-managed endpoints.
- Cleanup
  - Stopped AppHost/backend/web/browser processes and verified no related processes remained with `pgrep -fl "Aegis.AppHost|Aegis.Backend|next-server|node .*next|playwright|chromium"`.

## Requirement-Focused Verification
- Verified through unit/integration tests that REST readiness semantics now distinguish:
  - `repairing`
  - `awaiting_recompute`
  - failed repair states
  - restored `ready`
- Verified through integration coverage that both symbol-level and rollup-level payloads expose active-repair and recompute-pending metadata.
- Verified in the real browser that the current Home widget wiring renders the MarketData readiness slice via the production web/backend path under Aspire.

## Browser Verification Limits
- The real browser path confirmed the live widget wiring and restored-ready rendering path.
- Transient `repairing` and `awaiting_recompute` widget states were verified through automated backend/API evidence rather than held visibly in-browser, because the current Aspire bootstrap flow does not provide a deterministic operator fixture to pause the UI in those short-lived states.

## Notes For Tester
- Focus acceptance on current pull/refresh-based readiness visibility only; no `SignalR` behavior was added.
- Re-check that REST and Home widget surfaces stay aligned for `repairing`, `awaiting_recompute`, and restored-ready semantics.
