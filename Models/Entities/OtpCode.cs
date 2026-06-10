using System.ComponentModel.DataAnnotations;

namespace MedicalApp.API.Models.Entities
{
    /// <summary>
    /// OTP codes for forgot password flow (phone-based).
    /// </summary>
    public class OtpCode : BaseEntity
    {
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(6)]
        public string Code { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; } = false;
    }
}
