# Feature

## Metadata
Feature ID: {{feature_id}}
Feature Folder: {{feature_folder}}
Title: {{feature_title}}
Priority: {{priority}}
Status: {{feature_status}}
Current Active Task: {{current_task_or_none}}
Current Owner: {{current_owner}}
Main Workspace Path: {{main_workspace_path}}
Main Workspace Branch: {{main_workspace_branch}}
Main Workspace Branch Verified: {{main_workspace_branch_verified}}
Recorded Base Branch: {{recorded_base_branch}}
Recorded Worktree Branch: {{recorded_worktree_branch}}
Recorded Worktree Path: {{recorded_worktree_path}}
PR Status: {{pr_status}}
PR URL: {{pr_url_or_none}}
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
- Keep `Main Workspace Path`, `Main Workspace Branch`, and `Main Workspace Branch Verified` aligned with the orchestration preflight state.
- If `Recorded Worktree Path` is missing or matches `Main Workspace Path`, treat the feature as blocked and do not delegate implementation.
- Keep `PR Status` and `PR URL` aligned with the real close-flow outcome when the feature enters close handling.
- Keep environment metadata aligned with the currently prepared worktree state and only list processes started or tracked by `orchestrator`.
- After `ACCEPTANCE.md` is created, `orchestrator` should proactively prepare the acceptance environment from the recorded worktree, record the resulting environment/process state here, and present an owner-facing preview of the acceptance guide.
- When the owner says `close this feature` or equivalent, `orchestrator` should stop the prepared acceptance environment, finalize feature closure bookkeeping, and then commit/push/create the PR from the recorded worktree branch to the recorded base branch unless blocked.
