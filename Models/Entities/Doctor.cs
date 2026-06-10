using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalApp.API.Models.Entities
{
    public class Doctor : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;



        [Required]
        [MaxLength(100)]
        public string Specialization { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? LicenseNumber { get; set; }

        [MaxLength(500)]
        public string? LicenseImageUrl { get; set; }

        public int YearsOfExperience { get; set; }

        [MaxLength(1000)]
        public string? Bio { get; set; }

        public decimal ConsultationFee { get; set; }

        public double AverageRating { get; set; } = 0;

        public int TotalReviews { get; set; } = 0;

        public bool IsAvailable { get; set; } = true;

        [MaxLength(100)]
        public string? Degree { get; set; }

        [MaxLength(100)]
        public string? University { get; set; }

        [MaxLength(100)]
        public string? SubSpecialty { get; set; }

        public int? GraduationYear { get; set; }

        [MaxLength(200)]
        public string? BoardCertification { get; set; }

        [MaxLength(200)]
        public string? Languages { get; set; }

        [MaxLength(100)]
        public string? QrCodeKey { get; set; }

        // Navigation properties
        public ICollection<DoctorClinic> DoctorClinics { get; set; } = new List<DoctorClinic>();
        public ICollection<DoctorSchedule> Schedules { get; set; } = new List<DoctorSchedule>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
