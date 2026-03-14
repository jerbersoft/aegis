# Developer Handoff

## Task
- Feature: `feature-004` — `feature-004-alpaca_realtime_marketdata_adapter`
- Task: `task-002` — `task-002-alpaca_streaming_client_adapter`

## Objective
Implement the Alpaca SDK-backed realtime streaming adapter that emits normalized provider events through the shared `MarketData` realtime-provider boundary defined by `task-001`.

## Dependency status
- `task-001-alpaca_sdk_contract_alignment`: satisfied and reviewed approved.
- Use the shared contracts, package choice, environment/feed normalization, and adapter-boundary rules established there as binding inputs.

## Required outcomes
- Implement connect/disconnect behavior for the Alpaca realtime stream.
- Emit normalized events through the bounded `ChannelReader<RealtimeMarketDataEvent>` boundary.
- Support finalized bars, `updatedBars`, trades, quotes, provider status, market status, corrections, and cancel/error signals using the approved mapping.
- Normalize runtime failures without leaking Alpaca SDK types outside the adapter.
- Preserve adapter-owned reconnect behavior and bounded, backpressure-aware delivery semantics.

## Implementation constraints
- Follow `docs/CONSTITUTION.md`, `docs/ARCHITECTURE.md`, and `docs/PROJECT.md` as binding.
- Keep scope limited to the realtime streaming adapter path; do not implement subscription diffing/capability work reserved for `task-003`.
- Keep Alpaca SDK and vendor-specific models confined to `src/adapters/Aegis.Adapters.Alpaca`.
- Preserve the single shared stream model rather than per-symbol connection patterns.
- Add brief high-signal comments where reconnect, buffering, or event translation behavior is non-obvious.
- Do not update `.work/` markdown artifacts in the implementation worktree.

## Expected implementation surfaces
- `src/adapters/Aegis.Adapters.Alpaca/`
- Realtime adapter services and supporting configuration/helpers under the adapter project
- Existing shared realtime provider contracts from `src/Aegis.Shared/Ports/MarketData/`
- Tests covering adapter normalization and streaming behavior

## Delivery guidance
- Build directly on the contract resolver/mapper decisions from `task-001`; do not reopen package or shared-boundary design unless a concrete blocker is found.
- Ensure callback/event handling feeds a bounded channel safely for high-volume symbol workloads.
- Keep connect/start/stop/reconnect ownership explicit inside the adapter.
- Translate vendor/runtime failures into shared semantics that downstream `MarketData` can consume without Alpaca knowledge.

## Minimum verification expectation for the implementing agent
- Add unit tests for event normalization, bounded channel behavior, connect/disconnect behavior, and failure normalization.
- Run relevant build/test validation and report exact commands and outcomes.
- Clearly distinguish any deferred live-credential/runtime verification from automated proof.

## Sequencing note
`task-003` depends on this task, so this implementation should leave a stable runtime event path for later subscription-diff and capability work.
