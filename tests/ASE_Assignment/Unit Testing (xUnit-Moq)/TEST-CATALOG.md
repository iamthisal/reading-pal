# Complete test catalogue

Each row is one test method. A Theory runs once for each InlineData row; its cases are counted separately in the README results table.

| Service | Test class | Test name | What it proves |
| --- | --- | --- | --- |
| Inventory | BooksControllerTests | `Create_ValidBook_SavesTrimmedBook` | Creating a book trims text and puts all copies on the shelf. |
| Inventory | BooksControllerTests | `Create_DuplicateIsbn_ReturnsConflict` | ISBN duplicates are rejected even with different casing and spaces. |
| Inventory | BooksControllerTests | `Update_CopyBoundary_PreservesBorrowedCopies` | Total copies cannot fall below the two copies already borrowed. |
| Inventory | BooksControllerTests | `Update_NullDescription_KeepsExistingDescription` | An omitted description keeps the existing description. |
| Inventory | BooksControllerTests | `Delete_BorrowedCopies_ReturnsConflict` | A book with borrowed copies cannot be deleted. |
| Inventory | BooksControllerTests | `Delete_MissingBook_ReturnsNotFound` | Deleting an unknown book reports a missing resource. |
| Inventory | BooksControllerTests | `MarkUnavailable_BorrowedCopies_KeepsBorrowedTotal` | Marking unavailable removes shelf copies while keeping borrowed copies. |
| Inventory | BooksControllerTests | `Create_InvalidModel_DoesNotSaveBook` | Invalid model state prevents a book from being saved. |
| Inventory | BookSearchControllerTests | `Search_PageSizeBoundary_UsesAllowedSize` | Page sizes use the default below one and stop at the maximum of 48. |
| Inventory | BookSearchControllerTests | `Search_AvailableOnly_ExcludesUnavailableBook` | Search excludes unavailable books when availability is requested. |
| Inventory | CatalogueRulesTests | `Delete_GenreInUse_ReturnsConflict` | A genre assigned to a book cannot be removed. |
| Inventory | CatalogueRulesTests | `Update_AuthorRenamed_UpdatesMatchingBooks` | Renaming an author also changes the author on their books. |
| Inventory | CatalogueRulesTests | `Add_RepeatedFavourite_StoresOneRow` | Adding a favourite twice stores only one favourite for that reader. |
| Inventory | CatalogueRulesTests | `Add_MissingIdentity_ReturnsUnauthorized` | A missing identity cannot add favourites. |
| Inventory | CatalogueRulesTests | `SetItems_DuplicateBookIds_ReturnsBadRequest` | A curated list cannot contain the same book twice. |
| Inventory | CatalogueRulesTests | `SetItems_UnknownBookId_ReturnsBadRequest` | Unknown book IDs cannot be saved as curated-list items. |
| Inventory | KafkaBookEventPublisherTests | `PublishAsync_UnsupportedEvent_ThrowsArgumentException` | Unknown event types fail explicitly and publisher options are read once. |
| Lending | DueDateReminderServiceTests | `QueueRemindersAsync_DueWindowBoundary_QueuesEligibleLoan` | Only loans due today, tomorrow or in two days receive reminders. |
| Lending | DueDateReminderServiceTests | `QueueRemindersAsync_ReturnedLoan_QueuesNothing` | A returned loan is excluded from reminders. |
| Lending | DueDateReminderServiceTests | `QueueRemindersAsync_AlreadyReminded_QueuesNothing` | Repeating reminder processing never queues a second reminder for the loan. |
| Lending | KafkaReservationEventPublisherTests | `PublishAsync_UnsupportedEvent_ThrowsBeforeReadingTopics` | Unknown lending events fail before topic configuration is accessed. |
| Lending | ReservationOutboxDispatcherTests | `DispatchAsync_PendingMessage_PublishesOnce` | A pending message is published exactly once and marked as published. |
| Lending | ReservationOutboxDispatcherTests | `DispatchAsync_PublishedMessage_DoesNotPublish` | A previously published message is never sent again. |
| Lending | ReservationOutboxDispatcherTests | `DispatchAsync_PublisherUnavailable_KeepsMessagePending` | An unavailable publisher leaves the message pending for retry. |
| Lending | ReservationRulesTests | `Reject_PendingReservation_QueuesCancellation` | Admin rejection cancels a pending reservation and queues its event without HTTP. |
| Lending | ReservationRulesTests | `Accept_MissingReservation_ReturnsNotFound` | Invalid and unknown reservation IDs fail before any HTTP client is requested. |
| Lending | ReservationRulesTests | `ReserveBook_InvalidIdentity_RejectsBeforeHttp` | Missing, malformed and admin identities cannot reserve books. |
| Lending | ReservationRulesTests | `Cancel_OwnPendingReservation_QueuesCancellation` | A reader can cancel their pending reservation and queue one cancellation event. |
| Lending | ReservationRulesTests | `Cancel_AnotherReadersReservation_ReturnsNotFound` | A reader cannot cancel another reader's reservation. |
| Lending | ReservationRulesTests | `Return_MissingLoan_ReturnsNotFound` | Returning a missing loan does not contact Inventory. |
| Lending | ReservationSnapshotServiceTests | `QueueSnapshotsAsync_ReservationStatus_QueuesOnlyActive` | Snapshots include active reservations and exclude completed ones. |
| Lending | ReservationSnapshotServiceTests | `QueueSnapshotsAsync_ExistingSnapshot_QueuesNothing` | An existing snapshot prevents duplicate snapshot events. |
| Lending | ReturnFineCalculatorTests | `DaysOverdue_ReturnDayBoundary_ReturnsNonnegativeDays` | Early and same-day returns have no overdue days, while late returns do. |
| Lending | ReturnFineCalculatorTests | `DaysOverdue_ColomboMidnight_CountsOneDay` | Crossing midnight in Sri Lanka counts as one day even within the same UTC date. |
| Lending | ReturnFineCalculatorTests | `DaysUntilDue_DueDayBoundary_ReturnsSignedDays` | Days-until-due retains negative, zero and positive calendar differences. |
| Notification | CatalogEventHandlerTests | `HandleAsync_NewBook_CreatesTwoAudiences` | A new book creates one customer announcement and one admin notice. |
| Notification | CatalogEventHandlerTests | `HandleAsync_DuplicateBookEvent_KeepsTwoNotifications` | Redelivering a catalogue event adds no extra notifications. |
| Notification | CatalogEventHandlerTests | `HandleAsync_InvalidCatalogPayload_SkipsEvent` | Unusable catalogue messages are skipped without saving notifications. |
| Notification | LendingEventHandlerTests | `HandleAsync_ClosedReservation_KeepsClosedStatus` | A delayed acceptance event cannot reopen a closed reservation. |
| Notification | LendingEventHandlerTests | `HandleAsync_AcceptedEvent_CreatesNotification` | An accepted event saves a notification and looks up its title exactly once. |
| Notification | LendingEventHandlerTests | `HandleAsync_DuplicateEvent_DoesNotLookUpTitleAgain` | A repeated event creates no duplicate and makes no second title lookup. |
| Notification | LendingEventHandlerTests | `HandleAsync_InvalidPayload_SkipsEvent` | Null, empty and malformed payloads are skipped before lookup or storage. |
| Notification | LendingEventHandlerTests | `HandleAsync_TitleUnavailable_UsesFallbackTitle` | An unavailable title still creates a notification using a book-ID fallback. |
| Notification | LendingEventHandlerTests | `HandleAsync_TitleLookupThrows_DoesNotSaveNotification` | A failed title lookup propagates so a later delivery can retry. |
| Notification | LookupFallbackTests | `GetTitleAsync_InvalidBaseUrl_ReturnsNull` | Missing or invalid inventory configuration returns no title without HTTP. |
| Notification | LookupFallbackTests | `GetNamesAsync_InvalidBaseUrl_ReturnsEmptyNames` | Missing or invalid user-service configuration returns no names without HTTP. |
| Notification | NotificationFactoryTests | `CreateForUser_AcceptedReservation_IncludesTitleAndDueDate` | Accepted reservations create a user message with the local due date. |
| Notification | NotificationFactoryTests | `CreateForUser_FineBoundary_NotifiesOnlyPositiveFine` | Fine notices require both a positive amount and positive overdue days. |
| Notification | NotificationFactoryTests | `CreateForUser_MissingTitle_UsesBookId` | Missing or blank titles fall back to the book ID. |
| Notification | NotificationFactoryTests | `CreateForAdmin_CancellationActor_NotifiesOnlyCustomerAction` | Admins hear about customer cancellations but not their own rejections. |
| Notification | NotificationFactoryTests | `CreateForUser_UnknownEvent_ReturnsNullForBothAudiences` | Unsupported events do not produce notifications for either audience. |
| Notification | NotificationReadStoreTests | `MarkReadAsync_DuplicateIds_AddsOneReadRecord` | Duplicate notification IDs create only one read record. |
| Notification | NotificationReadStoreTests | `MarkReadAsync_AlreadyRead_AddsNothing` | Marking an already-read notification is harmless. |
| Notification | NotificationReadStoreTests | `MarkReadAsync_DifferentRecipients_KeepsSeparateReadRecords` | One recipient reading a notification does not mark it read for another. |
| Notification | NotificationVisibilityTests | `MarkRead_AnotherCustomersNotification_ReturnsNotFound` | A customer cannot mark another customer's notification as read. |
| Notification | NotificationVisibilityTests | `Get_AdminsOwnChange_HidesNotification` | Admins do not see notices about their own catalogue changes or request customer names. |
| User | AdminControllerTests | `AcceptUser_PendingUser_ApprovesUser` | Accepting a pending member records approval and enables access. |
| User | AdminControllerTests | `AcceptUser_MissingId_ReturnsNotFound` | Invalid and unknown IDs cannot approve a member. |
| User | AdminControllerTests | `RejectUser_PendingUser_DeletesUser` | Rejecting a pending member deletes the account. |
| User | AdminControllerTests | `RevokeUser_ActiveUser_ClearsApproval` | Revoking a member clears both approval and its timestamp. |
| User | AdminControllerTests | `RevokeUser_AdminAccount_KeepsApproval` | Administrator access cannot be revoked by the member-revocation action. |
| User | AuthControllerTests | `Register_NewEmail_SavesUnapprovedUser` | Registration saves an unapproved user with a hashed password. |
| User | AuthControllerTests | `Register_DuplicateEmail_DoesNotAddUser` | A duplicate email cannot create a second account. |
| User | AuthControllerTests | `Login_CorrectPassword_ReturnsUserToken` | Correct credentials produce a user token and read JWT settings once. |
| User | AuthControllerTests | `Login_InvalidPassword_ReturnsUnauthorized` | Empty and incorrect passwords cannot produce a token. |
| User | AuthControllerTests | `Login_UnknownEmail_ReturnsUnauthorized` | An unknown email is rejected before token generation. |
| User | UserControllerTests | `UpdateProfile_NullPassword_KeepsPasswordHash` | Profile updates keep the password when no replacement is supplied. |
| User | UserControllerTests | `UpdateProfile_NewPassword_ReplacesPasswordHash` | A supplied replacement password is hashed and saved. |
| User | UserControllerTests | `GetProfile_InvalidIdentity_ReturnsUnauthorized` | Missing, empty and malformed token identities cannot read a profile. |
| User | UserControllerTests | `GetProfile_MissingUser_ReturnsNotFound` | A valid identity with no matching account is reported as missing. |
| User | UserControllerTests | `UpdateProfile_DuplicateEmail_DoesNotChangeProfile` | Changing to another user's email leaves the original profile intact. |
