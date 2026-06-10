using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.DTOs.Doctor
{
    public class DoctorProfileDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string Specialization { get; set; } = string.Empty;
        public string? LicenseNumber { get; set; }
        public string? LicenseImageUrl { get; set; }
        public int YearsOfExperience { get; set; }
        public string? Bio { get; set; }
        public decimal ConsultationFee { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public bool IsAvailable { get; set; }
        public int? ClinicId { get; set; }
        public string? ClinicName { get; set; }
        public double? ClinicLatitude { get; set; }
        public double? ClinicLongitude { get; set; }

        // New profile fields
        public string? Degree { get; set; }
        public string? University { get; set; }
        public string? SubSpecialty { get; set; }
        public int? GraduationYear { get; set; }
        public string? BoardCertification { get; set; }
        public List<string> Languages { get; set; } = new();
        public List<string> AssociatedClinics { get; set; } = new();
        public string? QrCodeKey { get; set; }

        // Distinct registered patients seen by this doctor (excludes cancelled + walk-ins).
        public int TotalPatients { get; set; }
    }

    public class UpdateDoctorProfileDto
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Specialization { get; set; }
        public int? YearsOfExperience { get; set; }
        public string? Bio { get; set; }
        public decimal? ConsultationFee { get; set; }
        public bool? IsAvailable { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? LicenseImageUrl { get; set; }

        // New profile fields
        public string? Degree { get; set; }
        public string? University { get; set; }
        public string? SubSpecialty { get; set; }
        public int? GraduationYear { get; set; }
        public string? BoardCertification { get; set; }
        public List<string>? Languages { get; set; }
    }

    public class DoctorListItemDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public decimal ConsultationFee { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public bool IsAvailable { get; set; }
        public string? ClinicName { get; set; }
        public string? ClinicArea { get; set; }
        public bool IsFavorited { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? DistanceKm { get; set; }
    }

    public class NearbyDoctorDto : DoctorListItemDto
    {
        public int? ClinicIdForLocation { get; set; }
    }

    public class ClinicDoctorDetailsDto
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public decimal ConsultationFee { get; set; }
        public bool IsAvailable { get; set; }
        public string? InternalNotes { get; set; }
        public List<DoctorScheduleDto> Schedules { get; set; } = new();
    }

    public class DoctorScheduleDto
    {
        public int Id { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public string StartTime { get; set; } = "09:00:00"; // hh:mm:ss
        public string EndTime { get; set; } = "17:00:00";
        public string? BreakStartTime { get; set; }
        public string? BreakEndTime { get; set; }
        public int SlotDurationMinutes { get; set; } = 20;
        public int MaxPatients { get; set; } = 20;
        public bool IsActive { get; set; } = true;
    }

    public class UpdateClinicDoctorDto
    {
        public int DoctorId { get; set; }
        public string? QrCodeKey { get; set; }
        public decimal ConsultationFee { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string? InternalNotes { get; set; }
        public List<DoctorScheduleDto> Schedules { get; set; } = new();
    }

    public class ScannedDoctorDto
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public int YearsOfExperience { get; set; }
        public string? Bio { get; set; }
        public decimal DefaultConsultationFee { get; set; }
        public bool IsAlreadyRegisteredInClinic { get; set; }
        public string? QrCodeKey { get; set; }
    }

    public class DoctorDashboardDto
    {
        public int TotalAppointments { get; set; }
        public int NewPatientsCount { get; set; }
        public int FollowUpsCount { get; set; }
        public int WalkInsCount { get; set; }
        public int OnlineCount { get; set; }
        public decimal TodayEarnings { get; set; }
        public int WaitingCount { get; set; }
        public int WithDoctorCount { get; set; }
        public int CompletedCount { get; set; }
    }

    public class PatientHistoryDto
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public int Age { get; set; }
        public string? Gender { get; set; }
        public string? BloodType { get; set; }
        public List<string> ChronicConditions { get; set; } = new();
        public List<string> CurrentMedications { get; set; } = new();
        public List<MedicalApp.API.DTOs.MedicalRecord.MedicalRecordDto> PastRecords { get; set; } = new();
    }
}
