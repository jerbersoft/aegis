# Feature

## Metadata
Feature ID: {{feature_id}}
Feature Folder: {{feature_folder}}
Title: {{feature_title}}
Priority: {{priority}}
Status: {{feature_status}}
Current Active Task: {{current_task_or_none}}
Current Owner: {{current_owner}}
Recorded Base Branch: {{recorded_base_branch}}
Recorded Worktree Branch: {{recorded_worktree_branch}}
Recorded Worktree Path: {{recorded_worktree_path}}
Environment Status: {{environment_status}}
Last Prepared At: {{last_prepared_at}}
Created Date: {{created_date}}
Last Updated: {{last_updated}}

## Source
Request Source: {{request_source}}
Requested By: {{requested_by}}

## Objective
{{objective}}

## Scope
{{scope}}

## Feature-Level Blockers
- {{feature_blocker_or_none}}

## Started Processes
- {{started_process_1_or_none}}
- {{started_process_2_or_none}}

## Task Index
- Repeat this pattern for as many tasks as needed.
- `task-001-{{task_1_slug}}` - {{task_1_title}} - {{task_1_status}} - depends on: {{task_1_dependencies_or_none}}
- `task-002-{{task_2_slug}}` - {{task_2_title}} - {{task_2_status}} - depends on: {{task_2_dependencies_or_none}}

Status note:

- Keep task index statuses aligned with each task's `TASK.md` so `planner` does not re-select already approved work.

## Next Action
{{next_action}}

## Planning Notes
{{planning_notes}}

## Linked Artifacts
- `ACCEPTANCE.md`
- `tasks/`

## Notes
{{notes}}

Workflow status notes:

- Keep `Current Active Task`, task statuses, and `Next Action` aligned with the actual execution loop state.
- Keep the feature `in_progress` until acceptance work is complete, even if all tasks are already `ready`.
- Keep environment metadata aligned with the currently prepared worktree state and only list processes started or tracked by `orchestrator`.
