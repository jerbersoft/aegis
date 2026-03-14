# Developer Handoff

## Task
- Feature: `feature-004` — `feature-004-alpaca_realtime_marketdata_adapter`
- Task: `task-004` — `task-004-marketdata_adapter_integration_validation`

## Objective
Integrate the completed Alpaca realtime adapter into the current `MarketData` runtime entry points and verify it is usable as the real provider foundation beyond the existing bootstrap-only path.

## Dependency status
- `task-001-alpaca_sdk_contract_alignment`: satisfied and review-approved.
- `task-002-alpaca_streaming_client_adapter`: satisfied and review-approved.
- `task-003-alpaca_subscription_and_capabilities`: satisfied and review-approved.
- Use the established shared contracts, adapter-boundary rules, streaming runtime, and subscription/capability surfaces as binding inputs.

## Required outcomes
- Wire the Alpaca realtime adapter into the current registration and configuration paths used by `MarketData`.
- Verify `MarketData` consumes the realtime provider only through shared vendor-neutral contracts.
- Preserve fake/bootstrap provider paths only where current local development or orchestration intentionally still depends on them.
- Add integration-focused validation for provider registration, shared-contract consumption, and failure behavior when the adapter cannot authenticate or connect.
- Keep the work limited to adapter integration and verification, not broader downstream `MarketData` orchestration redesign.

## Implementation constraints
- Follow `docs/CONSTITUTION.md`, `docs/ARCHITECTURE.md`, and `docs/PROJECT.md` as binding.
- Keep Alpaca SDK types and vendor-specific behavior confined to `src/adapters/Aegis.Adapters.Alpaca`.
- Preserve the constitution rule that fake/bootstrap paths remain explicit bootstrap choices rather than hidden production fallbacks.
- Do not broaden scope into unrelated `MarketData` runtime features, UI work, or non-Alpaca integrations.
- Add brief high-signal comments where provider selection, configuration, or fallback behavior is non-obvious.
- Do not update `.work/` markdown artifacts in the implementation worktree.

## Expected implementation surfaces
- `src/adapters/Aegis.Adapters.Alpaca/`
- `src/Aegis.Backend/` or current composition-root wiring for adapter registration
- Current `MarketData` provider registration/configuration entry points
- Tests covering registration, integration behavior, and normalized failure paths

## Delivery guidance
- Reuse the stable adapter surfaces from tasks `001`-`003`; do not reopen contract or subscription design unless a concrete blocker is found.
- Make real-vs-bootstrap provider selection explicit and easy to reason about.
- Prefer automated proof for integration behavior, and treat credential-gated live-provider checks as additive evidence rather than the only proof.
- Ensure failure-path behavior is observable through shared semantics when auth, configuration, or connection startup fails.

## Minimum verification expectation for the implementing agent
- Add unit and/or integration tests for DI registration, provider selection, shared-contract consumption, and failure-path normalization.
- Run relevant build/test validation and report exact commands and outcomes.
- If Aspire-managed or live-provider checks are attempted, clearly separate automated proof from any credential-gated runtime verification.
- If credentials are unavailable, document exactly what remains unverified and why.

## Sequencing note
This is the last required implementation task in the feature sequence. Leave the feature in a state where acceptance preparation can focus on integrated adapter behavior rather than reopening earlier adapter-boundary decisions.
