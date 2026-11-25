# Mutation testing summary

Stryker.NET mutation testing of the ASE unit tests. Score = (killed + timeout) / (killed + timeout + survived + no coverage).

| Service | Run | Killed | Timeout | Survived | No coverage | Mutation score |
| --- | --- | --- | --- | --- | --- | --- |
| InventoryService | 2026-11-25 00:23 UTC | 74 | 0 | 164 | 309 | **13.53%** |
| LendingService | 2026-11-25 00:25 UTC | 80 | 0 | 59 | 409 | **14.6%** |
| NotificationService | 2026-11-25 00:28 UTC | 77 | 0 | 100 | 237 | **18.6%** |
| UserService | 2026-11-25 00:20 UTC | 54 | 0 | 63 | 42 | **33.96%** |
