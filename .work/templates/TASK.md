# Task

## Metadata
Feature ID: {{feature_id}}
Feature Folder: {{feature_folder}}
Task ID: {{task_id}}
Task Folder: {{task_folder}}
Title: {{task_title}}
Status: {{task_status}}
Current Owner: {{current_owner}}
Acceptance Status: {{acceptance_status}}
Acceptance Document: {{acceptance_document_reference}}
Created Date: {{created_date}}
Last Updated: {{last_updated}}

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`
Recommended Task Status Values: `draft | in_progress | ready | blocked | closed`

## Objective
{{objective}}

## Scope
{{scope}}

## Dependencies
- {{dependency_1_or_none}}
- {{dependency_2_or_none}}

## Blockers
- {{blocker_1_or_none}}
- {{blocker_2_or_none}}

Status note:

- Use `blocked` only when the next required step cannot proceed because of a concrete dependency, missing evidence, or environment limitation.
- Use `in_progress` for any active execution-loop phase; use `Current Owner` and the task artifacts to show whether the task is in development, testing, review, or rework.
- Use `ready` only after development, testing, and review are complete.
- Use `closed` only after the task is represented in feature-level `ACCEPTANCE.md`.

## Next Action
{{next_action}}

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
{{notes}}
