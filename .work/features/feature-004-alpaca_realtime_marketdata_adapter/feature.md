# Feature

## Metadata
Feature ID: feature-004
Feature Folder: feature-004-alpaca_realtime_marketdata_adapter
Title: Alpaca realtime MarketData adapter
Priority: high
Status: closed
Current Active Task: none
Current Owner: orchestrator
Main Workspace Path: /Users/herbertsabanal/Projects/aegis
Main Workspace Branch: master
Main Workspace Branch Verified: yes
Recorded Base Branch: master
Recorded Worktree Branch: feature-004-alpaca_realtime_marketdata_adapter
Recorded Worktree Path: /Users/herbertsabanal/Projects/.aegis-worktrees/feature-004-alpaca_realtime_marketdata_adapter-impl-01
PR Status: merged
PR URL: https://github.com/jerbersoft/aegis/pull/2
Environment Status: stopped_after_owner_acceptance
Last Prepared At: 2026-03-14 acceptance-environment prepared from recorded worktree
Created Date: 2026-03-13
Last Updated: 2026-03-14 owner reported PR merged

## Source
Request Source: continue MarketData by implementing the real Alpaca adapter with the Alpaca .NET SDK
Requested By: user

## Objective
Implement the real Alpaca-backed realtime `MarketData` adapter in `src/adapters/Aegis.Adapters.Alpaca` using the official Alpaca NuGet package so `MarketData` can consume normalized realtime provider events and provider capabilities through stable shared contracts.

## Scope
- Keep the feature focused on the Alpaca adapter as the next `MarketData` foundation slice.
- Implement the official Alpaca .NET SDK integration path inside `src/adapters/Aegis.Adapters.Alpaca`.
- Complete the planned provider-facing `IRealtimeMarketDataProvider` path for normalized realtime events.
- Implement normalized translation for realtime finalized bars, `updatedBars`, trades, quotes, provider/runtime status events, market-status events, corrections, and cancel/error signals as supported by the shared provider contracts.
- Implement provider capability reporting needed by `MarketData` orchestration so feed and feature support remain explicit and provider-agnostic.
- Implement replace-all desired-state subscription translation into Alpaca-native additive subscribe/unsubscribe operations.
- Keep Alpaca SDK and vendor-specific models confined to the adapter; shared contracts must remain vendor-neutral.
- Preserve scalability expectations for a single shared stream plus internal fan-out rather than per-symbol connection models.
- Keep fake/bootstrap provider paths available only where current local development orchestration still intentionally depends on them; do not introduce generic production test-mode branches.
- Keep `SignalR` UI work, non-`MarketData` modules, and non-Alpaca broker integration out of scope for this feature.

## Feature-Level Blockers
- none

## Started Processes
- none

## Task Index
- `task-001-alpaca_sdk_contract_alignment` - Define Alpaca SDK contract mapping and adapter boundaries - closed - depends on: none
- `task-002-alpaca_streaming_client_adapter` - Implement Alpaca streaming client adapter for normalized realtime events - closed - depends on: task-001-alpaca_sdk_contract_alignment
- `task-003-alpaca_subscription_and_capabilities` - Implement Alpaca subscription diffing and capability reporting - closed - depends on: task-001-alpaca_sdk_contract_alignment, task-002-alpaca_streaming_client_adapter
- `task-004-marketdata_adapter_integration_validation` - Integrate and validate the Alpaca realtime adapter with MarketData - closed - depends on: task-001-alpaca_sdk_contract_alignment, task-002-alpaca_streaming_client_adapter, task-003-alpaca_subscription_and_capabilities

Status note:

- Keep task index statuses aligned with each task's `TASK.md` so `planner` does not re-select already approved work.

## Next Action
None. PR `https://github.com/jerbersoft/aegis/pull/2` has been merged and feature workflow is complete.

## Recommended Execution Sequence
1. Complete `task-001-alpaca_sdk_contract_alignment` first so the SDK package/version choice, shared contract mapping, and adapter-boundary rules are fixed before implementation spreads across the adapter.
2. Complete `task-002-alpaca_streaming_client_adapter` second so normalized event delivery exists before subscription-diff and capability wiring depend on it.
3. Complete `task-003-alpaca_subscription_and_capabilities` third so the adapter can apply desired-state subscriptions and report supported behavior to `MarketData`.
4. Complete `task-004-marketdata_adapter_integration_validation` last so the integrated adapter is validated against `MarketData` expectations with the full contract and subscription model in place.

## Planning Notes
- `docs/modules/MARKET_DATA.md` explicitly states that `src/adapters/Aegis.Adapters.Alpaca` is not yet a full historical/realtime market-data adapter.
- `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md` defines the target provider-facing contracts, but historical-bar, realtime-provider, and capability contracts are still primarily planned rather than fully implemented.
- `docs/integration/ALPACA.md` already records that Alpaca supports the relevant realtime event families and that the official C# SDK is a strong fit for the approved stack.
- Current repository evidence shows `Aegis.Adapters.Alpaca` contains only symbol-reference and historical-bar provider implementations plus explicit fake bootstrap providers; there is no realtime provider implementation yet.
- The adapter should assume one shared Alpaca websocket connection per active equities feed and translate `MarketData` replace-all desired subscription state into provider-native additive diffs.
- The feature should preserve the architecture rule that `MarketData` owns business behavior, readiness, and subscription intent while the adapter owns SDK, auth, reconnect, vendor translation, and provider-native protocol behavior.

## Linked Artifacts
- `ACCEPTANCE.md`
- `tasks/`

## Notes
- Evidence used for this planning artifact: `docs/modules/MARKET_DATA.md`, `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md`, `docs/integration/ALPACA.md`, and current `src/adapters/Aegis.Adapters.Alpaca` contents.
- This feature should be the next `MarketData` implementation slice before deeper provider-agnostic subscription-runtime work because the shared runtime needs a real Alpaca-backed realtime adapter to integrate against.

## Planner Readiness Notes
- This feature is prepared for `planner` to begin task selection with `task-001-alpaca_sdk_contract_alignment`.
- No known cross-module decision is required to start the first task because the scope is intentionally limited to the Alpaca adapter and shared provider-contract alignment.
- Planner should keep tasks sequential because later tasks depend on the contract and boundary decisions fixed by earlier work.
- The first handoff should explicitly preserve the rule that vendor SDK types stay inside the adapter and that the official Alpaca NuGet package is the intended implementation path.

Workflow status notes:

- Keep `Current Active Task`, task statuses, and `Next Action` aligned with the actual execution loop state.
- Keep the feature `in_progress` until acceptance work is complete, even if all tasks are already `ready`.
- Keep `Main Workspace Path`, `Main Workspace Branch`, and `Main Workspace Branch Verified` aligned with the orchestration preflight state.
- If `Recorded Worktree Path` is missing or matches `Main Workspace Path`, treat the feature as blocked and do not delegate implementation.
- Keep `PR Status` and `PR URL` aligned with the real close-flow outcome when the feature enters close handling.
- Keep environment metadata aligned with the currently prepared worktree state and only list processes started or tracked by `orchestrator`.
- After `ACCEPTANCE.md` is created, `orchestrator` should ask `runtime` to proactively prepare the owner acceptance environment from the recorded worktree, record the resulting environment/process state here, and present an owner-facing preview of the acceptance guide.
- This owner acceptance environment is separate from any task-level tester environment used earlier in the execution loop.
- When the owner says `accept this feature` or `reject this feature`, `orchestrator` should immediately ask `runtime` to stop the prepared acceptance environment before updating acceptance state or routing follow-up work.
- When the owner says `accept this feature` or equivalent, `orchestrator` should treat that as the close-flow command and, per `docs/CONSTITUTION.md`, as the owner command that authorizes publication: stop the prepared acceptance environment if needed via `runtime`, finalize feature closure bookkeeping in the canonical base-branch `.work/` docs, and then commit/push/create the PR from the recorded worktree branch to the recorded base branch unless blocked. `.work/` Markdown artifacts remain base-branch-only and must not be copied into the worktree branch; PR merge or rejection remains with the owner.
