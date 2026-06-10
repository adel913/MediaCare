using System.ComponentModel.DataAnnotations;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.DTOs.Appointment
{
    public class CreateAppointmentDto
    {
        [Required(ErrorMessage = "Doctor ID is required")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Appointment date is required")]
        public DateTime AppointmentDate { get; set; }

        [Required(ErrorMessage = "Start time is required")]
        public TimeSpan StartTime { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public int? FamilyMemberId { get; set; }
    }

    public class ClinicCreateAppointmentDto
    {
        [Required(ErrorMessage = "Doctor ID is required")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Appointment date is required")]
        public DateTime AppointmentDate { get; set; }

        [Required(ErrorMessage = "Start time is required")]
        public TimeSpan StartTime { get; set; }

        public int? PatientId { get; set; }

        [MaxLength(100)]
        public string? OfflinePatientName { get; set; }

        [MaxLength(20)]
        public string? OfflinePatientPhone { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IsEmergency { get; set; } = false;

        [MaxLength(500)]
        public string? ChiefComplaint { get; set; }

        public bool IsPaid { get; set; } = false;

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

        public int? OfflinePatientAge { get; set; }

        public Gender? OfflinePatientGender { get; set; }
    }

    public class AppointmentDto
    {
        public int Id { get; set; }
        public int? PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public int? FamilyMemberId { get; set; }
        public string? FamilyMemberName { get; set; }
        public string? OfflinePatientPhone { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public AppointmentStatus Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public int? QueueNumber { get; set; }
        public QueueStatus? QueueStatus { get; set; }
        public RefundStatus RefundStatus { get; set; }
        public string RefundStatusText { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? CancellationReason { get; set; }
        public string? DoctorProfileImageUrl { get; set; }
        public int? ClinicId { get; set; }
        public string? ClinicName { get; set; }
        public string? ClinicAddress { get; set; }
        public int? CurrentServingNumber { get; set; }
        public bool IsEmergency { get; set; }
        public string? ChiefComplaint { get; set; }
        public bool IsPaid { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public string PaymentStatusText { get; set; } = string.Empty;
        public decimal ConsultationFee { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string PaymentMethodText { get; set; } = string.Empty;
        public int? OfflinePatientAge { get; set; }
        public Gender? OfflinePatientGender { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CancelAppointmentDto
    {
        [Required(ErrorMessage = "Cancellation reason is required")]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class UpdateAppointmentStatusDto
    {
        [Required]
        public AppointmentStatus Status { get; set; }
    }

    public class RescheduleAppointmentDto
    {
        [Required(ErrorMessage = "New appointment date is required")]
        public DateTime AppointmentDate { get; set; }

        [Required(ErrorMessage = "New start time is required")]
        public TimeSpan StartTime { get; set; }
    }

    // ===== Live Queue DTOs =====
    public class LiveQueueTrackerDto
    {
        public int AppointmentId { get; set; }
        public int MyQueueNumber { get; set; }
        public int CurrentServingNumber { get; set; }
        public int PatientsAheadOfMe { get; set; }
        public int EstimatedWaitTimeMinutes { get; set; }
        public QueueStatus? MyQueueStatus { get; set; }
        public string DoctorName { get; set; } = string.Empty;
    }

    public class ClinicDashboardOverviewDto
    {
        public int PaidCount { get; set; }
        public int WalkInCount { get; set; }
        public decimal TodayRevenueAmount { get; set; }
    }

    public class PaymentsDashboardDto
    {
        public decimal TotalRevenue { get; set; }
        public string RevenueGrowthText { get; set; } = "+12% vs last month";
        public decimal CashAmount { get; set; }
        public double CashPercentage { get; set; }
        public decimal OnlineAmount { get; set; }
        public double OnlinePercentage { get; set; }
        public decimal RefundsAmount { get; set; }
        public double RefundsPercentage { get; set; }
        public List<TransactionDto> RecentTransactions { get; set; } = new();
    }

    public class TransactionDto
    {
        public int AppointmentId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Paid"; // Paid, Pending, Refunded
        public PaymentMethod PaymentMethod { get; set; }
        public string PaymentMethodText { get; set; } = "Cash";
    }
}
