# PoolDesk 1.1.2 verification

2026-11-26: all 28 offline tests passed on Windows.

Includes all-stage slot selection, exclusion of unrelated slots, actual collection writes with an edited name, blank-name validation, dialog cancellation, collection.db safety, download error recovery, and UI rendering. Tests use isolated temporary game folders; no live osu! installation was modified.

Build limitation: NuGet vulnerability audit returned NU1900 because api.nuget.org was unreachable; vulnerability audit was not completed.
