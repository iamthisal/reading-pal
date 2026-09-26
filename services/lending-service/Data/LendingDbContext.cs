using LendingService.Models;
using Microsoft.EntityFrameworkCore;

namespace LendingService.Data
{
    public class LendingDbContext : DbContext
    {
        public LendingDbContext(DbContextOptions<LendingDbContext> options) : base(options) { }

        public DbSet<Reservation> Reservations { get; set; } = null!;
        public DbSet<ReservationEventOutbox> ReservationEvents { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Reservation>().Property(r => r.Status).IsConcurrencyToken();
            modelBuilder.Entity<ReservationEventOutbox>().HasIndex(e => new { e.PublishedAtUtc, e.CreatedAtUtc });
            modelBuilder.Entity<ReservationEventOutbox>().HasIndex(e => e.ReservationId).IsUnique();
        }
    }
}
