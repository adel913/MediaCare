using System.ComponentModel.DataAnnotations;

namespace MedicalApp.API.DTOs.MedicalRecord
{
    public class PrescribedMedicationDto
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
    }

    public class MedicalRecordDto
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string DoctorSpecialization { get; set; } = string.Empty;
        public string DoctorProfileImageUrl { get; set; } = string.Empty;
        public int? AppointmentId { get; set; }
        public string Diagnosis { get; set; } = string.Empty;
        public string? Prescription { get; set; }
        public string? Instructions { get; set; }
        public int? FamilyMemberId { get; set; }
        public string? FamilyMemberName { get; set; }
        public string? TreatmentPlan { get; set; }
        public string? Notes { get; set; }
        public string? Symptoms { get; set; }
        
        // SOAP & Vitals
        public string? Subjective { get; set; }
        public string? Objective { get; set; }
        public string? Assessment { get; set; }
        public string? Plan { get; set; }
        public string? BloodPressure { get; set; }
        public string? HeartRate { get; set; }
        public string? Weight { get; set; }

        // Mapped UI Fields
        public List<PrescribedMedicationDto>? Medications { get; set; }
        public string? Observations { get; set; }
        public List<string>? RecommendedCare { get; set; }

        public DateTime VisitDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateMedicalRecordDto
    {
        [Required(ErrorMessage = "Patient ID is required")]
        public int PatientId { get; set; }

        public int? AppointmentId { get; set; }

        [Required(ErrorMessage = "Diagnosis is required")]
        [MaxLength(500)]
        public string Diagnosis { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Prescription { get; set; }

        [MaxLength(1000)]
        public string? Instructions { get; set; }

        public int? FamilyMemberId { get; set; }

        [MaxLength(1000)]
        public string? TreatmentPlan { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [MaxLength(500)]
        public string? Symptoms { get; set; }

        // SOAP & Vitals from Doctor Input
        [MaxLength(1000)]
        public string? Subjective { get; set; }

        [MaxLength(1000)]
        public string? Objective { get; set; }

        [MaxLength(1000)]
        public string? Assessment { get; set; }

        [MaxLength(1000)]
        public string? Plan { get; set; }

        [MaxLength(50)]
        public string? BloodPressure { get; set; }

        [MaxLength(50)]
        public string? HeartRate { get; set; }

        [MaxLength(50)]
        public string? Weight { get; set; }

        // Mapped UI Fields from Doctor Input
        public List<PrescribedMedicationDto>? Medications { get; set; }
        public string? Observations { get; set; }
        public List<string>? RecommendedCare { get; set; }
    }
}
