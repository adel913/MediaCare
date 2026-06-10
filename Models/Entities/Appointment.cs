using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.Models.Entities
{
    public class Appointment : BaseEntity
    {
        public int? PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public Patient? Patient { get; set; }

        public int? FamilyMemberId { get; set; }

        [ForeignKey(nameof(FamilyMemberId))]
        public FamilyMember? FamilyMember { get; set; }

        [Required]
        public int DoctorId { get; set; }

        [ForeignKey(nameof(DoctorId))]
        public Doctor Doctor { get; set; } = null!;

        [Required]
        public DateTime AppointmentDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        public TimeSpan? EndTime { get; set; }

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

        /// <summary>
        /// Queue number for the day (auto-assigned).
        /// </summary>
        public int? QueueNumber { get; set; }

        public QueueStatus? QueueStatus { get; set; }

        public RefundStatus RefundStatus { get; set; } = RefundStatus.None;

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(500)]
        public string? CancellationReason { get; set; }

        [MaxLength(100)]
        public string? OfflinePatientName { get; set; }

        [MaxLength(20)]
        public string? OfflinePatientPhone { get; set; }

        public bool IsEmergency { get; set; } = false;

        [MaxLength(500)]
        public string? ChiefComplaint { get; set; }

        public bool IsPaid { get; set; } = false;

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

        /// <summary>
        /// Consultation fee frozen at booking time. Revenue calculations must use this value.
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal ConsultationFee { get; set; }

        public int? OfflinePatientAge { get; set; }

        public Gender? OfflinePatientGender { get; set; }

        // Navigation properties
        public MedicalRecord? MedicalRecord { get; set; }
        public Review? Review { get; set; }
    }
}
