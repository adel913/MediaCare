using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalApp.API.Models.Entities
{
    public class Patient : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(500)]
        public string? MedicalHistory { get; set; }

        [MaxLength(50)]
        public string? BloodType { get; set; }

        [MaxLength(200)]
        public string? Allergies { get; set; }

        [MaxLength(200)]
        public string? ChronicDiseases { get; set; }

        [MaxLength(100)]
        public string? EmergencyContactName { get; set; }

        [MaxLength(20)]
        public string? EmergencyContactPhone { get; set; }

        // Navigation properties
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<FamilyMember> FamilyMembers { get; set; } = new List<FamilyMember>();
    }
}
