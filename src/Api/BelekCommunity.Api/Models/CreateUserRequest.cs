using System.ComponentModel.DataAnnotations;

namespace BelekCommunity.Api.Models
{
    public class CreateUserRequest
    {
        [Required]
        public string Email { get; set; } = null!;

        [Required]
        public string Password { get; set; } = null!;
    }
}
