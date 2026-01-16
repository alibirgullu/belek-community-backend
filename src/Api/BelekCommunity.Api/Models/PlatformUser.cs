using System;
using System.ComponentModel.DataAnnotations;

namespace BelekCommunity.Api.Models
{
    public class PlatformUser
    {
        public int Id { get; set; }

        [Required]
        public Guid ExternalId { get; set; }

        [Required]
        public string Email { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;
    }
}
