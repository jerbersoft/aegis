# Feature Acceptance - feature-001

## Feature
- ID: `feature-001`
- Title: `MarketData` intraday repair and recompute progression
- Covered tasks:
  - `task-001` - intraday repair state model
  - `task-002` - intraday recompute execution
  - `task-003` - intraday repair visibility

Task outcome summary:
- `task-001`: explicit repair lifecycle, repair-cause classification, deduplication, and rollup repair-state semantics are implemented.
- `task-002`: repaired persistence, bounded recompute replay, atomic runtime replacement, and validation-gated readiness restoration are implemented.
- `task-003`: minimal repair/recompute visibility is exposed through readiness APIs and the `/home` MarketData widget.

## What this acceptance covers
- Required `1-min` intraday symbols move into `repairing` for trailing gaps, internal gaps, and materially changed corrected finalized bars.
- Repair work stays deduplicated per `symbol|interval|profile` and widens to the earliest affected timestamp when repeated detections occur.
- Recompute runs after repaired persistence, replays only the affected suffix from the earliest affected timestamp, replaces runtime state atomically, and restores readiness only after validation succeeds.
- REST readiness responses and the `/home` MarketData widget expose minimal repair visibility, including active repair counts, pending recompute counts, and earliest affected timestamp metadata.

## Run the app
1. Start Aspire:
   - `dotnet run --project "src/Aegis.AppHost/Aegis.AppHost.csproj"`
2. Open the Aspire-managed web URL for `Aegis.Web`.
3. Log in with the local demo flow.
4. Navigate to `/home`.
5. After verification, stop Aspire and any related backend/web/browser-test processes.

## Automated verification to run
- `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"`
- `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"`
- In `src/Aegis.Web`:
  - `npm run lint`
  - `npm run build`

## What to test

### 1. Repair lifecycle semantics (`task-001`)
- Verify required intraday symbols with trailing or internal gaps report `repairing` instead of generic `not_ready`.
- Verify corrected finalized bars can trigger the same repair lifecycle.
- Verify rollup intraday readiness reports `repairing` when repair is the only degraded condition.

Expected outcomes:
- Symbol readiness preserves meaningful gap reason codes.
- Repair state keeps the earliest affected timestamp.
- One active repair job exists per `symbol|interval|profile`.

### 2. Recompute execution and readiness restoration (`task-002`)
- Verify repair sequencing is: fetch -> persistence upsert -> recompute from earliest affected timestamp -> atomic snapshot replacement -> validation -> readiness restoration.
- Verify trailing-gap, internal-gap, and materially changed corrected-bar repairs replay only the affected suffix.
- Verify materially unchanged corrected bars behave as no-op normalization.
- Verify fetch or validation failure keeps readiness degraded.

Expected outcomes:
- Readiness returns to `ready` only after recompute and validation succeed.
- Failed repair paths remain degraded with repair-specific reason codes.
- Runtime replacement is atomic from the consumer point of view.

### 3. Visibility through API and Home widget (`task-003`)
- Verify symbol-level readiness exposes:
  - `hasActiveRepair`
  - `pendingRecompute`
  - `earliestAffectedBarUtc`
- Verify rollup readiness exposes:
  - `activeRepairSymbolCount`
  - `pendingRecomputeSymbolCount`
  - `earliestAffectedBarUtc`
- Verify `/home` MarketData widget shows the current intraday readiness state and clears repair metadata once readiness is restored.

Expected outcomes:
- Repairing symbols and rollups expose minimal, operator-readable repair metadata.
- Restored-ready responses clear active repair metadata.
- The real `/home` widget renders the ready/restored-ready path through Aspire-managed backend and web endpoints.

## Evidence already recorded
- Unit test evidence:
  - `task-001`: Passed `17`
  - `task-002`: Passed `19`
  - `task-003`: Passed `20`
- Integration test evidence:
  - `task-001`: Passed `1`
  - `task-002`: Passed `9`
  - `task-003`: Passed `9`
- Web validation evidence:
  - `task-003`: `npm run lint` passed, `npm run build` passed
- Aspire/browser evidence:
  - `task-003`: `/home` MarketData widget verified on the real ready/restored-ready path with cleanup completed

## Caveat
- A deterministic browser-visible fixture for transient `repairing` / `awaiting_recompute` widget states was not available. Those transient states were verified through unit tests, integration tests, and REST evidence rather than a held in-browser scenario. This is documented as a non-blocking follow-up, not an acceptance blocker.

## Verification notes
- Browser coverage status: partial, with real Aspire-managed browser verification completed for the `/home` ready/restored-ready path.
- Transient-state verification support: no deterministic browser fixture yet for held `repairing` / `awaiting_recompute` UI states; automated and REST evidence cover those semantics.
- Browser cleanup confirmation: Aspire, backend, web, and browser-test processes were explicitly terminated and verified stopped after testing.

## Acceptance decision
- Feature acceptance status: ready
- Tasks covered by this guide: `task-001`, `task-002`, `task-003`
