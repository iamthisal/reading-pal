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
        public DbSet<Fine> Fines { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Reservation>().Property(r => r.Status).IsConcurrencyToken();
            modelBuilder.Entity<BorrowRecord>().HasIndex(b => b.ReservationId).IsUnique();
            modelBuilder.Entity<BorrowRecord>().HasOne<Reservation>().WithOne()
                .HasForeignKey<BorrowRecord>(b => b.ReservationId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ReservationEventOutbox>().HasIndex(e => new { e.PublishedAtUtc, e.CreatedAtUtc });
            modelBuilder.Entity<ReservationEventOutbox>().Property(e => e.EventType).HasMaxLength(64);
            modelBuilder.Entity<ReservationEventOutbox>().HasIndex(e => new { e.ReservationId, e.EventType }).IsUnique();
            modelBuilder.Entity<Fine>().HasIndex(f => f.BorrowRecordId).IsUnique();
            modelBuilder.Entity<Fine>().HasOne<BorrowRecord>().WithOne()
                .HasForeignKey<Fine>(f => f.BorrowRecordId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Fine>().Property(f => f.Amount).HasPrecision(12, 2);
            modelBuilder.Entity<Fine>().Property(f => f.DailyRate).HasPrecision(12, 2);
        }
    }
}
