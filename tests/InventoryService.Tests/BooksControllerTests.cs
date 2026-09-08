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
    }
}

