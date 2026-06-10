using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalApp.API.Models.Entities
{
    /// <summary>
    /// Clinic entity representing a medical clinic.
    /// Updated to match Flutter UI: Government, Area, LinkMap fields.
    /// </summary>
    public class Clinic : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? FacilityId { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Government { get; set; }

        [MaxLength(100)]
        public string? Area { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }

        [MaxLength(500)]
        public string? LinkMap { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [MaxLength(500)]
        public string? LicenseImageUrl { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public TimeSpan? OpeningTime { get; set; }

        public TimeSpan? ClosingTime { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<DoctorClinic> DoctorClinics { get; set; } = new List<DoctorClinic>();
        public ICollection<ClinicAdmin> Admins { get; set; } = new List<ClinicAdmin>();
    }
}
