# Work Tracking Workflow

This repository uses `.work/` as the source of truth for internal feature tracking, work-item coordination, and agent handoffs.

## Goals

- Keep one durable workflow record per feature.
- Make handoffs explicit between `planner`, `developer`, `tester`, `reviewer`, and `orchestrator`.
- Preserve a simple audit trail of what was requested, implemented, tested, reviewed, and what still needs action.

## Directory structure

```text
.work/
  WORKFLOW.md
  features/
    feature-001-market_data_implementation/
      feature.md
      developer_handoff.md
      implementation_summary.md
      testing_results.md
      review_results.md
      artifacts/
  templates/
    feature.md
    developer_handoff.md
    implementation_summary.md
    testing_results.md
    review_results.md
```

## Feature naming convention

Each feature lives in its own folder under `.work/features/`.

Feature folder names must use this format:

```text
feature-<number>-<description>
```

Rules:

- `number` is a zero-padded feature identifier such as `001`, `002`, or `017`.
- `description` is a short snake_case label.
- Use lowercase letters, numbers, and underscores only.
- Do not use spaces.

Examples:

- `feature-001-market_data_implementation`
- `feature-002-watchlist_management`
- `feature-003-order_entry`

## Feature model

- One feature folder represents one tracked feature.
- A feature may contain multiple smaller work-items if they all belong to the same feature.
- The feature folder is the canonical place for planning, implementation, testing, review, and workflow state.

## Canonical files per feature

Every feature folder should use these files:

- `feature.md`
  - canonical metadata and status file for the feature
  - records current workflow stage, owner, blockers, and next action
- `developer_handoff.md`
  - written by `planner`
  - defines what `developer` should implement
- `implementation_summary.md`
  - written by `developer`
  - records what was implemented, how it was validated, and what needs higher-level testing
- `testing_results.md`
  - written by `tester`
  - records integration testing, Playwright testing, manual UI verification when needed, and any failures or blockers
- `review_results.md`
  - written by `reviewer`
  - records code review findings, testing activity review findings, and readiness assessment
- `artifacts/`
  - optional supporting material such as screenshots, logs, exported reports, or temporary evidence worth preserving

## Workflow lifecycle

Recommended stage flow:

1. Intake
2. Planning
3. Development
4. Testing
5. Review
6. Ready or Rework

Detailed lifecycle:

### 1. Intake

- `orchestrator` creates or selects the feature folder.
- `feature.md` is created and initialized.
- Original request, goal, and initial constraints are captured.

### 2. Planning

- `planner` reads the request and relevant repository context.
- `planner` creates or updates `developer_handoff.md`.
- The handoff must define scope, assumptions, out-of-scope items, unit-test expectations, and follow-on testing notes for `tester`.

### 3. Development

- `developer` works from `developer_handoff.md`.
- `developer` writes production code and unit tests only.
- `developer` does not write integration tests or UI-automated tests.
- `developer` creates or updates `implementation_summary.md`.

### 4. Testing

- `tester` consumes `implementation_summary.md`.
- `tester` writes and runs integration tests when required.
- `tester` writes and runs Playwright tests when UI automation is needed and practical.
- `tester` may use manual UI verification only when automation is not yet practical.
- `tester` records outcomes in `testing_results.md`.

### 5. Review

- `reviewer` reviews `developer` output against `docs/CONSTITUTION.md`, repository conventions, scope discipline, and unit-test expectations.
- `reviewer` reviews `tester` output to confirm that the implemented code received the necessary higher-level testing.
- For behavior-changing features, `reviewer` is required.
- `reviewer` records findings in `review_results.md`.

### 6. Ready or Rework

- `orchestrator` reads all feature artifacts and determines the next workflow action.
- If review identifies gaps, work returns to `developer` or `tester` as appropriate.
- If review is satisfactory and required evidence exists, the feature can move to `ready`.

## Status model

`feature.md` is the canonical source of status.

Allowed statuses:

- `draft`
- `planned`
- `in_development`
- `in_testing`
- `in_review`
- `rework_required`
- `ready`
- `blocked`
- `closed`

Recommended meaning:

- `draft`: feature exists but planning has not started
- `planned`: developer handoff is ready
- `in_development`: `developer` is implementing
- `in_testing`: `tester` is validating
- `in_review`: `reviewer` is assessing implementation and testing activity
- `rework_required`: review or testing found gaps that must be fixed
- `ready`: workflow evidence is present and the feature is ready for the next product or delivery decision
- `blocked`: progress cannot continue due to an unresolved dependency, ambiguity, or missing prerequisite
- `closed`: feature is finished and no further workflow activity is expected

## Agent responsibilities

### Orchestrator

- Owns workflow sequencing and status transitions.
- Chooses the active feature folder.
- Routes work between `planner`, `developer`, `tester`, and `reviewer`.
- Decides whether a feature advances, loops back for rework, or becomes blocked.
- May route Markdown workflow or planning document updates to `Architect` when those updates are outside the normal feature execution handoff chain.

### Architect

- Owns repository planning and documentation work that should live in Markdown.
- Is the primary owner for `.work/*.md` planning docs and workflow docs, including feature-level planning documents when requested.
- May update Markdown guidance, process docs, and planning artifacts without taking over implementation, testing, or review execution.

### Planner

- Works only with tasks and work-items.
- Produces `developer_handoff.md`.
- Does not write code, tests, or reviews.

### Developer

- Implements code changes and unit tests only.
- Produces `implementation_summary.md`.
- Must not write integration tests or UI-automated tests.

### Tester

- Owns integration testing and UI verification activity.
- Prefers Playwright when UI automation is practical.
- May use manual UI verification when automation is not yet practical.
- Produces `testing_results.md`.

### Reviewer

- Reviews both implementation and testing activity.
- Confirms code quality and constitution alignment.
- Confirms the implemented behavior received appropriate testing depth.
- Produces `review_results.md`.

## Testing policy inside the workflow

- `developer` owns unit tests.
- `tester` owns integration tests.
- `tester` should prefer Playwright for UI-affected behavior when automation is practical.
- Manual UI verification is an allowed fallback only when Playwright coverage is not yet practical.
- `reviewer` must verify that testing depth matches the implemented behavior.

## Recommended status transitions

- `draft` -> `planned`
- `planned` -> `in_development`
- `in_development` -> `in_testing`
- `in_testing` -> `in_review`
- `in_review` -> `ready`
- `in_review` -> `rework_required`
- `rework_required` -> `in_development`
- `rework_required` -> `in_testing`
- any status -> `blocked`
- `ready` -> `closed`

## Minimum artifact expectations

### `feature.md`

Should capture at least:

- feature id
- feature title
- status
- priority
- source request
- current stage
- current owner
- blockers
- next action
- linked artifacts

### `developer_handoff.md`

Should capture at least:

- objective
- scope
- assumptions
- out-of-scope items
- likely implementation surfaces
- unit-test expectations
- follow-on testing notes for `tester`

### `implementation_summary.md`

Should capture at least:

- files changed
- behavior changed
- unit tests added or updated
- validation performed
- known limitations
- testing areas that still need `tester`

### `testing_results.md`

Should capture at least:

- test scope selected
- integration tests created or updated
- Playwright tests created or updated when applicable
- manual UI verification performed when used as fallback
- commands executed
- pass or fail outcomes
- blockers, failures, and follow-up recommendations

### `review_results.md`

Should capture at least:

- code review findings
- testing activity review findings
- missing evidence
- required fixes
- readiness recommendation for `orchestrator`

## Initial policy

- Use `.work/WORKFLOW.md` as the canonical workflow document.
- Use `.work/features/` for all feature tracking going forward.
- Keep one feature folder per feature.
- Allow multiple smaller work-items inside the same feature folder when they belong to the same feature.
- Use `feature.md` as the canonical metadata and status file.
