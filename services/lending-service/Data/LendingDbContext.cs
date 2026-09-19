using LendingService.Models;
using Microsoft.EntityFrameworkCore;

namespace LendingService.Data
{
    public class LendingDbContext : DbContext
    {
        public LendingDbContext(DbContextOptions<LendingDbContext> options) : base(options) { }

        public DbSet<Reservation> Reservations { get; set; } = null!;
    }
}
