# Developer Handoff

## Task
- Feature: `feature-004` — `feature-004-alpaca_realtime_marketdata_adapter`
- Task: `task-001` — `task-001-alpaca_sdk_contract_alignment`

## Objective
Define the implementation-ready boundary between the official Alpaca .NET SDK and Aegis shared MarketData provider contracts so later adapter tasks can build against stable, vendor-neutral contracts.

## Required outcomes
- Select the official Alpaca NuGet package and version to use.
- Define normalized mappings for finalized bars, `updatedBars`, trades, quotes, provider status, market status, corrections, and cancel/error signals.
- Define adapter-owned auth, feed, and environment configuration expectations.
- Define connection lifecycle, reconnect ownership, and bounded channel-reader expectations.
- Define which Alpaca SDK types stay internal to the adapter and prevent vendor leakage across shared contracts.

## Implementation constraints
- Follow `docs/CONSTITUTION.md`, `docs/ARCHITECTURE.md`, and `docs/PROJECT.md` as binding.
- Keep scope limited to contract mapping and adapter-boundary decisions; do not implement the full realtime adapter in this task.
- Keep Alpaca SDK types confined to `src/adapters/Aegis.Adapters.Alpaca`.
- Preserve provider-agnostic shared contracts for `MarketData`.
- Prefer a single shared stream model and bounded, backpressure-aware delivery semantics.
- Add brief high-signal comments where boundary or mapping rules are non-obvious.
- Do not update `.work/` markdown artifacts in the implementation worktree; workflow docs remain canonical in the main workspace.

## Expected implementation surfaces
- `src/adapters/Aegis.Adapters.Alpaca/Aegis.Adapters.Alpaca.csproj`
- Alpaca adapter configuration types/services as needed for boundary definition
- Shared MarketData provider ports/contracts in `src/Aegis.Shared/Ports/MarketData/` if the current contract shape requires completion or refinement
- Contract documentation only if needed to clarify binding adapter behavior

## Delivery guidance
- Make package/version selection explicit and justified in implementation artifacts or task summary.
- Define event normalization in a way that later tasks can implement without reopening boundary decisions.
- Make reconnect ownership and channel-reader semantics explicit enough for large-symbol realtime workloads.
- Avoid raw vendor payload exposure outside the adapter boundary.

## Minimum verification expectation for the implementing agent
- Add unit tests for mapping/normalization logic introduced by this task.
- Run build validation for package integration and contract changes.
- Report exact commands and outcomes.
- If any runtime validation is deferred to later tasks, state that clearly rather than implying full adapter verification.

## Sequencing note
This is the first task in the feature and must complete before `task-002`, `task-003`, and `task-004` proceed.
