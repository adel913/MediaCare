using System.ComponentModel.DataAnnotations;

namespace MedicalApp.API.DTOs.Clinic
{
    public class ClinicDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? FacilityId { get; set; }
        public string? Description { get; set; }
        public string? Government { get; set; }
        public string? Area { get; set; }
        public string? Address { get; set; }
        public string? LinkMap { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? LogoUrl { get; set; }
        public string? LicenseImageUrl { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public TimeSpan? OpeningTime { get; set; }
        public TimeSpan? ClosingTime { get; set; }
        public bool IsActive { get; set; }
        public int DoctorsCount { get; set; }
    }

    public class CreateClinicDto
    {
        [Required(ErrorMessage = "Clinic name is required")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? FacilityId { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Government is required")]
        [MaxLength(100)]
        public string Government { get; set; } = string.Empty;

        [Required(ErrorMessage = "Area is required")]
        [MaxLength(100)]
        public string Area { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Address { get; set; }

        [MaxLength(500)]
        public string? LinkMap { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(100)]
        [EmailAddress]
        public string? Email { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public TimeSpan? OpeningTime { get; set; }
        public TimeSpan? ClosingTime { get; set; }
    }

    public class NearbyClinicDto : ClinicDto
    {
        public double DistanceKm { get; set; }
        public int MatchingDoctorsCount { get; set; }
    }

    public class UpdateClinicDto
    {
        [MaxLength(200)]
        public string? Name { get; set; }

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
        [EmailAddress]
        public string? Email { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public TimeSpan? OpeningTime { get; set; }
        public TimeSpan? ClosingTime { get; set; }
    }
}
