# Work Tracking Workflow

This repository uses `.work/` as the source of truth for internal feature tracking, task execution, agent handoffs, and final acceptance guidance.

## Goals

- Keep one durable workflow record per feature.
- Make tasks the leaf-level execution unit.
- Make handoffs explicit between `orchestrator`, `planner`, `developer`, `tester`, `reviewer`, `Architect`, and `acceptance`.
- Preserve a clear audit trail of what was planned, implemented, tested, reviewed, and accepted.

## Hierarchy

- A `feature` is the container for a coherent body of work.
- A `task` is the smallest executable unit of work inside a feature.
- Tasks are leaf-level units. Tasks do not contain sub-tasks.
- Workflow execution runs at the task level, not directly at the feature level.

## Workspace Model

### Main workspace

- The main workspace is the repository root, for example `/Users/herbertsabanal/Projects/aegis`.
- The main workspace is the authoritative home for `.work/`.
- `orchestrator`, `Architect`, `planner`, and `acceptance` operate from the main workspace.
- The `.work` copy in the main workspace is canonical and must be treated as the single source of truth.

### Implementation worktrees

- Implementation worktrees are separate sibling checkouts outside the repository folder.
- Recommended worktree root: `/Users/herbertsabanal/Projects/aegis-worktrees/`.
- Each implementation worktree is a full checkout of the repository, not just `src/` and `tests/`.
- `developer`, `tester`, and `reviewer` operate in an assigned implementation worktree.
- Worktree-local copies of `.work/` are not authoritative and must not be treated as the source of truth for workflow state.

### Worktree branching model

- The repository owner or user chooses the base branch before execution begins.
- `orchestrator` uses the currently checked-out branch as the base for implementation worktrees.
- `orchestrator` does not choose between `master` and `develop`; that is an owner/user decision.
- Each implementation worktree must use its own branch created from the current branch.
- The worktree branch name must match the worktree folder name.

### Worktree naming and location

- Worktree folders should live under the sibling worktree root, for example `/Users/herbertsabanal/Projects/aegis-worktrees/`.
- Worktree folders must use this format:

```text
<feature-folder>-impl-<number>
```

Examples:

- `feature-001-market_data_implementation-impl-01`
- `feature-001-market_data_implementation-impl-02`

- The worktree branch name must exactly match the worktree folder name.
- `number` must be a zero-padded lane identifier such as `01`, `02`, or `03`.
- `orchestrator` must assign a new lane number for each parallel implementation worktree under the same feature.

### Canonical-document rule

- Task and feature workflow documents are authored and maintained in the main workspace under `.work/`.
- `developer`, `tester`, and `reviewer` should read canonical task/feature docs from the main workspace, even when executing inside a worktree.
- Code, tests, runtime verification, and review inspection happen in the assigned worktree.
- Acceptance and final summaries are written in the main workspace.

## Directory structure

```text
.work/
  WORKFLOW.md
  templates/
    feature.md
    TASK.md
    developer_handoff.md
    implementation_summary.md
    testing_results.md
    review_results.md
    FEATURE_SUMMARY.md
    ACCEPTANCE.md
  features/
    feature-001-market_data_implementation/
      feature.md
      ACCEPTANCE.md
      tasks/
        task-001-bootstrap_daily_ingestion/
          TASK.md
          developer_handoff.md
          implementation_summary.md
          testing_results.md
          review_results.md
          artifacts/
        task-002-market_data_api/
          TASK.md
          developer_handoff.md
          implementation_summary.md
          testing_results.md
          review_results.md
          artifacts/
```

## Naming conventions

### Feature folders

Feature folders must use this format:

```text
feature-<number>-<description>
```

Rules:

- `number` is zero-padded, such as `001`, `002`, or `017`.
- `description` is short snake_case.
- Use lowercase letters, numbers, and underscores only.

Examples:

- `feature-001-market_data_implementation`
- `feature-002-watchlist_management`

### Task folders

Task folders live under a feature's `tasks/` directory and must use this format:

```text
task-<number>-<description>
```

Rules:

- `number` is zero-padded within the feature, such as `001`, `002`, or `003`.
- `description` is short snake_case.
- Task numbering resets per feature.
- Tasks are leaf-level units and should be small enough for one execution loop.

Examples:

- `task-001-bootstrap_daily_ingestion`
- `task-002-market_data_api`

## Canonical files

### Feature-level files

- `feature.md`
  - canonical feature dashboard and metadata file
  - indexes tasks, task statuses, dependencies, blockers, and current focus
- `FEATURE_SUMMARY.md`
  - optional concise feature-delivery summary
  - suitable for final user updates, PR drafting, or release-note style reporting
- `ACCEPTANCE.md`
  - feature-level acceptance guide for the user
  - explains how to run the app, what to test, and expected outcomes
- `tasks/`
  - contains all executable task folders for the feature

### Task-level files

Each task folder should contain:

- `TASK.md`
  - canonical task metadata and workflow state file
  - owned primarily by `Architect` for initial setup and by `Orchestrator` for workflow-state transitions
- `developer_handoff.md`
  - written by `planner`
- `implementation_summary.md`
  - written by `developer`
- `testing_results.md`
  - written by `tester`
- `review_results.md`
  - written by `reviewer`
- `artifacts/`
  - optional supporting evidence such as screenshots or logs

## Workflow ownership

### Orchestrator

- Selects the feature, or uses the feature specified by the user.
- Owns the workflow loop, not implementation.
- Is the only agent allowed to decide which agent or subagent is called next.
- Requires feature and task tracking to be complete before task execution begins.
- For new planning work, routing to `Architect` happens before the execution loop starts, not inside it.
- When beginning work on a feature, creates or reuses an implementation worktree from the current branch.
- Uses a sibling worktree folder outside the repository, typically under `/Users/herbertsabanal/Projects/aegis-worktrees/`.
- Asks `planner` for the next task that should be worked on.
- Routes the selected task through `developer` -> `tester` -> `reviewer`.
- After each completed task cycle, asks `planner` whether another task is ready.
- Repeats until there are no more required tasks ready to implement, or the feature is blocked.
- When no more required tasks remain, asks `acceptance` to create or update `ACCEPTANCE.md`.

### Planner

- Owns task selection and sequencing within a feature.
- Decides which task is ready based on task status, dependencies, and blockers.
- Creates or updates task-level `developer_handoff.md`.
- Updates planning metadata as needed, but does not own execution-state transitions.
- Does not create brand-new features or brand-new tasks.
- Does not implement code, testing, or review.
- Does not call other agents or subagents.

### Developer

- Works on one task only.
- Implements code and unit tests.
- Writes task-level `implementation_summary.md`.
- Reads canonical workflow documents from the main workspace and executes code changes in the assigned worktree.
- Does not write integration tests or UI-automated tests.
- Does not call other agents or subagents.

### Tester

- Works on one task only.
- Writes and runs integration tests when needed.
- Writes and runs Playwright tests when UI automation is needed and practical.
- May use manual UI verification only when automation is not yet practical.
- Writes task-level `testing_results.md`.
- Reads canonical workflow documents from the main workspace and executes testing in the assigned worktree.
- Before declaring browser verification blocked, performs and documents a browser-verification capability check.
- Distinguishes browser-confirmed evidence from semantics proven only by unit, integration, or API evidence.
- For transient UI states that are difficult to hold deterministically, may combine automated proof of semantics with a real browser smoke path, as long as any remaining gap is documented precisely.
- Does not call other agents or subagents.

### Reviewer

- Works on one task only.
- Reviews implementation and testing activity.
- Confirms constitution alignment, coding standards, and testing sufficiency.
- Writes task-level `review_results.md`.
- Reads canonical workflow documents from the main workspace and reviews code/tests in the assigned worktree.
- Does not call other agents or subagents.

### Architect

- Owns Markdown workflow and planning documents under `.work/`.
- During planning phase, creates new feature folders, task folders, `feature.md`, and `TASK.md` records for newly defined tracked work.
- During planning and tracked-work creation, reads `.work/WORKFLOW.md`, relevant existing feature docs, and applicable templates under `.work/templates/` as documentation references.
- Does not take over implementation, testing, or review execution.
- Does not own task execution artifacts such as `developer_handoff.md`, `implementation_summary.md`, `testing_results.md`, or `review_results.md`.
- Outside the bounded feature execution loop, `Architect` may delegate planning, research, or documentation work when that improves quality or speed.

### Acceptance

- Owns feature-level `ACCEPTANCE.md`.
- Produces final user-facing acceptance guidance after the task loop is complete.
- May also produce `FEATURE_SUMMARY.md` when `orchestrator` wants a concise final delivery summary or PR-style overview.
- Writes acceptance guidance in the main workspace while describing how the owner should validate the feature in the assigned worktree.
- Does not take over implementation, testing, or review execution.
- Does not call other agents or subagents.

## Workflow loop

### 1. Feature intake

- `orchestrator` selects the feature folder, or routes to `Architect` to create it during planning before execution begins.
- `Architect` creates `feature.md` and task folders for new tracked work.
- The feature objective, scope, and task index are established.
- If tasks are already known, they should be listed in `feature.md` before execution begins.
- Task execution must not begin until the required feature and task tracking artifacts exist.
- Before task execution begins, the owner or user selects the base branch in the main workspace.
- `orchestrator` then creates or reuses an implementation worktree from the current branch.
- The worktree branch and worktree folder must use the same implementation-lane name, for example `feature-001-market_data_implementation-impl-01`.

### 2. Task selection

- `orchestrator` asks `planner` for the next task to work on.
- `planner` inspects `feature.md`, task statuses, task dependencies, and blockers.
- `planner` must treat tasks already marked `ready` or `closed` as non-selectable unless `orchestrator` has explicitly reopened them.
- `planner` either:
  - selects the next ready task and prepares its `developer_handoff.md`, or
  - reports that no more tasks are ready, or
  - reports a blocker that prevents progress.
- If the selected task is missing a task folder or `TASK.md`, that is a workflow blocker. Do not repair tracking artifacts inside the execution loop.

### 3. Development

- `developer` works from the selected task folder.
- `developer` reads canonical `TASK.md` and `developer_handoff.md` from the main workspace.
- `developer` edits code and runs developer validation in the assigned worktree.
- `developer` implements the task and writes `implementation_summary.md`.

### 4. Testing

- `tester` reads the selected task's canonical `implementation_summary.md` from the main workspace.
- `tester` runs integration tests, browser verification, and manual validation in the assigned worktree.
- `tester` performs required integration testing and UI verification.
- `tester` writes `testing_results.md`.
- If browser verification appears blocked, `tester` must first document which capability check step failed: AppHost startup, endpoint reachability, browser launch, login path, page access, or other concrete limitation.
- If full browser observation of all states is impractical, `tester` should document the verification split between browser evidence and automated/API evidence rather than collapsing all remaining work into a generic blocker.
- When the missing browser evidence involves a transient UI state, `tester` should explicitly state whether a deterministic fixture, seeded scenario, test-host override, or operator-triggerable path exists. If not, that absence must be recorded as the reason the browser evidence is partial rather than silently assumed.

### 5. Review

- `reviewer` reads the selected task's canonical artifacts from the main workspace.
- `reviewer` inspects code and tests in the assigned worktree.
- `reviewer` assesses code quality, constitution alignment, and testing sufficiency.
- `reviewer` writes `review_results.md`.
- `reviewer` should distinguish between a true approval blocker and a non-blocking evidence-depth improvement when transient browser-only states lack a deterministic fixture but semantics are otherwise proven by stronger lower-level evidence.

### 6. Loop decision

- `orchestrator` inspects the task result.
- If rework is needed, the task stays active and the loop routes back appropriately.
- If the task is ready, `planner` is asked for the next task.
- The loop repeats until there are no more required tasks ready to implement.
- `Orchestrator` owns feature-level status transitions and normally owns task status transitions as well.
- A task remains `ready` until it is factored into the feature-level `ACCEPTANCE.md`.
- A task moves from `ready` to `closed` only after `Orchestrator` confirms it is represented in `ACCEPTANCE.md`.
- Missing tracking artifacts during execution are blockers, not repair paths.

### 7. Acceptance document

- When `planner` reports that no more required tasks remain, `orchestrator` asks `acceptance` to create or update feature-level `ACCEPTANCE.md`.
- `acceptance` does not need a separate user confirmation when this work is explicitly delegated by `Orchestrator` as part of the internal workflow.
- `ACCEPTANCE.md` should explicitly cover the tasks that are being accepted so `Orchestrator` can close them.
- The owner validates the accepted feature against the implementation worktree state; merge or PR is not required before local acceptance testing.
- `ACCEPTANCE.md` should tell the user:
  - how to run the app
  - what prerequisites are required
  - what to test manually
  - what outcomes to expect
  - any known limitations or caveats
- `ACCEPTANCE.md` should also summarize feature-level outcomes task by task so users can see what was delivered, what was directly browser-verified, and what remains as a documented non-blocking follow-up.
- When helpful for final reporting, `orchestrator` may also ask `acceptance` to create `FEATURE_SUMMARY.md` with a concise implementation-oriented summary suitable for user updates or PR drafting.

## Machine-readable agent result contracts

Task execution, planning-setup, and acceptance agents should return a single compact JSON object for orchestration decisions.

Shared envelope:

```json
{
  "feature_id": "string",
  "feature_folder": "string",
  "task_id": "string | null",
  "task_folder": "string | null",
  "agent": "planner | developer | tester | reviewer | architect | acceptance",
  "agent_status": "complete | partial | blocked | failed",
  "artifact": "string",
  "result": "string",
  "reason_code": "string | null"
}
```

Rules:

- Use `task_id` and `task_folder` for task-scoped work; use `null` only when the outcome is feature-level, such as `no_more_tasks`, `feature_tracking_ready`, or `acceptance_ready`.
- `artifact` is the primary file created or updated by the agent, or `none` when no artifact changed.
- Detailed evidence belongs in the artifact document, not in the JSON.
- Only `Orchestrator` decides actual routing; subagents do not return routing instructions.

Agent-specific outcomes:

- `planner`
  - `complete + task_ready`
  - `complete + no_more_tasks`
  - `partial + needs_clarification`
  - `blocked + blocked`
- `developer`
  - `complete + implementation_ready`
  - `partial + implementation_partial`
  - `blocked + blocked`
- `tester`
  - `complete + pass`
  - `complete + fail`
  - `blocked + blocked`
- `reviewer`
  - `complete + approved`
  - `complete + changes_requested`
  - `blocked + blocked`
- `architect`
  - `complete + feature_tracking_ready`
  - `blocked + blocked`
- `acceptance`
  - `complete + acceptance_ready`
  - `blocked + blocked`

Suggested `reason_code` values:

- Shared: `missing_decision`, `missing_dependency`, `environment_blocked`, `artifact_missing`
- `planner`: `task_tracking_missing`, `dependency_blocked`, `task_not_ready`
- `developer`: `handoff_gap`, `implementation_incomplete`, `unit_validation_blocked`
- `tester`: `defect_found`, `verification_gap`, `test_env_blocked`, `apphost_start_failed`, `endpoint_unreachable`, `browser_launch_failed`, `login_flow_blocked`, `page_access_blocked`
- `reviewer`: `code_gap`, `test_gap`, `missing_evidence`, `standards_gap`
- `architect`: `planning_incomplete`
- `acceptance`: `acceptance_incomplete`

Expected routing:

- `planner` + `task_ready` -> `Orchestrator` decides whether to call `developer`
- `planner` + `no_more_tasks` -> `Orchestrator` decides whether to call `acceptance`
- `planner` + `needs_clarification` -> `Orchestrator` decides whether clarification is needed from the user
- `planner` + `blocked` -> `Orchestrator` decides the next action
- `developer` + `implementation_ready` -> `Orchestrator` decides whether to call `tester`
- `developer` + `implementation_partial` -> `Orchestrator` decides the next action
- `developer` + `blocked` -> `Orchestrator` decides the next action
- `tester` + `pass` -> `Orchestrator` decides whether to call `reviewer`
- `tester` + `fail` -> `Orchestrator` decides whether to route back to `developer` or handle another action
- `tester` + `blocked` -> `Orchestrator` decides the next action
- `reviewer` + `approved` -> `Orchestrator` decides the next action
- `reviewer` + `changes_requested` -> `Orchestrator` decides whether to route to `developer` or `tester`
- `reviewer` + `blocked` -> `Orchestrator` decides the next action
- `architect` + `feature_tracking_ready` -> `Orchestrator` decides the next action
- `architect` + `blocked` -> `Orchestrator` decides the next action
- `acceptance` + `acceptance_ready` -> `Orchestrator` closes covered tasks and the feature when appropriate
- `acceptance` + `blocked` -> `Orchestrator` decides the next action

## Status model

### Feature status

Feature status is tracked in `feature.md` and represents overall feature state.

Allowed feature statuses:

- `draft`
- `in_progress`
- `blocked`
- `closed`

Recommended meaning:

- `draft`: feature exists but task execution has not started
- `in_progress`: one or more tasks are active or not yet complete
- `blocked`: progress cannot continue because required work is blocked
- `closed`: acceptance is complete and no further workflow is expected

### Task status

Task status is tracked in `TASK.md` and represents execution state for the leaf-level unit.

Allowed task statuses:

- `draft`
- `in_progress`
- `ready`
- `blocked`
- `closed`

Recommended meaning:

- `draft`: task exists but has not been prepared for execution
- `in_progress`: the task is active anywhere inside the execution loop, including development, testing, review, or directed rework; use `Current Owner` plus artifacts to show the exact active phase
- `ready`: the task has passed the execution loop and is waiting to be represented in `ACCEPTANCE.md`
- `blocked`: the task cannot proceed
- `closed`: the task is represented in `ACCEPTANCE.md` and no further workflow work is expected

Ownership guidance:

- `orchestrator` normally performs task status transitions based on agent outcomes.
- `planner` should not re-select tasks already marked `ready` or `closed` unless `orchestrator` explicitly reopens them.
- `blocked` should be used only when the next required step cannot proceed because of a concrete external dependency, missing evidence, or environment limitation; it should not be used as a substitute for incomplete reasoning.
- `ready` means the task has completed development, testing, and review and is waiting only for feature-level acceptance coverage or closure.
- `closed` means the task is represented in `ACCEPTANCE.md` and should not be re-entered into the execution loop unless explicitly reopened.

### Task acceptance status

Task acceptance status is tracked in `TASK.md` and represents whether the task has been incorporated into feature-level acceptance guidance.

Allowed acceptance-status values:

- `not_covered`
- `covered_in_acceptance`
- `not_applicable`

Recommended meaning:

- `not_covered`: the task is not yet represented in `ACCEPTANCE.md`
- `covered_in_acceptance`: the task is represented in `ACCEPTANCE.md`
- `not_applicable`: the task does not need acceptance-guide coverage

## Feature rollup expectations

- `feature.md` should index all tasks and their current statuses.
- `feature.md` should identify the current active task when one exists.
- Feature status should usually be derived from its tasks.
- A feature can move toward acceptance when all required tasks are `ready` or `closed` and no mandatory task is blocked.
- A feature should remain `in_progress` until acceptance work is finished, even if all tasks are already `ready`.
- The task index shown in `feature.md` is repeatable; add as many task rows as the feature requires.

## Minimum template expectations

### `feature.md`

Should capture at least:

- feature id
- feature title
- overall status
- objective
- feature-level blockers
- current active task
- task index with statuses and dependencies
- next action
- linked artifacts including `ACCEPTANCE.md`
- optionally `FEATURE_SUMMARY.md` when feature-level summary reporting is needed

### `TASK.md`

Should capture at least:

- feature id
- task id
- task title
- task status
- task objective
- scope
- dependencies
- blockers
- current owner
- acceptance status
- acceptance document reference
- next action
- linked task artifacts

### `developer_handoff.md`

Should capture at least:

- task objective
- task scope
- requirements
- acceptance criteria
- implementation surfaces
- unit-test expectations
- follow-on testing notes for `tester`

### `implementation_summary.md`

Should capture at least:

- files changed
- behavior changed
- unit tests added or updated
- validation performed
- limitations or risks
- remaining testing expectations for `tester`
- whether any deterministic fixture, seeded scenario, or test hook exists for hard-to-reproduce transient states
- what evidence the developer expects `tester` to gather in the real browser versus lower-level automation

### `testing_results.md`

Should capture at least:

- selected test scope
- integration tests created or updated
- Playwright tests created or updated when applicable
- manual UI verification when used as fallback
- commands executed
- pass or fail outcomes
- blockers or failures
- browser-verification capability check results when browser verification is required or reported blocked
- a clear split between browser-confirmed evidence and automated/API-only evidence when coverage is mixed
- whether a deterministic transient-state fixture exists, and if not, why the browser evidence is necessarily partial
- process cleanup evidence for Aspire/backend/web/browser verification flows

### `review_results.md`

Should capture at least:

- code review findings
- testing activity review findings
- missing evidence
- required fixes
- readiness recommendation
- whether any missing browser-only evidence is truly blocking or only a follow-up improvement

### `ACCEPTANCE.md`

Should capture at least:

- feature summary
- tasks covered by this acceptance guide
- feature-level delivered outcomes by task
- prerequisites
- how to run the app
- what to test
- expected outcomes
- known limitations or caveats
- when applicable, whether browser coverage is complete or partly supported by automated/API evidence for transient states

### `FEATURE_SUMMARY.md`

Should capture at least:

- feature summary
- tasks delivered and their user-visible outcomes
- key verification performed
- notable blockers resolved during implementation
- any remaining non-blocking follow-up notes

## Initial policy

- Use `.work/WORKFLOW.md` as the canonical workflow document.
- Use feature folders as containers and task folders as leaf-level execution units.
- Keep one `ACCEPTANCE.md` per feature at the feature root.
- Keep `.work/` authoritative in the main workspace, not in worktree copies.
- Use sibling worktrees for implementation, testing, and review execution.
- Run the execution loop per task.
- Let `planner` choose the next task to work on.
- Let `acceptance` own final acceptance guidance once task execution is complete.
