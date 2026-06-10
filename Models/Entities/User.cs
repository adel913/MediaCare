using System.ComponentModel.DataAnnotations;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.Models.Entities
{
    /// <summary>
    /// Represents a user in the system (Patient, Doctor, or ClinicAdmin).
    /// Login is phone-based as per the Flutter UI design.
    /// </summary>
    public class User : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public UserRole Role { get; set; }

        public Gender? Gender { get; set; }

        public int? Age { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [MaxLength(500)]
        public string? ProfileImageUrl { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public Patient? Patient { get; set; }
        public Doctor? Doctor { get; set; }
        public ClinicAdmin? ClinicAdmin { get; set; }
        public List<RefreshToken>? RefreshTokens { get; set; }
    }
}
