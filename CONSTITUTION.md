# Aegis Engineering Constitution

This constitution defines the non-negotiable standards for implementation agents in this repository.

## 1) Mission

- Deliver correct, secure, maintainable software that satisfies the user’s requested outcome.
- Prioritize reliable execution over speculative design or unnecessary complexity.

## 2) Core Principles

- Requirement first: optimize for fulfilling the explicit task and acceptance criteria.
- Evidence over assertion: claims about completion must be supported by verification output.
- Smallest correct change: keep scope tight and avoid unrelated refactors.
- Convention alignment: follow existing architecture, style, naming, and tooling.
- Safety first: avoid destructive operations unless explicitly requested.

## 3) Implementation Rules

- Understand requirements before editing; infer defaults from repository context.
- Read affected code paths before making changes.
- Preserve backward compatibility unless a breaking change is explicitly requested.
- Include tests/docs updates whenever behavior changes.
- Do not commit or push unless explicitly asked by the user.

## 4) Approved Technology Stack (Allowlist)

Agents MUST use only the approved stack unless the user explicitly approves an exception.

- Backend runtime/framework: .NET 10
- Language: C# 14
- Primary database: PostgreSQL
- ORM: Entity Framework Core
- API: ASP.NET REST and SignalR (for realtime UI updates)
- Orchestration: .NET Aspire
- Containerization: Docker
- Unit testing: xUnit, NSubstitute, Shouldly
- End-to-end testing: Playwright
- Date/time library: NodaTime (preferred for all domain date/time handling)
- Frontend UI framework: Next.js (initial standard)

Dependency policy:

- Do not introduce alternative frameworks/libraries for the same concerns without explicit approval.
- Prefer built-in platform capabilities first, then approved libraries.
- If a new dependency is truly required, the agent must:
  1. Explain why existing approved tools are insufficient.
  2. Propose the minimal package(s) needed.
  3. Mark the task as pending approval before adding it.

## 5) Testing Policy

Testing selection order:

- Prefer unit tests first, then integration tests, then Playwright for critical user journeys.
- Do not use Playwright as the primary vehicle for business-rule validation when unit/integration tests can cover the behavior.

When to use each test type:

- Unit tests (`xUnit`, `NSubstitute`, `Shouldly`): domain logic, validators, mappings, service behavior, edge cases, and failure paths.
- Integration tests: EF Core query behavior, migrations, API contracts, serialization, authentication/authorization policies, and SignalR hub interactions.
- End-to-end tests (`Playwright`): high-value UI workflows, cross-page interactions, and user-visible regressions including realtime updates.

Minimum expectations:

- Bug fix: add at least one regression test that would fail before the fix and pass after.
- New feature: cover happy path plus at least one meaningful edge/failure case.
- Realtime behavior: verify server-side emission and client/UI reaction.
- Date/time behavior: include timezone and boundary-condition coverage using `NodaTime`.

Verification reporting requirements:

- Include exact test/validation commands executed.
- State which test scope was chosen (unit/integration/e2e) and why.
- Report pass/fail outcomes.
- If any tests are skipped, explicitly state what was skipped and why.

## 6) Mandatory Verification (Completion Gate)

Before declaring a task complete, the agent MUST:

1. Run relevant validation (tests, lint, build, or equivalent checks).
2. Verify the implementation satisfies the stated requirement (not just compile success).
3. Re-run checks after fixes until they pass.

The agent MUST NOT claim completion based only on code edits.

If validation cannot be executed (missing env, credentials, runtime, or other blockers), the agent must:

- Clearly state what could not be verified.
- Explain the blocker.
- Provide exact commands for the user to run.
- Mark the task as not fully verified.

## 7) Definition of Done

A task is done only when all are true:

- Requested behavior is implemented.
- Relevant tests/checks pass locally (or blockers are explicitly documented).
- Edge cases and failure paths are reasonably handled.
- Change summary includes what changed, where, and how it was validated.

## 8) Communication Contract

- Be concise and factual.
- State assumptions when requirements are implicit.
- Report validation commands and outcomes.
- Separate verified facts from recommendations.

## 9) Prohibited Behaviors

- Declaring completion without testing and requirement verification.
- Performing destructive git/file operations without explicit instruction.
- Introducing unrelated changes to satisfy a focused request.
- Hiding failures, skipped checks, or uncertainty.
- Introducing non-approved stack alternatives without explicit user approval.

## 10) Escalation Conditions

Ask for clarification only when blocked by:

- Ambiguity that materially changes implementation.
- Destructive/irreversible actions with significant risk.
- Missing secrets/credentials or inaccessible dependencies.

When escalating, present a recommended default and expected impact.
