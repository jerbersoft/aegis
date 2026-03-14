# Acceptance

## Covered tasks
- `task-001` — explicit intraday repair state model, deduplication, widening, priority, retry/backoff, and bounded concurrency.
- `task-002` — repair execution order, recompute progression, atomic runtime replacement, and readiness restoration.
- `task-003` — REST/Home visibility for `repairing`, `awaiting_recompute`, failed repair, and restored `ready` states.

## Validate from the recorded implementation worktree
Run the checks from:

- `/Users/herbertsabanal/Projects/.aegis-worktrees/feature-002-market_data_intraday_repair_progression-impl-01`

### 1) Backend verification
From the worktree root, run:

```bash
dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"
dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"
```

Expected outcome:
- All tests pass.
- Coverage proves required intraday symbols move through `repairing` and `awaiting_recompute` correctly.
- Failed repair fetch/validation stays degraded instead of restoring `ready` early.
- Trailing gaps, internal gaps, and material corrected bars recompute from the correct earliest timestamp.

### 2) Web verification
From `src/Aegis.Web`, run:

```bash
npm install
npm run lint
npm run build
node --experimental-strip-types --test "src/Aegis.Web/components/dashboard/market-data-widget.helpers.spec.ts"
```

Expected outcome:
- Install, lint, build, and helper tests all pass.
- The Home MarketData widget supports repair detail text for active repair and recompute-pending states.

### 3) Aspire + browser verification
From the worktree root, start AppHost:

```bash
dotnet run --project "src/Aegis.AppHost/Aegis.AppHost.csproj"
```

Then use the Aspire-exposed web URL (recorded verification exposed `http://127.0.0.1:3001`) and verify:
- `/login` loads and login succeeds.
- `/home` renders the MarketData widget.
- The widget shows `MARKETDATA BOOTSTRAP`, `INTRADAY READINESS`, and `Refresh`.
- The ready-state intraday view renders for the seeded required symbol path used in verification (`AMD`).

Optional API spot checks against the Aspire-managed app:
- `GET /api/market-data/intraday/readiness`
- `GET /api/market-data/intraday/readiness/AMD`

Expected outcome:
- Rollup payload exposes `activeRepairSymbolCount`, `pendingRecomputeSymbolCount`, and rollup `earliestAffectedBarUtc`.
- Symbol payload exposes `hasActiveRepair`, `pendingRecompute`, `activeGapType`, `activeGapStartUtc`, and `earliestAffectedBarUtc`.
- Current pull/refresh-based UI wiring is intact; no `SignalR` behavior is expected in this feature.

## Acceptance caveat
- Real browser verification covers the production wiring and restored-ready path.
- Short-lived `repairing` and `awaiting_recompute` UI states are accepted through automated backend/API evidence rather than a deterministic browser hold.
- Stop AppHost and related processes after verification.
