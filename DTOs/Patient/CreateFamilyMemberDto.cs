using System.ComponentModel.DataAnnotations;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.DTOs.Patient
{
    public class CreateFamilyMemberDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public RelationType Relation { get; set; }

        [Required]
        public int Age { get; set; }

        [Required]
        public Gender Gender { get; set; }

        [MaxLength(50)]
        public string? BloodType { get; set; }

        [MaxLength(500)]
        public string? MedicalHistory { get; set; }

        [MaxLength(200)]
        public string? Allergies { get; set; }

        [MaxLength(200)]
        public string? ChronicDiseases { get; set; }
    }
}
