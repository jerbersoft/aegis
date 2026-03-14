# Testing Results

## Task
- Feature ID: feature-002
- Task ID: task-002
- Classification: behavior-changing

## Verification scope
- Chosen scope: unit + integration
- Why: this task changes backend repair/recompute/readiness semantics in `MarketData`; integration tests cover persisted/API-visible outcomes, while unit tests cover recompute start-point and repair-state edge cases more directly than UI automation.

## Commands executed
- `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"`
  - Outcome: passed (`24` tests)
- `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"`
  - Outcome: passed (`9` tests)

## What was verified
- Trailing-gap repair recomputes from the first missing trailing bar and restores `ready` after repair completes.
- Internal-gap repair recomputes from the earliest missing internal bar and restores `ready` after repair completes.
- Material corrected bars trigger recompute from the corrected timestamp; materially unchanged corrected bars are normalized without unnecessary recompute/readiness churn.
- Failed repair fetch leaves readiness degraded as `repairing` with `repair_fetch_failed`.
- Failed repaired-sequence validation leaves readiness degraded as `repairing` with `repair_validation_failed` and does not restore readiness early.
- Bounded repair scheduling/backoff semantics are covered at unit level, including priority ordering and duplicate-attempt suppression during retry backoff.
- Rollup/symbol readiness contracts expose repair metadata (`hasActiveRepair`, `pendingRecompute`, `earliestAffectedBarUtc`, rollup repair counts).

## Inspection-based verification
- Reviewed `IntradayMarketDataHydrationService` in the implementation worktree.
- Confirmed repair execution ordering is implemented as: provider fetch -> persistence/upsert -> recompute -> final validation -> atomic runtime snapshot replacement.
- Confirmed readiness restoration happens only after repaired-sequence validation succeeds.
- Confirmed the implementation writes an intermediate `awaiting_recompute` snapshot before the final ready/restored snapshot, while final public API tests remain focused on deterministic end states.

## Skipped / not added
- No Playwright/browser verification was added because this task is backend-only and the requirement is satisfied more directly by unit/integration coverage plus implementation inspection.
- No deterministic external test was added to hold the transient `awaiting_recompute` state mid-rebuild; the implementation evidence for that transient state is the intermediate runtime-store snapshot plus the readiness contract/view coverage.

## Result
- Status: pass
- Rework needed: no defects found during verification
