using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.Models.Entities
{
    public class FamilyMember : BaseEntity
    {
        [Required]
        public int PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public Patient Patient { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public RelationType Relation { get; set; }

        [Required]
        public int Age { get; set; }

        public Gender Gender { get; set; }

        [MaxLength(50)]
        public string? BloodType { get; set; }

        [MaxLength(500)]
        public string? MedicalHistory { get; set; }

        [MaxLength(200)]
        public string? Allergies { get; set; }

        [MaxLength(200)]
        public string? ChronicDiseases { get; set; }

        // Navigation property for appointments booked for this family member
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
