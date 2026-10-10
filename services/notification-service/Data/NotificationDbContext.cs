using Microsoft.EntityFrameworkCore;
using NotificationService.Models;

namespace NotificationService.Data;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var notification = modelBuilder.Entity<Notification>();
        notification.HasIndex(n => new { n.EventId, n.Audience }).IsUnique();
        notification.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAtUtc });
        notification.HasIndex(n => new { n.Audience, n.IsRead, n.CreatedAtUtc });
        notification.Property(n => n.Audience).HasMaxLength(16);
        notification.Property(n => n.Type).HasMaxLength(32);
        notification.Property(n => n.BookTitle).HasMaxLength(200);
        notification.Property(n => n.Message).HasMaxLength(500);
    }
}
