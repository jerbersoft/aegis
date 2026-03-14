# Implementation Summary

## Classification
- Behavior-changing.

## What changed
- Fixed the watchlist SignalR client to send the approved `snake_case` subscription payload (`watchlist_id`) instead of camelCase, which unblocked real authenticated watchlist subscriptions in the AppHost browser path.
- Added client-side normalization for realtime ack/snapshot payloads so the web consumer tolerates the approved backend `snake_case` wire fields while keeping internal TypeScript views camelCase.
- Fixed watchlist snapshot state rollover so selecting a different watchlist does not ignore a fresh batch `1` snapshot just because the previous watchlist had a higher batch number.
- Added regression coverage for the snake_case request/payload path and watchlist-switch snapshot reset behavior.

## Validation run
- `npm test` in `src/Aegis.Web` — passed (`14` tests).
- `npm run lint` in `src/Aegis.Web` — passed.
- `dotnet test "aegis.sln"` in the implementation worktree — passed (`58` tests total across Universe and MarketData suites).

## Requirement-focused verification
- `dotnet run --project "src/Aegis.AppHost/Aegis.AppHost.csproj" > "/tmp/aegis-task003-dev-apphost.log" 2>&1 &` — started AppHost successfully.
- `sleep 25 && curl -k -I https://localhost:17032` — passed (`HTTP/2 404` from Aspire dashboard root, confirming host reachability).
- `sleep 25 && curl -I http://localhost:3001` — passed (`307` redirect to `/login`).
- `sleep 25 && curl -I http://localhost:5078` — passed (`404`, confirming backend reachability).
- `node -e '...create watchlist fixture via web API...'` in `src/Aegis.Web` — passed; created authenticated watchlist fixture with `AMD`, then ran `/api/market-data/bootstrap/run`.
- `node -e '...playwright browser verification...'` in `src/Aegis.Web` — passed; real AppHost browser session showed the watchlist row as `AMD$359.500.28%ERemove` and status badges included `live` plus `Live watchlist prices • as of ... UTC`, proving Watchlists no longer stayed `OFFLINE` and no longer rendered `— / —` after bootstrap.
- `pkill ...; sleep 5; pgrep ...` — passed; cleaned up AppHost/backend/web/playwright processes.

## Tester follow-up
- Re-run the existing AppHost browser verification for task-003 to confirm the fixed browser path and keep broader Home/reconnect regression coverage aligned with prior tester scope.

## Artifact status
- `implementation_summary.md` updated.
