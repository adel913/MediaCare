using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalApp.API.Models.Entities
{
    /// <summary>
    /// Many-to-Many relationship between Doctor and Clinic.
    /// Represents a doctor's employment or registration at a specific clinic.
    /// </summary>
    public class DoctorClinic : BaseEntity
    {
        [Required]
        public int DoctorId { get; set; }

        [ForeignKey(nameof(DoctorId))]
        public Doctor Doctor { get; set; } = null!;

        [Required]
        public int ClinicId { get; set; }

        [ForeignKey(nameof(ClinicId))]
        public Clinic Clinic { get; set; } = null!;

        public DateTime JoinedDate { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public decimal? ConsultationFee { get; set; }

        public bool IsAvailable { get; set; } = true;

        [MaxLength(200)]
        public string? InternalNotes { get; set; }
    }
}
