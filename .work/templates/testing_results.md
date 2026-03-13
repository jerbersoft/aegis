# Testing Results

## Feature Information
Feature ID: {{feature_id}}
Feature Title: {{feature_title}}
Feature Folder: {{feature_folder}}

Tested By: Tester Agent
Date: {{date}}

---

## 1. Testing Scope

Selected test depth:

- Integration tests: {{yes_or_no}}
- Playwright tests: {{yes_or_no}}
- Manual UI verification: {{yes_or_no}}

Reasoning:

{{why_this_test_depth_was_chosen}}

---

## 2. Implementation Summary Used

Reference the implementation summary consumed for testing.

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

---

## 4. Commands Executed

List exact commands run.

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

---

## 6. Failures or Blockers

- {{failure_or_blocker_1}}
- {{failure_or_blocker_2}}

If none:

- None

---

## 7. Coverage Assessment

Did testing appear sufficient for the implemented change?

{{coverage_assessment}}

---

## 8. Recommended Next Step

- Send to reviewer
- Return to developer
- Re-run testing after fixes
- Blocked pending clarification

Selected next step:

{{recommended_next_step}}
