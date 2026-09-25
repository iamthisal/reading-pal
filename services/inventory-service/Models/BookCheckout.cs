namespace InventoryService.Models;

// One deduction per Lending reservation, including retries after a lost HTTP response.
public class BookCheckout
{
    public int Id { get; set; } // Lending reservation ID
    public int BookId { get; set; }
    public DateTime CheckoutDateUtc { get; set; }
}
