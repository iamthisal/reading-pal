namespace InventoryService.Models
{
    /// <summary>One entry in a curated list: a book or an author, depending on the list's item type.</summary>
    public class CuratedListItem
    {
        public int Id { get; set; }

        public int CuratedListId { get; set; }
        public CuratedList? CuratedList { get; set; }

        /// <summary>Order within the list, starting at 0.</summary>
        public int Position { get; set; }

        public int? BookId { get; set; }
        public Book? Book { get; set; }

        public int? AuthorId { get; set; }
        public Author? Author { get; set; }
    }
}
