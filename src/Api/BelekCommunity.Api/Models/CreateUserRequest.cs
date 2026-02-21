using System.ComponentModel.DataAnnotations;

namespace BelekCommunity.Api.Models
{
    public class CreateUserRequest
    {
        [Required(ErrorMessage = "E-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        public string Password { get; set; } = null!;
    }
}