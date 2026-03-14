# Testing Results

## Task
- Feature: `feature-002-market_data_intraday_repair_progression`
- Task: `task-003-intraday_repair_visibility`
- Classification: behavior-changing

## Verification scope selected
- Unit tests for repair lifecycle and widget helper semantics.
- Integration tests for backend/API readiness payload semantics.
- Web lint/build plus helper tests for UI wiring safety.
- Real browser path under `Aegis.AppHost` for login -> `/home` -> MarketData widget.

## Commands executed
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
  - Passed; Aspire started and exposed the web app at `http://127.0.0.1:3001`
- Capability checks under Aspire
  - `curl -I "http://127.0.0.1:3001/login"` -> Passed (`200 OK`)
  - `curl -i "http://127.0.0.1:3001/api/auth/session"` -> Passed (`401 Unauthorized` before login, confirming endpoint reachability)
- `node ".work/features/feature-002-market_data_intraday_repair_progression/tasks/task-003-intraday_repair_visibility/browser_verify_task003.mjs"`
  - Passed
- Cleanup
  - Stopped related AppHost/backend/web/browser processes with `pkill` and confirmed no matching processes remained via `pgrep`

## What was verified

### Backend/API proof
Verified by automated unit/integration coverage:
- Intraday readiness distinguishes `repairing`, `awaiting_recompute`, failed repair states, and restored `ready`.
- Symbol payloads expose `hasActiveRepair`, `pendingRecompute`, `activeGapType`, `activeGapStartUtc`, and `earliestAffectedBarUtc`.
- Rollup payloads expose `activeRepairSymbolCount`, `pendingRecomputeSymbolCount`, and rollup `earliestAffectedBarUtc`.
- Readiness is restored only after recompute/validation succeeds.
- Failed repair fetch/validation remains visible as `repairing` instead of incorrectly restoring `ready`.

Primary evidence:
- `tests/Aegis.MarketData.IntegrationTests/MarketDataApiTests.cs`
- `tests/Aegis.MarketData.UnitTests/MarketDataBootstrapServiceTests.cs`
- `tests/Aegis.MarketData.UnitTests/IntradayRepairStateTests.cs`

### Web/UI proof
Verified by automated web checks:
- Widget helper text correctly renders recompute-pending wording and affected timestamp precedence.
- Web types and fetch wiring compile and lint cleanly for the updated readiness payloads.

Primary evidence:
- `src/Aegis.Web/components/dashboard/market-data-widget.helpers.spec.ts`
- `npm run lint`
- `npm run build`

### Browser-path proof under `Aegis.AppHost`
Verified in a real browser session against Aspire-managed URLs only:
- Opened `/login`.
- Logged in successfully.
- Opened `/home`.
- Confirmed the MarketData widget rendered with `MARKETDATA BOOTSTRAP`, `INTRADAY READINESS`, and `Refresh`.
- Confirmed the widget rendered ready-state intraday detail for `AMD` through the live web/backend path.
- Confirmed Aspire-managed API responses for `/api/market-data/intraday/readiness` and `/api/market-data/intraday/readiness/AMD` matched the ready UI path.

Artifacts:
- `browser-home-pre-bootstrap.png`
- `browser-home-post-bootstrap.png`

## Browser coverage boundary
- In-browser proof covered the real production wiring and restored-ready rendering path.
- Transient `repairing` and `awaiting_recompute` UI states were **not** deterministically held in the browser because the current Aspire bootstrap flow does not provide a stable operator fixture for those short-lived states.
- Those transient states were verified through automated backend/API evidence instead, which is non-blocking for this task because the semantics are directly covered by integration/unit tests and the real browser path proved the UI wiring.

## Outcome
- Result: **pass**
- Rework needed: **no defects found during verification**
