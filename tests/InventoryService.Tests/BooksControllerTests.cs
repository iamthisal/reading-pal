using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using InventoryService.Controllers;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InventoryService.Tests
{
    public class BooksControllerTests
    {
        private InventoryDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<InventoryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new InventoryDbContext(options);
        }

        // TC-BOOK-001: Admin can add a book successfully and receives 201 Created
        [Fact]
        public async Task Admin_Can_Add_Book_Successfully()
        {
            // Arrange
            using var context = GetDbContext();
            var controller = new BooksController(context);
            var request = new CreateBookRequest
            {
                Title = "The Pragmatic Programmer",
                Author = "David Thomas, Andrew Hunt",
                ISBN = "978-0135957059",
                Genre = "Technology",
                TotalCopies = 5
            };

            // Act
            var result = await controller.Create(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(201, createdResult.StatusCode);

            var response = Assert.IsType<BookResponse>(createdResult.Value);
            Assert.True(response.Id > 0);
            Assert.Equal("The Pragmatic Programmer", response.Title);
            Assert.Equal("David Thomas, Andrew Hunt", response.Author);
            Assert.Equal("978-0135957059", response.ISBN);
            Assert.Equal("Technology", response.Genre);
            Assert.Equal(5, response.TotalCopies);
        }

        // TC-BOOK-002: Available copies must equal Total copies at creation
        [Fact]
        public async Task AvailableCopies_Equals_TotalCopies_On_Creation()
        {
            // Arrange
            using var context = GetDbContext();
            var controller = new BooksController(context);
            var request = new CreateBookRequest
            {
                Title = "Design Patterns",
                Author = "Erich Gamma, Richard Helm, Ralph Johnson, John Vlissides",
                ISBN = "978-0201633610",
                Genre = "Software Architecture",
                TotalCopies = 8
            };

            // Act
            var result = await controller.Create(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var response = Assert.IsType<BookResponse>(createdResult.Value);

            Assert.Equal(8, response.TotalCopies);
            Assert.Equal(8, response.AvailableCopies);

            // Verify persisted entity in database
            var dbBook = await context.Books.FindAsync(response.Id);
            Assert.NotNull(dbBook);
            Assert.Equal(8, dbBook!.TotalCopies);
            Assert.Equal(8, dbBook.AvailableCopies);
        }

        // TC-BOOK-003: System sets CreatedAt and UpdatedAt timestamps automatically
        [Fact]
        public async Task System_Sets_CreatedAt_And_UpdatedAt_Automatically()
        {
            // Arrange
            using var context = GetDbContext();
            var controller = new BooksController(context);
            var before = DateTime.UtcNow.AddSeconds(-1);
            var request = new CreateBookRequest
            {
                Title = "Refactoring",
                Author = "Martin Fowler",
                ISBN = "978-0134757599",
                Genre = "Software Design",
                TotalCopies = 3
            };

            // Act
            var result = await controller.Create(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var response = Assert.IsType<BookResponse>(createdResult.Value);
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.InRange(response.CreatedAt, before, after);
            Assert.InRange(response.UpdatedAt, before, after);
            Assert.Equal(response.CreatedAt, response.UpdatedAt);
        }

        // TC-BOOK-004: Create endpoint requires Admin role authorization
        [Fact]
        public void Add_Book_Requires_Admin_Role()
        {
            // Inspect method-level or class-level [Authorize(Roles = "Admin")]
            var methodInfo = typeof(BooksController).GetMethod(nameof(BooksController.Create));
            Assert.NotNull(methodInfo);

            var authorizeAttribute = methodInfo!.GetCustomAttribute<AuthorizeAttribute>()
                ?? typeof(BooksController).GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorizeAttribute);
            Assert.Equal("Admin", authorizeAttribute!.Roles);
        }

        // TC-BOOK-005: Duplicate ISBN returns Conflict (409)
        [Fact]
        public async Task Add_Book_With_Duplicate_ISBN_Returns_Conflict()
        {
            // Arrange
            using var context = GetDbContext();
            context.Books.Add(new Book
            {
                Title = "Existing Book",
                Author = "Existing Author",
                ISBN = "978-0132350884",
                Genre = "Technology",
                TotalCopies = 2,
                AvailableCopies = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = new BooksController(context);
            var request = new CreateBookRequest
            {
                Title = "New Book with Same ISBN",
                Author = "Another Author",
                ISBN = "978-0132350884",
                Genre = "Technology",
                TotalCopies = 5
            };

            // Act
            var result = await controller.Create(request);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal(409, conflictResult.StatusCode);
        }

        // TC-BOOK-006: Invalid model state returns BadRequest (400)
        [Fact]
        public async Task Add_Book_With_Invalid_ModelState_Returns_BadRequest()
        {
            // Arrange
            using var context = GetDbContext();
            var controller = new BooksController(context);
            controller.ModelState.AddModelError("Title", "Title is required");

            var request = new CreateBookRequest
            {
                Title = "",
                Author = "Author",
                ISBN = "123456",
                Genre = "Genre",
                TotalCopies = 1
            };

            // Act
            var result = await controller.Create(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        // TC-BOOK-007: Get book by ID returns correct book when found
        [Fact]
        public async Task GetById_Returns_Book_When_Exists()
        {
            // Arrange
            using var context = GetDbContext();
            var book = new Book
            {
                Title = "Domain-Driven Design",
                Author = "Eric Evans",
                ISBN = "978-0321125217",
                Genre = "Software Architecture",
                TotalCopies = 4,
                AvailableCopies = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var controller = new BooksController(context);

            // Act
            var result = await controller.GetById(book.Id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<BookResponse>(okResult.Value);
            Assert.Equal(book.Id, response.Id);
            Assert.Equal("Domain-Driven Design", response.Title);
            Assert.Equal("Eric Evans", response.Author);
        }

        // TC-BOOK-008: Get book by ID returns NotFound when not exists
        [Fact]
        public async Task GetById_Returns_NotFound_When_Not_Exists()
        {
            // Arrange
            using var context = GetDbContext();
            var controller = new BooksController(context);

            // Act
            var result = await controller.GetById(999);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        // TC-BOOK-009: GetAll returns all books
        [Fact]
        public async Task GetAll_Returns_All_Books()
        {
            // Arrange
            using var context = GetDbContext();
            context.Books.AddRange(
                new Book
                {
                    Title = "Book A",
                    Author = "Author A",
                    ISBN = "ISBN-A",
                    Genre = "Genre A",
                    TotalCopies = 2,
                    AvailableCopies = 2
                },
                new Book
                {
                    Title = "Book B",
                    Author = "Author B",
                    ISBN = "ISBN-B",
                    Genre = "Genre B",
                    TotalCopies = 4,
                    AvailableCopies = 4
                }
            );
            await context.SaveChangesAsync();

            var controller = new BooksController(context);

            // Act
            var result = await controller.GetAll();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var books = Assert.IsAssignableFrom<IEnumerable<BookResponse>>(okResult.Value);
            Assert.Equal(2, books.Count());
        }

        // TC-BOOK-UPDATE-001: Admin can update book details successfully and receives 200 OK
        [Fact]
        public async Task Admin_Can_Update_Book_Successfully()
        {
            // Arrange
            using var context = GetDbContext();
            var book = new Book
            {
                Title = "Original Title",
                Author = "Original Author",
                ISBN = "978-0135957059",
                Genre = "Technology",
                TotalCopies = 5,
                AvailableCopies = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var controller = new BooksController(context);
            var request = new UpdateBookRequest
            {
                Title = "Updated Title",
                Author = "Updated Author",
                ISBN = "978-0135957059",
                Genre = "Computer Science",
                TotalCopies = 8
            };

            // Act
            var result = await controller.Update(book.Id, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<BookResponse>(okResult.Value);

            Assert.Equal(book.Id, response.Id);
            Assert.Equal("Updated Title", response.Title);
            Assert.Equal("Updated Author", response.Author);
            Assert.Equal("978-0135957059", response.ISBN);
            Assert.Equal("Computer Science", response.Genre);
            Assert.Equal(8, response.TotalCopies);
            Assert.Equal(8, response.AvailableCopies);

            var dbBook = await context.Books.FindAsync(book.Id);
            Assert.NotNull(dbBook);
            Assert.Equal("Updated Title", dbBook!.Title);
            Assert.Equal("Updated Author", dbBook.Author);
            Assert.Equal(8, dbBook.TotalCopies);
            Assert.Equal(8, dbBook.AvailableCopies);
        }

        // TC-BOOK-UPDATE-002: Update endpoint requires Admin role authorization
        [Fact]
        public void Update_Book_Requires_Admin_Role()
        {
            var methodInfo = typeof(BooksController).GetMethod(nameof(BooksController.Update));
            Assert.NotNull(methodInfo);

            var authorizeAttribute = methodInfo!.GetCustomAttribute<AuthorizeAttribute>()
                ?? typeof(BooksController).GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorizeAttribute);
            Assert.Equal("Admin", authorizeAttribute!.Roles);
        }

        // TC-BOOK-UPDATE-003: Updating non-existent book returns NotFound (404)
        [Fact]
        public async Task Update_Book_Returns_NotFound_When_Not_Exists()
        {
            // Arrange
            using var context = GetDbContext();
            var controller = new BooksController(context);
            var request = new UpdateBookRequest
            {
                Title = "Some Title",
                Author = "Some Author",
                ISBN = "978-0135957059",
                Genre = "Technology",
                TotalCopies = 5
            };

            // Act
            var result = await controller.Update(999, request);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        // TC-BOOK-UPDATE-004: Updating book with duplicate ISBN of another book returns Conflict (409)
        [Fact]
        public async Task Update_Book_With_Duplicate_ISBN_Returns_Conflict()
        {
            // Arrange
            using var context = GetDbContext();
            context.Books.AddRange(
                new Book
                {
                    Title = "Book One",
                    Author = "Author One",
                    ISBN = "978-1111111111",
                    Genre = "Fiction",
                    TotalCopies = 3,
                    AvailableCopies = 3
                },
                new Book
                {
                    Title = "Book Two",
                    Author = "Author Two",
                    ISBN = "978-2222222222",
                    Genre = "Fiction",
                    TotalCopies = 4,
                    AvailableCopies = 4
                }
            );
            await context.SaveChangesAsync();

            var bookTwo = await context.Books.FirstAsync(b => b.ISBN == "978-2222222222");

            var controller = new BooksController(context);
            var request = new UpdateBookRequest
            {
                Title = "Book Two Renamed",
                Author = "Author Two",
                ISBN = "978-1111111111", // Conflict with Book One
                Genre = "Fiction",
                TotalCopies = 4
            };

            // Act
            var result = await controller.Update(bookTwo.Id, request);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal(409, conflictResult.StatusCode);
        }

        // TC-BOOK-UPDATE-005: Updating book keeping same ISBN succeeds without conflict
        [Fact]
        public async Task Update_Book_Keeping_Same_ISBN_Succeeds()
        {
            // Arrange
            using var context = GetDbContext();
            var book = new Book
            {
                Title = "Book One",
                Author = "Author One",
                ISBN = "978-1111111111",
                Genre = "Fiction",
                TotalCopies = 3,
                AvailableCopies = 3
            };
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var controller = new BooksController(context);
            var request = new UpdateBookRequest
            {
                Title = "Book One (Revised Edition)",
                Author = "Author One",
                ISBN = "978-1111111111", // Same ISBN
                Genre = "Fiction",
                TotalCopies = 5
            };

            // Act
            var result = await controller.Update(book.Id, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<BookResponse>(okResult.Value);
            Assert.Equal("Book One (Revised Edition)", response.Title);
            Assert.Equal("978-1111111111", response.ISBN);
            Assert.Equal(5, response.TotalCopies);
        }

        // TC-BOOK-UPDATE-006: Invalid model state returns BadRequest (400)
        [Fact]
        public async Task Update_Book_With_Invalid_ModelState_Returns_BadRequest()
        {
            // Arrange
            using var context = GetDbContext();
            var book = new Book
            {
                Title = "Valid Book",
                Author = "Valid Author",
                ISBN = "978-1234567890",
                Genre = "Tech",
                TotalCopies = 2,
                AvailableCopies = 2
            };
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var controller = new BooksController(context);
            controller.ModelState.AddModelError("Title", "Title is required");

            var request = new UpdateBookRequest
            {
                Title = "",
                Author = "Valid Author",
                ISBN = "978-1234567890",
                Genre = "Tech",
                TotalCopies = 2
            };

            // Act
            var result = await controller.Update(book.Id, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        // TC-BOOK-UPDATE-007: Updating TotalCopies correctly adjusts AvailableCopies
        [Fact]
        public async Task Update_Book_TotalCopies_Adjusts_AvailableCopies_Correctly()
        {
            // Arrange
            using var context = GetDbContext();
            var book = new Book
            {
                Title = "Sample Book",
                Author = "Sample Author",
                ISBN = "978-3333333333",
                Genre = "Tech",
                TotalCopies = 10,
                AvailableCopies = 7 // 3 copies currently borrowed
            };
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var controller = new BooksController(context);
            var request = new UpdateBookRequest
            {
                Title = "Sample Book",
                Author = "Sample Author",
                ISBN = "978-3333333333",
                Genre = "Tech",
                TotalCopies = 12 // Increased by 2
            };

            // Act
            var result = await controller.Update(book.Id, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<BookResponse>(okResult.Value);
            Assert.Equal(12, response.TotalCopies);
            Assert.Equal(9, response.AvailableCopies); // 7 + 2 = 9 (or 12 - 3 borrowed = 9)

            var dbBook = await context.Books.FindAsync(book.Id);
            Assert.NotNull(dbBook);
            Assert.Equal(12, dbBook!.TotalCopies);
            Assert.Equal(9, dbBook.AvailableCopies);
        }

        // TC-BOOK-UPDATE-008: Updating TotalCopies to less than borrowed copies returns BadRequest (400)
        [Fact]
        public async Task Update_Book_TotalCopies_Less_Than_Borrowed_Returns_BadRequest()
        {
            // Arrange
            using var context = GetDbContext();
            var book = new Book
            {
                Title = "Sample Book",
                Author = "Sample Author",
                ISBN = "978-4444444444",
                Genre = "Tech",
                TotalCopies = 10,
                AvailableCopies = 6 // 4 copies borrowed
            };
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var controller = new BooksController(context);
            var request = new UpdateBookRequest
            {
                Title = "Sample Book",
                Author = "Sample Author",
                ISBN = "978-4444444444",
                Genre = "Tech",
                TotalCopies = 3 // Less than 4 borrowed copies
            };

            // Act
            var result = await controller.Update(book.Id, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        // TC-BOOK-UPDATE-009: Update modifies UpdatedAt timestamp while preserving CreatedAt
        [Fact]
        public async Task Update_Book_Updates_UpdatedAt_And_Preserves_CreatedAt()
        {
            // Arrange
            using var context = GetDbContext();
            var originalCreatedAt = DateTime.UtcNow.AddDays(-5);
            var originalUpdatedAt = DateTime.UtcNow.AddDays(-5);

            var book = new Book
            {
                Title = "Old Book",
                Author = "Old Author",
                ISBN = "978-5555555555",
                Genre = "History",
                TotalCopies = 5,
                AvailableCopies = 5,
                CreatedAt = originalCreatedAt,
                UpdatedAt = originalUpdatedAt
            };
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var controller = new BooksController(context);
            var request = new UpdateBookRequest
            {
                Title = "Updated Book Title",
                Author = "Old Author",
                ISBN = "978-5555555555",
                Genre = "History",
                TotalCopies = 5
            };

            var before = DateTime.UtcNow.AddSeconds(-1);

            // Act
            var result = await controller.Update(book.Id, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<BookResponse>(okResult.Value);
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.Equal(originalCreatedAt, response.CreatedAt);
            Assert.InRange(response.UpdatedAt, before, after);
            Assert.True(response.UpdatedAt > originalUpdatedAt);
        }

        // TC-BOOK-DELETE-001: Admin can remove an available book successfully
        [Fact]
        public async Task Admin_Can_Delete_Book_Successfully()
        {
            using var context = GetDbContext();
            var book = new Book
            {
                Title = "Book to Remove",
                Author = "Author",
                ISBN = "978-6666666666",
                Genre = "Fiction",
                TotalCopies = 2,
                AvailableCopies = 2
            };
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var controller = new BooksController(context);
            var result = await controller.Delete(book.Id);

            Assert.IsType<NoContentResult>(result);
            Assert.Null(await context.Books.FindAsync(book.Id));
        }

        // TC-BOOK-DELETE-002: Delete endpoint requires Admin role authorization
        [Fact]
        public void Delete_Book_Requires_Admin_Role()
        {
            var methodInfo = typeof(BooksController).GetMethod(nameof(BooksController.Delete));
            Assert.NotNull(methodInfo);

            var authorizeAttribute = methodInfo!.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorizeAttribute);
            Assert.Equal("Admin", authorizeAttribute!.Roles);
        }

        // TC-BOOK-DELETE-003: Removing a missing book returns NotFound
        [Fact]
        public async Task Delete_Book_Returns_NotFound_When_Not_Exists()
        {
            using var context = GetDbContext();
            var controller = new BooksController(context);

            var result = await controller.Delete(999);

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        // TC-BOOK-DELETE-004: Books with borrowed copies cannot be removed
        [Fact]
        public async Task Delete_Book_With_Borrowed_Copies_Returns_Conflict()
        {
            using var context = GetDbContext();
            var book = new Book
            {
                Title = "Borrowed Book",
                Author = "Author",
                ISBN = "978-7777777777",
                Genre = "Fiction",
                TotalCopies = 3,
                AvailableCopies = 2
            };
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var controller = new BooksController(context);
            var result = await controller.Delete(book.Id);

            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(409, conflictResult.StatusCode);
            Assert.NotNull(await context.Books.FindAsync(book.Id));
        }

    }
}

