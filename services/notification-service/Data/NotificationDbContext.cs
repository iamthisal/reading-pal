using Microsoft.EntityFrameworkCore;
using NotificationService.Models;

namespace NotificationService.Data;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRead> NotificationReads => Set<NotificationRead>();
    public DbSet<ReservationState> ReservationStates => Set<ReservationState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var notification = modelBuilder.Entity<Notification>();
        // One event can notify the customer and the admins, or several customers (a deleted book).
        notification.HasIndex(n => new { n.EventId, n.Audience, n.UserId }).IsUnique();
        notification.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAtUtc });
        notification.HasIndex(n => new { n.Audience, n.IsRead, n.CreatedAtUtc });
        notification.Property(n => n.Audience).HasMaxLength(16);
        notification.Property(n => n.Type).HasMaxLength(32);
        notification.Property(n => n.BookTitle).HasMaxLength(200);
        notification.Property(n => n.Message).HasMaxLength(500);

        var read = modelBuilder.Entity<NotificationRead>();
        read.HasIndex(r => new { r.NotificationId, r.UserId }).IsUnique();
        read.HasOne<Notification>().WithMany().HasForeignKey(r => r.NotificationId).OnDelete(DeleteBehavior.Cascade);

        var state = modelBuilder.Entity<ReservationState>();
        state.HasKey(s => s.ReservationId);
        state.Property(s => s.ReservationId).ValueGeneratedNever();
        state.Property(s => s.Status).HasMaxLength(16);
        state.HasIndex(s => new { s.BookId, s.Status });
    }
}
