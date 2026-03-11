# StarLoom Rewrite Execution Route Diff Checklist

## Scope

- Original project: `C:\Users\YLC\Desktop\StarLoom`
- Rewrite worktree: `C:\Users\YLC\Desktop\StarLoom\.worktrees\starloom-rewrite`
- Goal: compare execution-route parity instead of only UI parity

## Conclusion

Execution-route parity has converged.

All previously-audited differences are now closed by the six execution batches in `docs/plans/2026-03-11-execution-route-parity-convergence.md`.

## Batch Closure Map

- Task 1 closed items `1, 2, 3, 32, 33, 34`
- Task 2 closed items `4, 5, 6, 7, 8, 9, 10, 11, 13, 16, 17, 18, 35, 36`
- Task 3 closed items `12, 27, 28, 29, 30, 31`
- Task 4 closed items `19, 20, 21, 22`
- Task 5 closed items `14, 15, 23, 24, 25, 26`
- Task 6 re-verified the combined route and closed the remaining audit state

## Resolved Checklist

- [x] 1. Startup now persists injected defaults immediately through `ConfigStore`.
- [x] 2. Plugin shutdown stops an active workflow before disposing runtime services.
- [x] 3. Localization reload logs and preserves prior translations instead of hard-failing.
- [x] 4. Configured workflow now returns to the required start location before beginning.
- [x] 5. Configured workflow validates collectable prerequisites before live execution.
- [x] 6. Managed Artisan-list ownership is restored in the rewrite orchestrator.
- [x] 7. Takeover is driven by free-slot pressure plus live turn-in availability again.
- [x] 8. Configured workflow can actively request and wait for Artisan pause handoff.
- [x] 9. The safe local-control pause gate is restored before local automation starts.
- [x] 10. Monitoring mode finalizes when pending purchases are gone or takeover work ends.
- [x] 11. Turn-in-only start validates collectable prerequisites before execution.
- [x] 12. Turn-in-only regains navigation to the configured collectable destination.
- [x] 13. Purchase-only start validates empty, invalid, and already-satisfied purchase lists.
- [x] 14. Purchase runtime now reads live currency instead of starting from hardcoded zero.
- [x] 15. Purchase flow restores NPC interaction and shop-opening execution steps.
- [x] 16. Configured workflow again executes turn-in, optional purchase, then finalization.
- [x] 17. Purchase branching is now based on real pending quantities, not configured item count.
- [x] 18. Final completion routes through return-or-close dispatch instead of unconditional return.
- [x] 19. Turn-in queue construction now uses live inventory and collectable eligibility.
- [x] 20. Turn-in entries now carry resolved job ids for the active collectable route.
- [x] 21. Turn-in execution restores submit, overcap, inventory-drop, and loop semantics.
- [x] 22. `CollectableShopGame` is now a real execution adapter instead of a stub.
- [x] 23. Purchase queue construction now reflects real inventory counts and live currency.
- [x] 24. Purchase batching now enforces the original per-transaction `99` cap.
- [x] 25. `ScripShopGame` now drives page selection, item confirmation, and inventory verification.
- [x] 26. Purchase catalog metadata now syncs back into configured items when the catalog shifts.
- [x] 27. Navigation requests now carry real coordinates, territory ids, radius, and Lifestream data.
- [x] 28. Travel runtime restores teleport, optional Lifestream, pathfind, and arrival checks.
- [x] 29. Workflow navigation requests now propagate `useLifestream` where route data requires it.
- [x] 30. Return-home behavior is restored as a dedicated execution chain, not generic navigation.
- [x] 31. `PostPurchaseAction.CloseGame` is now part of runtime completion routing.
- [x] 32. Collectable-shop config preserves the route metadata required by turn-in and purchase travel.
- [x] 33. Return-point config retains the data needed for inn, housing, and apartment return routes.
- [x] 34. Purchase settings now affect live takeover and purchase batching semantics again.
- [x] 35. Home-page actions now map back to the restored orchestrator semantics under the rewrite UI.
- [x] 36. Failure-state presentation is aligned with the original overlay-state mapping.

## Verification Record

- Focused route tests were run batch-by-batch after each implementation step.
- Final integration verification is owned by Task 6:
  - `dotnet test C:\Users\YLC\Desktop\StarLoom\.worktrees\starloom-rewrite\StarLoom.Tests\StarLoom.Tests.csproj -v minimal`
  - `dotnet build C:\Users\YLC\Desktop\StarLoom\.worktrees\starloom-rewrite\StarLoom.csproj -v minimal`
