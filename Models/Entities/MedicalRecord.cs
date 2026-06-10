using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.Models.Entities
{
    public class MedicalRecord : BaseEntity
    {
        [Required]
        public int PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public Patient Patient { get; set; } = null!;

        public int? FamilyMemberId { get; set; }

        [ForeignKey(nameof(FamilyMemberId))]
        public FamilyMember? FamilyMember { get; set; }

        [Required]
        public int DoctorId { get; set; }

        [ForeignKey(nameof(DoctorId))]
        public Doctor Doctor { get; set; } = null!;

        public int? AppointmentId { get; set; }

        [ForeignKey(nameof(AppointmentId))]
        public Appointment? Appointment { get; set; }

        [Required]
        [MaxLength(500)]
        public string Diagnosis { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Prescription { get; set; }

        [MaxLength(1000)]
        public string? Instructions { get; set; }

        [MaxLength(1000)]
        public string? TreatmentPlan { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [MaxLength(500)]
        public string? Symptoms { get; set; }

        // New Clinical & SOAP Fields mapped to Flutter UI
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

        public DateTime VisitDate { get; set; } = DateTime.UtcNow;
    }
}
