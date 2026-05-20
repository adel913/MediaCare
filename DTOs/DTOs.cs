using MediaCare.Models;

namespace MediaCare.DTOs;

// ─── Generic wrapper ──────────────────────────────────────────────────────────

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message) =>
        new() { Success = false, Message = message };
}

// ─── Auth ─────────────────────────────────────────────────────────────────────

public class RegisterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Patient;

    // Doctor extra fields
    public string? Specialization { get; set; }
    public string? Gender { get; set; }
    public decimal? ConsultationFee { get; set; }
    public string? Bio { get; set; }

    // Patient extra fields
    public DateTime? DateOfBirth { get; set; }
    public string? PatientGender { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class UserProfileResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Doctor fields
    public string? Specialization { get; set; }
    public string? Gender { get; set; }
    public decimal? ConsultationFee { get; set; }
    public string? Bio { get; set; }
    public bool? IsAvailable { get; set; }

    // Patient fields
    public DateTime? DateOfBirth { get; set; }
    public string? PatientGender { get; set; }
    public string? Allergies { get; set; }
}

// ─── Doctor ───────────────────────────────────────────────────────────────────

public class DoctorResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? Bio { get; set; }
    public decimal ConsultationFee { get; set; }
    public bool IsAvailable { get; set; }
    public string? Governorate { get; set; }
    public string? Area { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public decimal AverageRating { get; set; }
    public int PatientsCount { get; set; }
}

public class UpdateDoctorRequest
{
    public string? Specialization { get; set; }
    public string? Gender { get; set; }
    public decimal? ConsultationFee { get; set; }
    public string? Bio { get; set; }
    public bool? IsAvailable { get; set; }
    public string? Governorate { get; set; }
    public string? Area { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class DoctorQrResponse
{
    public string QrCode { get; set; } = string.Empty;
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
}

public class PopularDoctorFilterRequest
{
    public string? Governorate { get; set; }
    public string? Area { get; set; }
    public string? Specialization { get; set; }
    public decimal? MaxFee { get; set; }
    public string? Gender { get; set; }
    public DateTime? AvailableDate { get; set; }
}

// ─── Clinic Links ─────────────────────────────────────────────────────────────

public class LinkDoctorRequest
{
    public string QrCode { get; set; } = string.Empty;
    public decimal ConsultationFee { get; set; }
}

public class LinkedDoctorResponse
{
    public int DoctorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public decimal ConsultationFee { get; set; }
    public DateTime LinkedAt { get; set; }
}

// ─── Doctor Availability ──────────────────────────────────────────────────────

public class DoctorAvailabilityRequest
{
    public int? ClinicId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int SlotDuration { get; set; } = 30;
}

public class DoctorAvailabilityResponse
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public int? ClinicId { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int SlotDuration { get; set; }
    public string Status { get; set; } = string.Empty;
}

// ─── Appointments ─────────────────────────────────────────────────────────────

public class CreateAppointmentRequest
{
    public int DoctorId { get; set; }
    public int? SlotId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public string? Notes { get; set; }

    // Patient type
    public PatientType PatientType { get; set; } = PatientType.User;
    public int? FamilyMemberId { get; set; }

    // WalkIn
    public string? WalkInName { get; set; }
    public string? WalkInPhone { get; set; }
    public int? WalkInAge { get; set; }
}

public class AppointmentResponse
{
    public int Id { get; set; }
    public int? PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientType { get; set; } = string.Empty;
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateAppointmentStatusRequest
{
    public AppointmentStatus Status { get; set; }
}

public class AppointmentTrackingResponse
{
    public int AppointmentId { get; set; }
    public int YourNumber { get; set; }
    public int CurrentPatient { get; set; }
    public string AppointmentStatus { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
}

// ─── Rating ───────────────────────────────────────────────────────────────────

public class CreateRatingRequest
{
    public int AppointmentId { get; set; }
    public int Score { get; set; }  // 1-5
    public string? Comment { get; set; }
}

public class RatingResponse
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ─── Favorites ────────────────────────────────────────────────────────────────

public class FavoriteDoctorResponse
{
    public int DoctorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public decimal ConsultationFee { get; set; }
    public decimal AverageRating { get; set; }
    public DateTime AddedAt { get; set; }
}

// ─── Family Members ───────────────────────────────────────────────────────────

public class CreateFamilyMemberRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Relation { get; set; }
}

public class FamilyMemberResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Relation { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ─── Medical History ──────────────────────────────────────────────────────────

public class CreateMedicalHistoryRequest
{
    public int PatientId { get; set; }
    public int? AppointmentId { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? ChronicDiseases { get; set; }
    public string? CurrentMedications { get; set; }
    public string? Allergies { get; set; }
    public string? PreviousSurgeries { get; set; }
    public string? FamilyHistory { get; set; }
    public string? BloodType { get; set; }
    public bool? IsSmoker { get; set; }
    public string? Notes { get; set; }
}

public class MedicalHistoryResponse
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public int? AppointmentId { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? ChronicDiseases { get; set; }
    public string? CurrentMedications { get; set; }
    public string? Allergies { get; set; }
    public string? PreviousSurgeries { get; set; }
    public string? FamilyHistory { get; set; }
    public string? BloodType { get; set; }
    public bool? IsSmoker { get; set; }
    public string? Notes { get; set; }
    public DateTime RecordDate { get; set; }
    public PrescriptionResponse? Prescription { get; set; }
}

// ─── Prescription ─────────────────────────────────────────────────────────────

public class CreatePrescriptionRequest
{
    public int MedicalHistoryId { get; set; }
    public string Medications { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string? Diagnosis { get; set; }
}

public class UpdatePrescriptionRequest
{
    public string? Medications { get; set; }
    public string? Instructions { get; set; }
    public string? Diagnosis { get; set; }
}

public class PrescriptionResponse
{
    public int Id { get; set; }
    public int MedicalHistoryId { get; set; }
    public string Medications { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string? Diagnosis { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// ─── Consultation ─────────────────────────────────────────────────────────────

public class CreateConsultationRequest
{
    public int DoctorId { get; set; }
}

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

public class ConsultationResponse
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<MessageResponse> Messages { get; set; } = new();
}

public class MessageResponse
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

// ─── AI Chatbot ───────────────────────────────────────────────────────────────

public class AiChatRequest
{
    public string Message { get; set; } = string.Empty;
}

public class AiChatResponse
{
    public string UserMessage { get; set; } = string.Empty;
    public string AiReply { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ─── Blog / Posts ─────────────────────────────────────────────────────────────

public class CreatePostRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public PostCategory Category { get; set; } = PostCategory.General;
}

public class UpdatePostRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public PostCategory? Category { get; set; }
}

public class PostResponse
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// ─── Notifications ────────────────────────────────────────────────────────────

public class NotificationResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public int? AppointmentId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ─── Clinic Auth ──────────────────────────────────────────────────────────────

public class ClinicLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ClinicAuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int ClinicId { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class ClinicProfileResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
}

public class UpdateClinicProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
}

public class ClinicHoursResponse
{
    public int Id { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public string OpenTime { get; set; } = string.Empty;
    public string CloseTime { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
}

public class UpdateClinicHoursRequest
{
    public List<ClinicHourItem> Hours { get; set; } = new();
}

public class ClinicHourItem
{
    public DayOfWeek DayOfWeek { get; set; }
    public string OpenTime { get; set; } = string.Empty;
    public string CloseTime { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
}

// ─── Provider ─────────────────────────────────────────────────────────────────

public class ProviderRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string? Phone { get; set; }
    public decimal ConsultationFee { get; set; }
    public int? DoctorId { get; set; }
}

public class ProviderResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string? Phone { get; set; }
    public decimal ConsultationFee { get; set; }
    public bool IsActive { get; set; }
    public int? DoctorId { get; set; }
}

public class ProviderScheduleRequest
{
    public int SlotDuration { get; set; } = 30;
    public string ShiftStart { get; set; } = string.Empty;
    public string ShiftEnd { get; set; } = string.Empty;
    public string? BreakStart { get; set; }
    public string? BreakEnd { get; set; }
    public int MaxPatientsPerDay { get; set; } = 20;
    public string WorkingDays { get; set; } = "1,2,3,4,5";
}

public class ProviderScheduleResponse
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public int SlotDuration { get; set; }
    public string ShiftStart { get; set; } = string.Empty;
    public string ShiftEnd { get; set; } = string.Empty;
    public string? BreakStart { get; set; }
    public string? BreakEnd { get; set; }
    public int MaxPatientsPerDay { get; set; }
    public string WorkingDays { get; set; } = string.Empty;
}

public class SlotResponse
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class RegenerateSlotsRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

// ─── Clinic Appointments ──────────────────────────────────────────────────────

public class ClinicAppointmentResponse
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public int? PatientId { get; set; }
    public string? PatientName { get; set; }
    public int? SlotId { get; set; }
    public DateTime? SlotTime { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal? Fee { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateClinicAppointmentStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

// ─── Queue ────────────────────────────────────────────────────────────────────

public class QueueItemResponse
{
    public int Id { get; set; }
    public int QueueNumber { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public int? AppointmentId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class QueueOverviewResponse
{
    public List<QueueItemResponse> Waiting { get; set; } = new();
    public List<QueueItemResponse> InProgress { get; set; } = new();
    public List<QueueItemResponse> Completed { get; set; } = new();
}

public class UpdateQueueStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

// ─── Walk-In ──────────────────────────────────────────────────────────────────

public class WalkInRequest
{
    public int ProviderId { get; set; }
    public int? SlotId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int? Age { get; set; }
    public string? Gender { get; set; }
    public string? ChiefComplaint { get; set; }
    public bool IsNewPatient { get; set; } = true;
    public bool HasLabResults { get; set; } = false;
    public bool HasRadiology { get; set; } = false;
    public string? Attachments { get; set; }
}

public class WalkInResponse
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int? Age { get; set; }
    public string? Gender { get; set; }
    public string? ChiefComplaint { get; set; }
    public bool IsNewPatient { get; set; }
    public bool HasLabResults { get; set; }
    public bool HasRadiology { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ─── Payment ──────────────────────────────────────────────────────────────────

public class PaymentResponse
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentSummaryResponse
{
    public decimal TotalRevenue { get; set; }
    public int PaidCount { get; set; }
    public int PendingCount { get; set; }
    public decimal PendingAmount { get; set; }
}

// ─── Dashboard ────────────────────────────────────────────────────────────────

public class DashboardOverviewResponse
{
    public DateTime Date { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalAppointments { get; set; }
    public int WalkInCount { get; set; }
    public int OnlineAppointmentsCount { get; set; }
    public int PaidCount { get; set; }
    public int PendingCount { get; set; }
    public int WaitingCount { get; set; }
    public int WithDoctorCount { get; set; }
    public int CompletedCount { get; set; }
    public int NoShowCount { get; set; }
    public int CancelledCount { get; set; }
    public List<QueueItemResponse> LiveQueue { get; set; } = new();
}

public class RevenueResponse
{
    public DateTime Date { get; set; }
    public decimal TotalRevenue { get; set; }
    public int PaidCount { get; set; }
    public List<RevenueItem> ByProvider { get; set; } = new();
}

public class RevenueItem
{
    public int ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int PaidCount { get; set; }
}

public class AppointmentsStatsResponse
{
    public int Total { get; set; }
    public int Online { get; set; }
    public int WalkIn { get; set; }
    public int Waiting { get; set; }
    public int Arrived { get; set; }
    public int WithDoctor { get; set; }
    public int Completed { get; set; }
    public int NoShow { get; set; }
    public int Cancelled { get; set; }
}

public class DoctorDashboardOverviewResponse
{
    public DateTime Date { get; set; }
    public int TotalCases { get; set; }
    public int CompletedCases { get; set; }
    public int WaitingCount { get; set; }
    public decimal TodayEarnings { get; set; }
    public List<ClinicAppointmentResponse> PatientList { get; set; } = new();
}

public class DoctorEarningsResponse
{
    public decimal TotalEarnings { get; set; }
    public int CompletedAppointments { get; set; }
    public List<DoctorEarningsItem> ByDate { get; set; } = new();
}

public class DoctorEarningsItem
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class DoctorCasesResponse
{
    public int Total { get; set; }
    public int Completed { get; set; }
    public int NoShow { get; set; }
    public int Cancelled { get; set; }
    public List<ClinicAppointmentResponse> Cases { get; set; } = new();
}
