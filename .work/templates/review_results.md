# Review Results

## Task Information
Feature ID: {{feature_id}}
Feature Title: {{feature_title}}
Feature Folder: {{feature_folder}}
Task ID: {{task_id}}
Task Title: {{task_title}}
Task Folder: {{task_folder}}

Reviewed By: Reviewer Agent
Date: {{date}}

---

## 1. Inputs Reviewed

- `TASK.md`: {{reviewed_yes_or_no}}
- `developer_handoff.md`: {{reviewed_yes_or_no}}
- `implementation_summary.md`: {{reviewed_yes_or_no}}
- `testing_results.md`: {{reviewed_yes_or_no}}
- Relevant changed files: {{reviewed_yes_or_no}}

---

## 2. Code Review Findings

- Constitution alignment: {{pass_fail_needs_follow_up}}
- Repository conventions: {{pass_fail_needs_follow_up}}
- Scope control: {{pass_fail_needs_follow_up}}
- Unit-test expectations: {{pass_fail_needs_follow_up}}

Findings:

- {{code_review_finding_1}}
- {{code_review_finding_2}}

---

## 3. Testing Activity Review Findings

- Integration coverage present when needed: {{pass_fail_needs_follow_up}}
- UI verification present when needed: {{pass_fail_needs_follow_up}}
- Playwright used when practical: {{pass_fail_needs_follow_up}}
- Manual fallback justified when used: {{pass_fail_needs_follow_up}}
- Reported evidence is credible and complete: {{pass_fail_needs_follow_up}}
- Browser blocker/capability assessment is specific and evidence-based: {{pass_fail_needs_follow_up}}
- Mixed browser vs automated/API evidence is clearly separated: {{pass_fail_needs_follow_up}}

Findings:

- {{testing_review_finding_1}}
- {{testing_review_finding_2}}

---

## 4. Missing Evidence

- {{missing_evidence_1}}
- {{missing_evidence_2}}

Transient browser-state fixture assessment:

- Deterministic fixture exists: {{yes_no_or_not_applicable}}
- If not, reviewer treatment: {{blocking_or_non_blocking_with_reason}}

If none:

- None

---

## 5. Required Fixes

- {{required_fix_1}}
- {{required_fix_2}}

If none:

- None

---

## 6. Readiness Assessment

- Ready to proceed: {{yes_or_no}}
- Needs developer follow-up: {{yes_or_no}}
- Needs tester follow-up: {{yes_or_no}}
- Blocked pending clarification: {{yes_or_no}}
- Browser-only transient-state evidence still required: {{yes_or_no}}

---

## 7. Recommendation To Orchestrator

{{recommendation_to_orchestrator}}
