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
- Scalability by default: design for sustained high-throughput workloads, efficient resource usage, and predictable performance under market volatility.
- Safety first: avoid destructive operations unless explicitly requested.

## 3) Implementation Rules

- Understand requirements before editing; infer defaults from repository context.
- Read affected code paths before making changes.
- Preserve backward compatibility unless a breaking change is explicitly requested.
- Database naming convention: use singular-form, snake_case names for tables and fields.
- EF Core migrations policy: never write migration files by hand. For any database schema change, always generate migrations using the Entity Framework Core migration tooling (for example, `dotnet ef migrations add <Name>`). This rule is mandatory and must be followed strictly.
- Third-party reference boundary: treat the `lib` folder IBKR project as read-only reference material; do not modify it, and implement all IBKR integration work in a separate project.
- Market-data scale requirement: architecture and implementations must account for thousands of tracked symbols (for example, a 4,000-symbol universe) and bursts of thousands to millions of streaming ticks and quotes per minute.
- Performance requirement: prefer designs that minimize unnecessary allocations, blocking work, chatty I/O, and bottlenecks in ingestion, processing, persistence, and fan-out paths.
- Include tests/docs updates whenever behavior changes.
- Add brief, targeted code comments for complex or non-obvious logic inside functions/methods, especially where intent, invariants, edge-case handling, business rules, or performance constraints may not be obvious from the code alone.
- Keep code comments high-signal: explain why the logic exists or what constraint it preserves, and avoid redundant comments that merely restate straightforward code.
- Production code must not carry test-environment runtime paths such as in-memory-database modes or test-mode execution branches used to avoid proper dev/test infrastructure.
- Temporary bootstrap stubs or fakes are allowed only when they preserve the intended final architecture boundaries, are clearly temporary, and have an expected replacement path.
- Do not commit or push unless explicitly asked by the user.
- `Orchestrator` may commit and push only on the recorded implementation worktree branch when feature publication is part of an explicitly user-approved workflow, and may open the resulting PR against the recorded base branch.
- No agent may commit directly on a base branch, push directly to a base branch, or merge a PR unless the policy is explicitly changed again.

## 4) Approved Technology Stack (Allowlist)

Agents MUST use only the approved stack unless the user explicitly approves an exception.

- Backend runtime/framework: `.NET 10`
- Language: `C# 14`
- Primary database: `PostgreSQL`
- ORM: `Entity Framework Core`
- API: `ASP.NET REST` and `SignalR` (for realtime UI updates)
- Orchestration: `.NET Aspire`
- Containerization: `Docker`
- Unit testing: `xUnit`, `NSubstitute`, `Shouldly`
- End-to-end testing: `Playwright`
- Date/time library: `NodaTime` (preferred for all domain date/time handling)
- Frontend UI framework: `Next.js` (initial standard)
- Frontend language: `TypeScript`
- Frontend styling: `Tailwind CSS`

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
- Prefer PostgreSQL-backed testing via test/dev containers or Aspire-managed local infrastructure for integration and behavior verification rather than embedding in-memory-database support into production runtime code.
- In-memory databases should live in test projects or test host overrides rather than normal production runtime code paths.
- Fake providers or stub integrations are acceptable only when they are serving an approved architectural bootstrap purpose rather than acting as generic test-mode infrastructure.

When to use each test type:

- Unit tests (`xUnit`, `NSubstitute`, `Shouldly`): domain logic, validators, mappings, service behavior, edge cases, and failure paths.
- Integration tests: EF Core query behavior, migrations, API contracts, serialization, authentication/authorization policies, and SignalR hub interactions.
- End-to-end tests (`Playwright`): high-value UI workflows, cross-page interactions, and user-visible regressions including realtime updates.

Browser-based verification policy:

- When validating the project through a browser, always start the ASP.NET Aspire host first via `Aegis.AppHost`.
- Perform backend or web URL testing only against the backend and web endpoints exposed by the running Aspire host.
- Do not treat ad hoc standalone browser launches against hardcoded service URLs as the default local verification path when Aspire orchestration is available.
- After browser-based verification completes, stop or kill the related Aspire, backend, web, and browser-test processes so no unnecessary local test/runtime processes are left running.
- For UI-adjacent or backend-driven features, use browser verification to confirm the real user-visible path, but keep business-rule validation primarily in unit and integration tests whenever those layers can prove the behavior more directly.
- Browser verification does not need to be the sole proof for every transient UI state when those states are difficult to hold deterministically in-browser. In that case, the agent should combine: (1) automated tests that prove the semantics, (2) API or runtime evidence that exposes the state, and (3) at least one real browser path that confirms the production UI wiring and rendered surface.
- If a transient UI state cannot be exercised deterministically in-browser, the agent must document exactly which visible states were confirmed in the browser, which states were verified through automated or API evidence instead, and why a deterministic browser fixture was not available.
- Before reporting browser verification as blocked, the testing agent must perform and document a capability check covering, where applicable: whether `Aegis.AppHost` can be started, whether Aspire-exposed endpoints are reachable, whether browser automation can launch, whether the login path can be exercised, and whether the target page can be opened.
- Agents must not report `test_env_blocked` or equivalent browser-verification blockers without recording the concrete failed capability step and the exact observed error or limitation.

Verification depth by task type:

- Trivial or non-behavioral changes: docs-only updates, copy or text edits, comments, formatting, or equivalent changes that do not alter runtime behavior. These may be verified with relevant low-cost checks such as build, lint, or unit tests to confirm no regressions.
- Behavior-changing work: bug fixes, new features, API changes, UI workflow changes, persistence changes, auth changes, integrations, configuration changes that affect runtime behavior, or any task with observable user/system impact. These require requirement-focused verification that demonstrates the behavior works according to the stated specification, not only that tests or builds pass.
- When in doubt, classify the task as behavior-changing and apply the stricter verification standard.

Coverage guidance:

- Target roughly 80% unit test coverage as a nice-to-have signal, not a hard compliance gate.
- Prioritize high-value tests over chasing coverage numbers; do not add low-importance or superficial tests only to reach 80%.
- Focus unit test effort on complex logic, critical business rules, hot paths, failure handling, and components with meaningful branching or risk.
- If lower coverage is acceptable for a change, prefer explaining the rationale over padding the suite with low-value tests.

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
- If browser coverage is partial, explicitly distinguish what was verified in the real UI, what was verified only through automated or API evidence, and whether the remaining gap is blocking or non-blocking.

## 6) Mandatory Verification (Completion Gate)

Before declaring a task complete, the agent MUST:

1. Classify the task as trivial/non-behavioral or behavior-changing and choose verification depth accordingly.
2. Run relevant validation (tests, lint, build, or equivalent checks).
3. Verify the implementation satisfies the stated requirement (not just compile success).
4. For behavior-changing work, perform requirement-focused verification that shows the implemented behavior works according to specification.
5. Re-run checks after fixes until they pass.

The agent MUST NOT claim completion based only on code edits.
The agent MUST NOT claim completion for behavior-changing work based only on build, lint, or unit/integration test success if the stated requirement still lacks direct verification.

If validation cannot be executed (missing env, credentials, runtime, or other blockers), the agent must:

- Clearly state what could not be verified.
- Explain the blocker.
- Provide exact commands for the user to run.
- Mark the task as not fully verified.

Completion status language:

- Use `complete` only when implementation and the required level of verification have both succeeded.
- Use `implemented, not fully verified` when the code is changed but the required verification could not be completed.
- Use `partial` when only part of the requested scope is implemented or verified.

## 7) Definition of Done

A task is done only when all are true:

- Requested behavior is implemented.
- Scalability and performance implications are considered for any hot path, streaming pipeline, or high-volume data workflow.
- Relevant tests/checks pass locally (or blockers are explicitly documented).
- The verification depth matches the task type: trivial/non-behavioral work gets appropriate regression checks, while behavior-changing work gets requirement-focused verification.
- Edge cases and failure paths are reasonably handled.
- Change summary includes what changed, where, and how it was validated.

## 8) Communication Contract

- Be concise and factual.
- State assumptions when requirements are implicit.
- State the completion status accurately (`complete`, `implemented, not fully verified`, or `partial`) based on verification evidence.
- Report validation commands and outcomes.
- Separate verified facts from recommendations.

## 9) Prohibited Behaviors

- Declaring completion without the verification required for the task type.
- Performing destructive git/file operations without explicit instruction.
- Introducing unrelated changes to satisfy a focused request.
- Modifying third-party source under `lib`, including the IBKR reference project.
- Hiding failures, skipped checks, or uncertainty.
- Introducing non-approved stack alternatives without explicit user approval.

## 10) Escalation Conditions

Ask for clarification only when blocked by:

- Ambiguity that materially changes implementation.
- Destructive/irreversible actions with significant risk.
- Missing secrets/credentials or inaccessible dependencies.

When escalating, present a recommended default and expected impact.
