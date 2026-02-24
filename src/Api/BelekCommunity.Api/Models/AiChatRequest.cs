using System.ComponentModel.DataAnnotations;

namespace BelekCommunity.Api.Models
{
    public class AiChatRequest
    {
        [Required(ErrorMessage = "Mesaj boş olamaz.")]
        public string Message { get; set; } = string.Empty;
    }
}