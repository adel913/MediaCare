using System.ComponentModel.DataAnnotations;
using MedicalApp.API.DTOs.Appointment;

namespace MedicalApp.API.DTOs.MedicalRecord
{
    public class CompleteConsultationDto
    {
        [Required(ErrorMessage = "Diagnosis is required")]
        [MaxLength(500)]
        public string Diagnosis { get; set; } = string.Empty;

        public List<PrescribedMedicationDto>? Medications { get; set; }

        [MaxLength(1000)]
        public string? Instructions { get; set; }
    }

    public class ConsultationPatientDto
    {
        public int? PatientId { get; set; }
        public int? FamilyMemberId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public int Age { get; set; }
        public string? Gender { get; set; }
        public string? BloodType { get; set; }
        public List<string> ChronicConditions { get; set; } = new();
        public List<string> Allergies { get; set; } = new();
        public bool IsFamilyMember { get; set; }
    }

    public class PreviousVisitDto
    {
        public int AppointmentId { get; set; }
        public DateTime VisitDate { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string? Diagnosis { get; set; }
        public string? ChiefComplaint { get; set; }
    }

    public class ConsultationScreenDto
    {
        public AppointmentDto Appointment { get; set; } = null!;
        public ConsultationPatientDto Patient { get; set; } = null!;
        public List<MedicalRecordDto> MedicalHistory { get; set; } = new();
        public List<PreviousVisitDto> PreviousVisits { get; set; } = new();
        public List<string> PreviousDiagnoses { get; set; } = new();
        public List<PrescribedMedicationDto> PreviousPrescriptions { get; set; } = new();
    }
}
