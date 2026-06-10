using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.DTOs.Patient
{
    public class FamilyMemberDto
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public RelationType Relation { get; set; }
        public int Age { get; set; }
        public Gender Gender { get; set; }
        public string? BloodType { get; set; }
        public string? MedicalHistory { get; set; }
        public string? Allergies { get; set; }
        public string? ChronicDiseases { get; set; }
    }
}
