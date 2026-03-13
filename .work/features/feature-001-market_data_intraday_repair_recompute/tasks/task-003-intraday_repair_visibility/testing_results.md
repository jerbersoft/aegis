# Testing Results

## Task
- Feature: `feature-001-market_data_intraday_repair_recompute`
- Task: `task-003-intraday_repair_visibility`
- Classification: behavior-changing

## Chosen verification scope
- Integration + unit + Aspire browser verification.
- Reason: this task changes backend readiness payloads and Home widget visibility semantics. Automated tests cover backend/runtime repair metadata and restored-ready behavior, while Aspire browser verification confirms the `/home` MarketData widget renders the current intraday readiness slice through the real web/backend path.

## Exact commands run
- `dotnet run --project "src/Aegis.AppHost/Aegis.AppHost.csproj" > "/Users/herbertsabanal/Projects/aegis/.work/features/feature-001-market_data_intraday_repair_recompute/tasks/task-003-intraday_repair_visibility/apphost.log" 2>&1 &`
- `curl -I http://127.0.0.1:3001`
- `curl -I http://127.0.0.1:5078/health`
- `node ".work/features/feature-001-market_data_intraday_repair_recompute/tasks/task-003-intraday_repair_visibility/browser_verify_task003.mjs" > ".work/features/feature-001-market_data_intraday_repair_recompute/tasks/task-003-intraday_repair_visibility/browser_verify_output.json"`
- `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"`
- `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"`
- `pkill -f "dotnet run --project src/Aegis.AppHost/Aegis.AppHost.csproj"; pkill -f "/Users/herbertsabanal/Projects/aegis/src/Aegis.AppHost/bin/Debug/net10.0/Aegis.AppHost"; pkill -f "Aegis.Backend/Aegis.Backend.csproj"; pkill -f "/Users/herbertsabanal/Projects/aegis/src/Aegis.Backend/bin/Debug/net10.0/Aegis.Backend"; pkill -f "node /Users/herbertsabanal/Projects/aegis/src/Aegis.Web/node_modules/.bin/next dev"; pkill -f "next-server \(v16.1.6\)"; pkill -f "browser_verify_task003.mjs"; pkill -f "playwright|chromium"`
- `pgrep -fl "Aegis.AppHost|Aegis.Backend|next-server|node .*next|browser_verify_task003|playwright|chromium"`

## Outcomes

### Passed
- `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"`
  - Passed: 20, Failed: 0, Skipped: 0
  - Verified symbol/readiness view mapping exposes `hasActiveRepair`, `pendingRecompute`, and `earliestAffectedBarUtc`.
  - Verified rollup mapping exposes `activeRepairSymbolCount`, `pendingRecomputeSymbolCount`, and rollup `earliestAffectedBarUtc`.
  - Verified repairing states retain repair visibility when validation/fetch fails.
- `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"`
  - Passed: 9, Failed: 0, Skipped: 0
  - Verified REST readiness surfaces return active repair metadata for repairing symbols.
  - Verified rollup readiness exposes active-repair and pending-recompute counts plus earliest affected timestamp.
  - Verified restored-ready responses clear active repair metadata.
- Aspire browser verification:
  - `Aegis.AppHost` started first and exposed working web/backend endpoints (`http://127.0.0.1:3001`, `http://127.0.0.1:5078`).
  - Verified `/home` renders the MarketData widget through the Aspire-managed web app after login.
  - Verified restored-ready intraday rollup/widget state through the real UI and REST surfaces:
    - rollup readiness state = `ready`
    - `activeRepairSymbolCount = 0`
    - `pendingRecomputeSymbolCount = 0`
    - `earliestAffectedBarUtc = null`
    - symbol detail row rendered as `AMD: ready ...`
    - ready state no longer showed repair text
  - Evidence artifacts created:
    - `browser_verify_output.json`
    - `browser-home-pre-bootstrap.png`
    - `browser-home-post-bootstrap.png`
    - `apphost.log`
  - Process cleanup verified:
    - `pgrep -fl ...` returned no related Aspire/backend/web/browser-test processes.
    - `http://127.0.0.1:3001/login` and `http://127.0.0.1:5078/health` refused connections after cleanup.

## Requirement-focused verification status
- Verified in automated tests: backend per-symbol and rollup visibility for `repairing`, repair failure states, awaiting-recompute metadata, earliest affected timestamp, and restored readiness.
- Verified in the browser via Aspire-managed endpoints: `/home` MarketData widget renders the intraday slice and shows the restored-ready state after refresh/bootstrap.
- Verified in the browser via Aspire-managed endpoints: the real widget no longer shows repair metadata once the symbol is ready.
- Not fully verified in the browser: active `repairing` / `awaiting_recompute` widget states, repairing symbol counts, earliest affected timestamp text, and per-symbol repair detail text with gap type/timestamp.

## Skipped / blocked checks
- Browser exercise gap for live repair-specific widget states.
- Reason: the available Aspire/dev runtime uses the fake historical provider happy path and the current operator UI exposes no direct control to induce an in-browser `repairing` or `awaiting_recompute` state. Those distinctions were therefore verified through unit/integration coverage and REST snapshots, not through an on-screen transient repair scenario.

## Browser verification evidence
- `browser-home-pre-bootstrap.png`
  - Captured `/home` widget before the final scripted refresh in this session.
- `browser-home-post-bootstrap.png`
  - Captured `/home` widget after the scripted refresh/bootstrap; showed:
    - `INTRADAY READINESS`
    - `1 ready / 0 not ready (1min)`
    - `State: ready`
    - `AMD: ready (780/100) • indicators ready • buzz 105.1%`
- `browser_verify_output.json`
  - Recorded matching REST evidence from the same browser session:
    - rollup `readinessState: ready`
    - rollup `activeRepairSymbolCount: 0`
    - rollup `pendingRecomputeSymbolCount: 0`
    - rollup `earliestAffectedBarUtc: null`
    - symbol `AMD` with `hasActiveRepair: false` and `pendingRecompute: false`

## Remaining gap / follow-up
- To fully complete the requested browser-only distinction coverage, the task still needs an Aspire-driven scenario that can hold the UI in an actual `repairing` or `awaiting_recompute` state long enough to observe:
  - rollup intraday readiness state = `repairing`
  - repairing symbol count
  - awaiting recompute count when present
  - earliest affected timestamp text
  - per-symbol repair detail text including gap type and affected timestamp
  - restored-ready state after that repair completes
- Existing automated coverage already verifies those semantics at the backend/runtime level; the missing piece is a deterministic browser-visible transient repair fixture.

## Final assessment
- Automated verification passed for backend contract behavior.
- Aspire browser verification passed for the real `/home` widget ready/restored-ready path and cleanup requirements.
- Full browser coverage of all repair-specific visible distinctions remains only partially verified because a deterministic in-browser repair-state fixture was not available in this environment.
