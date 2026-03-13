# Testing Results

## Task Information
Feature ID: {{feature_id}}
Feature Title: {{feature_title}}
Feature Folder: {{feature_folder}}
Task ID: {{task_id}}
Task Title: {{task_title}}
Task Folder: {{task_folder}}

Tested By: Tester Agent
Date: {{date}}

---

## 1. Testing Scope

- Integration tests: {{yes_or_no}}
- Playwright tests: {{yes_or_no}}
- Manual UI verification: {{yes_or_no}}
- Browser verification required by scope: {{yes_or_no}}

Reasoning:

{{why_this_test_depth_was_chosen}}

---

## 2. Implementation Summary Used

- Source file: `implementation_summary.md`
- Key behaviors under test:
  - {{behavior_1}}
  - {{behavior_2}}

---

## 3. Tests Created or Updated

Integration tests:

- {{integration_test_file_or_none}}

Playwright tests:

- {{playwright_test_file_or_none}}

Manual verification notes:

- {{manual_verification_note_or_none}}

Browser verification capability check:

- `Aegis.AppHost` startup: {{capability_result_or_not_applicable}}
- Aspire endpoint reachability: {{capability_result_or_not_applicable}}
- Browser automation launch: {{capability_result_or_not_applicable}}
- Login path: {{capability_result_or_not_applicable}}
- Target page access: {{capability_result_or_not_applicable}}
- Concrete blocker/error: {{capability_blocker_or_none}}

Deterministic transient-state fixture:

- Exists: {{yes_no_or_not_applicable}}
- Details: {{fixture_details_or_none}}
- If absent, why partial browser coverage is expected: {{fixture_gap_reason_or_none}}

---

## 4. Commands Executed

```text
{{command_1}}
{{command_2}}
```

---

## 5. Results

- Overall result: {{pass_fail_blocked}}
- Integration result: {{pass_fail_not_run}}
- Playwright result: {{pass_fail_not_run}}
- Manual UI result: {{pass_fail_not_run}}
- Browser-confirmed coverage: {{browser_confirmed_scope}}
- Automated/API-only coverage: {{automated_only_scope}}

---

## 6. Failures Or Blockers

- {{failure_or_blocker_1}}
- {{failure_or_blocker_2}}

If none:

- None

---

## 7. Coverage Assessment

{{coverage_assessment}}

If browser coverage is partial, explain:

- which states were confirmed in the real UI
- which states were verified only through automated or API evidence
- whether the remaining gap is blocking or non-blocking, and why

Aspire/browser cleanup evidence:

- Cleanup commands executed: {{cleanup_commands_or_none}}
- Cleanup verification result: {{cleanup_verification_result_or_none}}

---

## 8. Recommended Next Step

{{recommended_next_step}}
