# Mutation testing summary

Stryker.NET mutation testing of each microservice's unit tests. Score = (killed + timeout) / (killed + timeout + survived + no coverage).

| Service | Run | Killed | Timeout | Survived | No coverage | Mutation score |
|---|---|---|---|---|---|---|
| InventoryService | 2026-10-18.18-04-15 | 341 | 0 | 153 | 53 | **62.34%** |
| LendingService | 2026-10-18.18-06-54 | 284 | 0 | 176 | 88 | **51.82%** |
| NotificationService | 2026-10-18.18-09-40 | 237 | 0 | 90 | 87 | **57.25%** |
| UserService | 2026-10-18.17-47-06 | 78 | 0 | 56 | 25 | **49.06%** |
