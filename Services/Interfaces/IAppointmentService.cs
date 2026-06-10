using MedicalApp.API.DTOs.Appointment;
using MedicalApp.API.Helpers;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.Services.Interfaces
{
    public interface IAppointmentService
    {
        Task<ApiResponse<AppointmentDto>> CreateAppointmentAsync(int userId, CreateAppointmentDto dto);
        Task<ApiResponse<AppointmentDto>> CreateClinicAppointmentAsync(int userId, ClinicCreateAppointmentDto dto);
        Task<ApiResponse<List<AppointmentDto>>> GetPatientAppointmentsAsync(int userId, string? filter = null, AppointmentStatus? status = null);
        Task<ApiResponse<List<AppointmentDto>>> GetDoctorAppointmentsAsync(int userId, DateTime? date = null, AppointmentStatus? status = null);
        Task<ApiResponse<AppointmentDto>> GetAppointmentByIdAsync(int appointmentId, int userId);
        Task<ApiResponse<AppointmentDto>> CancelAppointmentAsync(int appointmentId, int userId, CancelAppointmentDto dto);
        Task<ApiResponse<AppointmentDto>> RescheduleAppointmentAsync(int appointmentId, int userId, RescheduleAppointmentDto dto);
        Task<ApiResponse<AppointmentDto>> UpdateStatusAsync(int appointmentId, int userId, UpdateAppointmentStatusDto dto);
        Task<ApiResponse<List<AppointmentDto>>> GetTodayQueueAsync(int userId);
        
        // Live Queue features
        Task<ApiResponse<LiveQueueTrackerDto>> GetLiveQueueTrackerAsync(int appointmentId, int userId);
        Task<ApiResponse<AppointmentDto>> CallNextPatientInQueueAsync(int doctorUserId);

        // Clinic Admin features
        Task<ApiResponse<ClinicDashboardOverviewDto>> GetClinicDashboardOverviewAsync(int clinicAdminUserId, int? doctorId);
        Task<ApiResponse<List<AppointmentDto>>> GetClinicTodayQueueAsync(int clinicAdminUserId, int doctorId);
        Task<ApiResponse<AppointmentDto>> StartCheckupAsync(int staffUserId, int appointmentId);
        Task<ApiResponse<PaymentsDashboardDto>> GetPaymentsDashboardAsync(int clinicAdminUserId, int? doctorId, string timeframe);
    }
}
