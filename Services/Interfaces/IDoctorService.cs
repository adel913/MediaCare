using MedicalApp.API.DTOs.Doctor;
using MedicalApp.API.DTOs.Schedule;
using MedicalApp.API.DTOs.MedicalRecord;
using MedicalApp.API.DTOs.Appointment;
using MedicalApp.API.Helpers;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.Services.Interfaces
{
    public interface IDoctorService
    {
        Task<ApiResponse<List<DoctorListItemDto>>> GetAllDoctorsAsync(
            string? specialization = null,
            string? search = null,
            string? government = null,
            string? area = null,
            string? appointmentDay = null,
            Gender? gender = null,
            decimal? minFee = null,
            decimal? maxFee = null,
            double? minRating = null,
            int? currentPatientUserId = null,
            double? userLat = null,
            double? userLng = null);
        Task<ApiResponse<List<DoctorListItemDto>>> GetPopularDoctorsAsync(int? currentPatientUserId = null, double? userLat = null, double? userLng = null);
        Task<ApiResponse<List<NearbyDoctorDto>>> GetNearbyDoctorsAsync(
            double lat,
            double lng,
            double radiusKm = 5,
            string? specialization = null,
            string? search = null);
        Task<ApiResponse<DoctorProfileDto>> GetDoctorByIdAsync(int doctorId);
        Task<ApiResponse<DoctorProfileDto>> GetProfileAsync(int userId);
        Task<ApiResponse<DoctorProfileDto>> UpdateProfileAsync(int userId, UpdateDoctorProfileDto dto);
        Task<ApiResponse<List<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>>> GetSchedulesAsync(int doctorId);
        Task<ApiResponse<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>> AddScheduleAsync(int clinicAdminUserId, int doctorId, CreateScheduleDto dto);
        Task<ApiResponse<bool>> DeleteScheduleAsync(int clinicAdminUserId, int scheduleId);
        Task<ApiResponse<List<AvailableSlotDto>>> GetAvailableSlotsAsync(int doctorId, DateTime date);
        Task<ApiResponse<List<string>>> GetSpecializationsAsync();
        Task<ApiResponse<DoctorDashboardDto>> GetDoctorDashboardAsync(int doctorUserId);
        Task<ApiResponse<List<AppointmentDto>>> GetDoctorLiveQueueAsync(int doctorUserId, string? filterStatus = null);
        Task<ApiResponse<PatientHistoryDto>> GetPatientHistoryAsync(int doctorUserId, int patientId);
        Task<ApiResponse<string>> GetDoctorQrCodeAsync(int doctorUserId);
        Task<ApiResponse<MedicalRecordDto>> SubmitConsultationSessionAsync(int doctorUserId, int appointmentId, CreateMedicalRecordDto dto);
        Task<ApiResponse<ConsultationScreenDto>> GetConsultationScreenAsync(int doctorUserId, int appointmentId);
        Task<ApiResponse<MedicalRecordDto>> CompleteConsultationAsync(int doctorUserId, int appointmentId, CompleteConsultationDto dto);
        Task<ApiResponse<List<AppointmentDto>>> GetActiveConsultationsAsync(int doctorUserId);
    }
}
