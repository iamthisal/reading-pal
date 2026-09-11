using System;
using InventoryService.Models;
using Xunit;

namespace InventoryService.Tests
{
    public class BookModelTests
    {
        [Fact]
        public void Book_DefaultValues_AreProperlyInitialized()
        {
            // Act
            var book = new Book();

            // Assert
            Assert.Equal(0, book.Id);
            Assert.Equal(string.Empty, book.Title);
            Assert.Equal(string.Empty, book.Author);
            Assert.Equal(string.Empty, book.ISBN);
            Assert.Equal(string.Empty, book.Genre);
            Assert.Equal(0, book.TotalCopies);
            Assert.Equal(0, book.AvailableCopies);
            Assert.True(book.CreatedAt <= DateTime.UtcNow);
            Assert.True(book.UpdatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void Book_CanSetProperties_Correctly()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var book = new Book
            {
                Id = 10,
                Title = "Clean Code",
                Author = "Robert C. Martin",
                ISBN = "978-0132350884",
                Genre = "Software Engineering",
                TotalCopies = 5,
                AvailableCopies = 5,
                CreatedAt = now,
                UpdatedAt = now
            };

            // Assert
            Assert.Equal(10, book.Id);
            Assert.Equal("Clean Code", book.Title);
            Assert.Equal("Robert C. Martin", book.Author);
            Assert.Equal("978-0132350884", book.ISBN);
            Assert.Equal("Software Engineering", book.Genre);
            Assert.Equal(5, book.TotalCopies);
            Assert.Equal(5, book.AvailableCopies);
            Assert.Equal(now, book.CreatedAt);
            Assert.Equal(now, book.UpdatedAt);
        }
    }
}

