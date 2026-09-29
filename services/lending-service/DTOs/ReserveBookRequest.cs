using System.ComponentModel.DataAnnotations;

namespace LendingService.DTOs
{
    public class ReserveBookRequest
    {
        [Required]
        public int BookId { get; set; }
    }
}
