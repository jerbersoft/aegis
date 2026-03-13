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

---

## 6. Failures Or Blockers

- {{failure_or_blocker_1}}
- {{failure_or_blocker_2}}

If none:

- None

---

## 7. Coverage Assessment

{{coverage_assessment}}

---

## 8. Recommended Next Step

{{recommended_next_step}}
