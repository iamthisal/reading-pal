using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Controllers;
using UserService.Data;
using UserService.Models;
using Xunit;

namespace UserService.Tests
{
    public class AdminViewUserTests
    {
        private UserDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new UserDbContext(options);
        }

        // TC-ADMIN-001
        // Check that the admin can view the list of registered users.
        [Fact]
        public void Admin_Can_View_Registered_Users()
        {
            using var context = GetDbContext();

            context.Users.AddRange(
                new User
                {
                    Id = 1,
                    FirstName = "Dahamya",
                    LastName = "Kulandi",
                    Email = "dahamyakulandi21@gmail.com",
                    Role = "User",
                    IsValidated = true,
                    CreatedAt = new DateTime(2026, 8, 31)
                },
                new User
                {
                    Id = 2,
                    FirstName = "Lithuli",
                    LastName = "Limansa",
                    Email = "lithu12@gmail.com",
                    Role = "User",
                    IsValidated = true,
                    CreatedAt = new DateTime(2026, 8, 31)
                }
            );

            context.SaveChanges();

            var controller = new AdminController(context);
            var result = controller.GetActiveUsers();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var users = Assert.IsAssignableFrom<IEnumerable<UserService.DTOs.UserSummaryResponse>>(okResult.Value);

            Assert.Equal(2, users.Count());
        }

        // TC-ADMIN-002
        // Check that the admin endpoint requires the Admin role.
        [Fact]
        public void Non_Admin_Cannot_Access_Admin_Endpoint()
        {
            var authorizeAttribute = typeof(AdminController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();

            Assert.NotNull(authorizeAttribute);
            Assert.Equal("Admin", authorizeAttribute!.Roles);
        }

        // TC-ADMIN-003
        // Check that the user's name, email and join date are returned correctly.
        [Fact]
        public void User_Information_Is_Returned_Correctly()
        {
            using var context = GetDbContext();

            var createdDate = new DateTime(2026, 8, 31);

            context.Users.Add(
                new User
                {
                    Id = 1,
                    FirstName = "Dahamya",
                    LastName = "Kulandi",
                    Email = "dahamyakulandi21@gmail.com",
                    Role = "User",
                    IsValidated = true,
                    CreatedAt = createdDate
                }
            );

            context.SaveChanges();

            var controller = new AdminController(context);
            var result = controller.GetActiveUsers();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var users = Assert.IsAssignableFrom<IEnumerable<UserService.DTOs.UserSummaryResponse>>(okResult.Value);

            var user = Assert.Single(users);

            Assert.Equal("Dahamya", user.FirstName);
            Assert.Equal("Kulandi", user.LastName);
            Assert.Equal("dahamyakulandi21@gmail.com", user.Email);
            Assert.Equal(createdDate, user.CreatedAt);
        }

        // TC-ADMIN-004
        // Check that admin accounts are not included in the normal active user list.
        [Fact]
        public void Admin_Accounts_Are_Not_Included_In_User_List()
        {
            using var context = GetDbContext();

            context.Users.AddRange(
                new User
                {
                    Id = 1,
                    FirstName = "Library",
                    LastName = "Admin",
                    Email = "admin@library.com",
                    Role = "Admin",
                    IsValidated = true,
                    CreatedAt = new DateTime(2026, 8, 31)
                },
                new User
                {
                    Id = 2,
                    FirstName = "Lithuli",
                    LastName = "Limansa",
                    Email = "lithu12@gmail.com",
                    Role = "User",
                    IsValidated = true,
                    CreatedAt = new DateTime(2026, 8, 31)
                }
            );

            context.SaveChanges();

            var controller = new AdminController(context);
            var result = controller.GetActiveUsers();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var users = Assert.IsAssignableFrom<IEnumerable<UserService.DTOs.UserSummaryResponse>>(okResult.Value);

            Assert.Single(users);
            Assert.Equal("lithu12@gmail.com", users.First().Email);
        }

        // TC-ADMIN-005
        // Check that an empty list is returned when there are no pending users.
        [Fact]
        public void Pending_User_List_Is_Empty_When_No_Pending_Users()
        {
            using var context = GetDbContext();

            context.Users.Add(
                new User
                {
                    Id = 1,
                    FirstName = "Dahamya",
                    LastName = "Kulandi",
                    Email = "dahamyakulandi21@gmail.com",
                    Role = "User",
                    IsValidated = true,
                    CreatedAt = new DateTime(2026, 8, 31)
                }
            );

            context.SaveChanges();

            var controller = new AdminController(context);
            var result = controller.GetPendingUsers();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var users = Assert.IsAssignableFrom<IEnumerable<UserService.DTOs.UserSummaryResponse>>(okResult.Value);

            Assert.Empty(users);
        }

        // TC-ADMIN-006
        // Check that an empty list is returned when there are no active users.
        [Fact]
        public void Active_User_List_Is_Empty_When_No_Active_Users()
        {
            using var context = GetDbContext();

            context.Users.Add(
                new User
                {
                    Id = 3,
                    FirstName = "Dimagi",
                    LastName = "Hansana",
                    Email = "dimahan@gmail.com",
                    Role = "User",
                    IsValidated = false,
                    CreatedAt = new DateTime(2026, 8, 31)
                }
            );

            context.SaveChanges();

            var controller = new AdminController(context);
            var result = controller.GetActiveUsers();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var users = Assert.IsAssignableFrom<IEnumerable<UserService.DTOs.UserSummaryResponse>>(okResult.Value);

            Assert.Empty(users);
        }
    }
}