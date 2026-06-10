using System.ComponentModel.DataAnnotations;

namespace MedicalApp.API.Models.Entities
{
    /// <summary>
    /// Maps a phone number to a Telegram Chat ID.
    /// This allows us to send OTP codes via a Telegram Bot for free.
    /// </summary>
    public class TelegramMapping : BaseEntity
    {
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TelegramChatId { get; set; } = string.Empty;
    }
}
