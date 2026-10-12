using Microsoft.EntityFrameworkCore;
using InventoryService.Models;

namespace InventoryService.Data
{
    public class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
        {
        }

        public DbSet<Book> Books => Set<Book>();
        public DbSet<Genre> Genres => Set<Genre>();
        public DbSet<BookCheckout> BookCheckouts => Set<BookCheckout>();
        public DbSet<Author> Authors => Set<Author>();
        public DbSet<CuratedList> CuratedLists => Set<CuratedList>();
        public DbSet<CuratedListItem> CuratedListItems => Set<CuratedListItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<BookCheckout>().Property(c => c.Id).ValueGeneratedNever();

            modelBuilder.Entity<Book>(entity =>
            {
                entity.HasKey(b => b.Id);
                entity.Property(b => b.AvailableCopies).IsConcurrencyToken();
                entity.HasIndex(b => b.ISBN).IsUnique();
            });

            modelBuilder.Entity<Genre>(entity =>
            {
                entity.HasKey(g => g.Id);
                entity.HasIndex(g => g.Name).IsUnique();
            });

            modelBuilder.Entity<Author>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.HasIndex(a => a.Name).IsUnique();
            });

            modelBuilder.Entity<CuratedList>(entity =>
            {
                entity.HasKey(l => l.Id);
                entity.HasIndex(l => l.Key).IsUnique();
                entity.Property(l => l.ItemType).HasConversion<string>().HasMaxLength(20);
            });

            modelBuilder.Entity<CuratedListItem>(entity =>
            {
                entity.HasKey(i => i.Id);
                entity.HasOne(i => i.CuratedList).WithMany(l => l.Items)
                    .HasForeignKey(i => i.CuratedListId).OnDelete(DeleteBehavior.Cascade);
                // Deleting a book or an author removes it from every list it was on.
                entity.HasOne(i => i.Book).WithMany()
                    .HasForeignKey(i => i.BookId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(i => i.Author).WithMany()
                    .HasForeignKey(i => i.AuthorId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(i => new { i.CuratedListId, i.BookId }).IsUnique();
                entity.HasIndex(i => new { i.CuratedListId, i.AuthorId }).IsUnique();
            });
        }
    }
}
