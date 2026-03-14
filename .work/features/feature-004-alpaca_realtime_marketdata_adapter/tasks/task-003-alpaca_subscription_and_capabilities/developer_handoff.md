# Developer Handoff

## Task
- Feature: `feature-004` — `feature-004-alpaca_realtime_marketdata_adapter`
- Task: `task-003` — `task-003-alpaca_subscription_and_capabilities`

## Objective
Implement Alpaca-side subscription diffing and capability reporting so `MarketData` can express desired realtime state through vendor-neutral shared contracts while the adapter translates that intent into Alpaca-native operations.

## Dependency status
- `task-001-alpaca_sdk_contract_alignment`: satisfied and review-approved.
- `task-002-alpaca_streaming_client_adapter`: satisfied and review-approved.
- Use the shared contract mapping, feed/environment rules, realtime provider boundary, and current streaming runtime from those tasks as binding inputs.

## Required outcomes
- Implement replace-all desired subscription state translation into Alpaca-native additive subscribe/unsubscribe operations.
- Preserve the single shared stream model with internal symbol/channel fan-out rather than per-symbol connections.
- Implement provider capability reporting for feed support, historical/batch support flags, revision/correction support, and relevant runtime limits/feature flags needed by `MarketData`.
- Normalize subscription application failures, rejected symbols, and limit/rate-related conditions into shared semantics.
- Keep vendor SDK types and Alpaca-specific logic confined to `src/adapters/Aegis.Adapters.Alpaca`.

## Implementation constraints
- Follow `docs/CONSTITUTION.md`, `docs/ARCHITECTURE.md`, and `docs/PROJECT.md` as binding.
- Keep scope limited to adapter-side subscription and capability behavior; do not broaden into downstream `MarketData` orchestration or final integration wiring reserved for `task-004`.
- Preserve replace-all desired-state semantics at the shared boundary even if Alpaca requires additive diff operations internally.
- Avoid chatty full resubscribe behavior when only incremental changes are needed.
- Preserve scalability for large symbol sets and high-volume realtime workloads.
- Add brief high-signal comments where diffing, rate-limit handling, or capability semantics are non-obvious.
- Do not update `.work/` markdown artifacts in the implementation worktree.

## Expected implementation surfaces
- `src/adapters/Aegis.Adapters.Alpaca/`
- Realtime adapter subscription-state helpers and capability-reporting types/services
- Shared capability descriptors or related provider-contract surfaces in `src/Aegis.Shared/Ports/MarketData/` only if required by the existing contract shape
- Tests covering diff application, capability reporting, and failure normalization

## Delivery guidance
- Build on the existing realtime provider from `task-002`; do not rework stable streaming behavior unless a concrete blocker is found.
- Make desired-state diffing deterministic and efficient so repeated calls with unchanged state are low-cost.
- Ensure capability reporting is explicit enough that `MarketData` does not need scattered Alpaca conditionals.
- Translate provider-native limits and failures into shared contract semantics without leaking Alpaca-specific models upstream.

## Minimum verification expectation for the implementing agent
- Add unit tests for desired-state diff application, no-op updates, unsubscribe/subscribe sequencing, capability reporting, and normalized failure paths.
- Run relevant build/test validation and report exact commands and outcomes.
- Clearly distinguish automated proof from any deferred live-provider verification requiring Alpaca credentials.

## Sequencing note
`task-004` depends on this task. Leave a stable subscription/capability surface that can be integrated into current `MarketData` registration and validation without reopening adapter-boundary decisions.
