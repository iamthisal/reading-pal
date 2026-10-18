# Controller endpoint coverage (definitions, not passed tests)

59 of 59 controller actions have a matching test request definition. Health routes are additionally checked in each service reads scenario. Swagger infrastructure is not load-tested.

|Service|Method|Route|Section:scenario in all-services.jmx|
|---|---|---|---|
|inventory|GET|`/api/Authors`|inventory-service:reads|
|inventory|GET|`/api/Authors/{id:int}`|inventory-service:reads|
|inventory|GET|`/api/Authors/profile`|inventory-service:negative; inventory-service:reads|
|inventory|GET|`/api/Authors/{id:int}/books`|inventory-service:reads|
|inventory|POST|`/api/Authors`|inventory-service:crud|
|inventory|PUT|`/api/Authors/{id:int}`|inventory-service:crud|
|inventory|DELETE|`/api/Authors/{id:int}`|inventory-service:crud|
|inventory|GET|`/api/Books`|inventory-service:reads|
|inventory|GET|`/api/Books/{id}`|inventory-service:negative; inventory-service:reads; lending-service:cancel; lending-service:reject; lending-service:return; mixed-user-workload:reads|
|inventory|POST|`/api/Books/{id:int}/checkouts/{reservationId:int}`|lending-service:return|
|inventory|POST|`/api/Books/{id:int}/checkouts/{reservationId:int}/return`|lending-service:return|
|inventory|DELETE|`/api/Books/{id}`|inventory-service:crud; lending-service:cancel; lending-service:reject; lending-service:return|
|inventory|POST|`/api/Books`|inventory-service:crud; inventory-service:negative; lending-service:cancel; lending-service:reject; lending-service:return|
|inventory|PUT|`/api/Books/{id}`|inventory-service:crud|
|inventory|PATCH|`/api/Books/{id}/mark-unavailable`|inventory-service:crud|
|inventory|PATCH|`/api/Books/{id}/mark-available`|inventory-service:crud|
|inventory|GET|`/api/books/search`|inventory-service:reads; mixed-user-workload:reads|
|inventory|GET|`/api/Favourites`|inventory-service:favourites|
|inventory|GET|`/api/Favourites/ids`|inventory-service:crud; inventory-service:favourites|
|inventory|PUT|`/api/Favourites/{bookId:int}`|inventory-service:crud|
|inventory|DELETE|`/api/Favourites/{bookId:int}`|inventory-service:crud|
|inventory|GET|`/api/Genres`|inventory-service:reads|
|inventory|GET|`/api/Genres/{id:int}/books`|inventory-service:reads|
|inventory|POST|`/api/Genres`|inventory-service:crud|
|inventory|PUT|`/api/Genres/{id}`|inventory-service:crud|
|inventory|DELETE|`/api/Genres/{id}`|inventory-service:crud|
|inventory|GET|`/api/Lists`|inventory-service:reads|
|inventory|GET|`/api/Lists/{key}`|inventory-service:reads|
|inventory|POST|`/api/Lists`|inventory-service:crud|
|inventory|PUT|`/api/Lists/{key}`|inventory-service:crud|
|inventory|PUT|`/api/Lists/{key}/items`|inventory-service:crud|
|inventory|DELETE|`/api/Lists/{key}`|inventory-service:crud|
|lending|GET|`/api/my-borrowings`|lending-service:cancel; lending-service:negative; lending-service:reads; lending-service:reject; lending-service:return; mixed-user-workload:reads|
|lending|POST|`/api/my-borrowings/reservations/{id:int}/cancel`|lending-service:cancel; lending-service:return|
|lending|GET|`/api/Reservations/pending`|lending-service:admin; lending-service:negative|
|lending|GET|`/api/Reservations/borrowed`|lending-service:admin|
|lending|GET|`/api/Reservations/history`|lending-service:admin|
|lending|POST|`/api/Reservations/{id:int}/accept`|lending-service:return|
|lending|POST|`/api/Reservations/{id:int}/reject`|lending-service:reject|
|lending|POST|`/api/Reservations`|lending-service:cancel; lending-service:reject; lending-service:return|
|lending|POST|`/api/returns/{borrowRecordId:int}`|lending-service:return|
|notification|GET|`/api/admin/notifications`|notification-service:admin; notification-service:admin-read-actions; notification-service:negative|
|notification|GET|`/api/admin/notifications/unread-count`|notification-service:admin; notification-service:admin-read-actions|
|notification|POST|`/api/admin/notifications/{id:int}/read`|notification-service:admin-read-actions|
|notification|POST|`/api/admin/notifications/read-all`|notification-service:admin-read-actions|
|notification|GET|`/api/notifications`|mixed-user-workload:reads; notification-service:negative; notification-service:read-actions; notification-service:reads|
|notification|GET|`/api/notifications/unread-count`|notification-service:read-actions; notification-service:reads|
|notification|POST|`/api/notifications/{id:int}/read`|notification-service:negative; notification-service:read-actions|
|notification|POST|`/api/notifications/read-all`|notification-service:read-actions|
|user|GET|`/api/Admin/message`|user-service:admin|
|user|GET|`/api/Admin/users/pending`|user-service:admin; user-service:lifecycle|
|user|GET|`/api/Admin/users/active`|user-service:admin; user-service:negative|
|user|POST|`/api/Admin/users/{id}/accept`|user-service:lifecycle|
|user|POST|`/api/Admin/users/{id}/reject`|user-service:lifecycle|
|user|POST|`/api/Admin/users/{id}/revoke`|user-service:lifecycle|
|user|POST|`/api/Auth/login`|inventory-service:crud; inventory-service:favourites; inventory-service:negative; lending-service:admin; lending-service:cancel; lending-service:negative; lending-service:reads; lending-service:reject; lending-service:return; mixed-user-workload:reads; notification-service:admin; notification-service:admin-read-actions; notification-service:negative; notification-service:read-actions; notification-service:reads; user-service:admin; user-service:lifecycle; user-service:login; user-service:negative; user-service:reads|
|user|POST|`/api/Auth/register`|user-service:lifecycle; user-service:negative|
|user|GET|`/api/User/profile`|user-service:lifecycle; user-service:negative; user-service:reads|
|user|PUT|`/api/User/profile`|user-service:lifecycle|

Endpoint presence is not exhaustive behaviour coverage or a live test pass. Kafka delivery latency, scheduler timing and overdue calendar calculations require separate fixtures beyond HTTP endpoint enumeration. Read actions need seeded notifications. Expected 4xx checks are distinct from successful business requests; all 5xx fail. No marks or capacity guarantee is implied.

