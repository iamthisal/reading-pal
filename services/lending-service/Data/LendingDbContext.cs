using LendingService.Models;
using Microsoft.EntityFrameworkCore;

namespace LendingService.Data
{
    public class LendingDbContext : DbContext
    {
        public LendingDbContext(DbContextOptions<LendingDbContext> options) : base(options) { }

        public DbSet<Reservation> Reservations { get; set; } = null!;
        public DbSet<ReservationEventOutbox> ReservationEvents { get; set; } = null!;
        public DbSet<BorrowRecord> BorrowRecords { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Reservation>().Property(r => r.Status).IsConcurrencyToken();
            modelBuilder.Entity<BorrowRecord>().HasIndex(b => b.ReservationId).IsUnique();
            modelBuilder.Entity<BorrowRecord>().HasOne<Reservation>().WithOne()
                .HasForeignKey<BorrowRecord>(b => b.ReservationId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ReservationEventOutbox>().HasIndex(e => new { e.PublishedAtUtc, e.CreatedAtUtc });
            modelBuilder.Entity<ReservationEventOutbox>().HasIndex(e => e.ReservationId).IsUnique();
        }
    }
}
