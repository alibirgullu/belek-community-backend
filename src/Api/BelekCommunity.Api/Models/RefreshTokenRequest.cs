using System.ComponentModel.DataAnnotations;

namespace BelekCommunity.Api.Models
{
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
