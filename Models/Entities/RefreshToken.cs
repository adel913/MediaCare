using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalApp.API.Models.Entities
{
    public class RefreshToken : BaseEntity
    {
        public int UserId { get; set; }

        public string Token { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public string CreatedByIp { get; set; } = string.Empty;

        public bool IsRevoked { get; set; }

        public DateTime? RevokedAt { get; set; }

        public string? ReplacedByToken { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}
