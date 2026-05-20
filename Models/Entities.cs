using System.ComponentModel.DataAnnotations;

namespace MediaCare.Models;

// ─── Enums ───────────────────────────────────────────────────────────────────

public enum UserRole { Patient, Doctor, Admin }
public enum ClinicRole { Clinic, Nurse }
public enum SlotStatus { Available, Booked, Blocked, Pending }

/// <summary>Extended appointment statuses as per spec.</summary>
public enum AppointmentStatus
{
    Upcoming,
    Completed,
    Past,
    Cancelled,
    Waiting,
    InProgress,
    NoShow
}

public enum ClinicAppointmentStatus { Waiting, Arrived, WithDoctor, Completed, NoShow, Cancelled }
public enum AppointmentType { Online, WalkIn, FamilyMember }
public enum PatientType { User, FamilyMember, WalkIn }
public enum PaymentStatus { Pending, Paid }
public enum QueueStatus { Waiting, InProgress, Completed }
public enum QueueType { Online, WalkIn }
public enum DoctorAvailabilityStatus { Pending, Approved, Rejected }
public enum PostCategory { General, Tips, News, Research, Announcement }
public enum NotificationTrigger { AppointmentCreated, AppointmentUpdated, AppointmentCancelled }

// ─── Core User / Auth ─────────────────────────────────────────────────────────

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required, MaxLength(150)]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Patient;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Doctor? DoctorProfile { get; set; }
    public Patient? PatientProfile { get; set; }
    public ICollection<FavoriteDoctor> FavoriteDoctors { get; set; } = new List<FavoriteDoctor>();
    public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
}

// ─── Doctor ───────────────────────────────────────────────────────────────────

public class Doctor
{
    public int Id { get; set; }
    public int UserId { get; set; }

    [Required, MaxLength(100)]
    public string Specialization { get; set; } = string.Empty;

    /// <summary>Male / Female</summary>
    [MaxLength(10)]
    public string? Gender { get; set; }

    [MaxLength(500)]
    public string? Bio { get; set; }

    public decimal ConsultationFee { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string QrCode { get; set; } = Guid.NewGuid().ToString();

    // Location
    [MaxLength(100)]
    public string? Governorate { get; set; }
    [MaxLength(100)]
    public string? Area { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Rating / Stats
    public decimal TotalRating { get; set; } = 0;
    public int PatientsCount { get; set; } = 0;
    public decimal AverageRating { get; set; } = 0;

    public User User { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<MedicalHistory> MedicalHistories { get; set; } = new List<MedicalHistory>();
    public ICollection<Consultation> Consultations { get; set; } = new List<Consultation>();
    public ICollection<DoctorClinicLink> ClinicLinks { get; set; } = new List<DoctorClinicLink>();
    public ICollection<DoctorAvailability> Availabilities { get; set; } = new List<DoctorAvailability>();
    public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public ICollection<FavoriteDoctor> FavoritedBy { get; set; } = new List<FavoriteDoctor>();
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

// ─── Patient ──────────────────────────────────────────────────────────────────

public class Patient
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    [MaxLength(10)]
    public string? Gender { get; set; }
    [MaxLength(500)]
    public string? Allergies { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<MedicalHistory> MedicalHistories { get; set; } = new List<MedicalHistory>();
    public ICollection<Consultation> Consultations { get; set; } = new List<Consultation>();
    public ICollection<ClinicAppointment> ClinicAppointments { get; set; } = new List<ClinicAppointment>();
    public ICollection<FamilyMember> FamilyMembers { get; set; } = new List<FamilyMember>();
}

// ─── Family Member ────────────────────────────────────────────────────────────

public class FamilyMember
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    [MaxLength(10)]
    public string? Gender { get; set; }
    [MaxLength(20)]
    public string? Relation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Patient Patient { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

// ─── Favorites ────────────────────────────────────────────────────────────────

public class FavoriteDoctor
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int DoctorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
}

// ─── Rating ───────────────────────────────────────────────────────────────────

public class Rating
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public int PatientId { get; set; }
    public int AppointmentId { get; set; }
    public int Score { get; set; }  // 1-5
    [MaxLength(500)]
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Doctor Doctor { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public Appointment Appointment { get; set; } = null!;
}

// ─── Clinic Links ─────────────────────────────────────────────────────────────

public class DoctorClinicLink
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public int ClinicId { get; set; }
    public decimal ConsultationFee { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    public Doctor Doctor { get; set; } = null!;
    public Clinic Clinic { get; set; } = null!;
}

// ─── Doctor Availability ──────────────────────────────────────────────────────

public class DoctorAvailability
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public int? ClinicId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int SlotDuration { get; set; } = 30;
    public DoctorAvailabilityStatus Status { get; set; } = DoctorAvailabilityStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Doctor Doctor { get; set; } = null!;
    public Clinic? Clinic { get; set; }
}

// ─── Appointment ──────────────────────────────────────────────────────────────

public class Appointment
{
    public int Id { get; set; }
    public int? PatientId { get; set; }       // null for WalkIn
    public int? FamilyMemberId { get; set; }  // for FamilyMember type
    public int DoctorId { get; set; }
    public int? SlotId { get; set; }

    public PatientType PatientType { get; set; } = PatientType.User;

    // WalkIn patient details (used when PatientType = WalkIn)
    [MaxLength(100)]
    public string? WalkInName { get; set; }
    [MaxLength(20)]
    public string? WalkInPhone { get; set; }
    public int? WalkInAge { get; set; }

    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Upcoming;
    [MaxLength(500)]
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Patient? Patient { get; set; }
    public FamilyMember? FamilyMember { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public Slot? Slot { get; set; }
    public Rating? Rating { get; set; }
}

// ─── Medical History (replaces MedicalRecord) ─────────────────────────────────

public class MedicalHistory
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public int? AppointmentId { get; set; }

    // Basic health questions
    [MaxLength(500)]
    public string? ChiefComplaint { get; set; }
    [MaxLength(200)]
    public string? ChronicDiseases { get; set; }
    [MaxLength(500)]
    public string? CurrentMedications { get; set; }
    [MaxLength(200)]
    public string? Allergies { get; set; }
    [MaxLength(200)]
    public string? PreviousSurgeries { get; set; }
    [MaxLength(200)]
    public string? FamilyHistory { get; set; }
    [MaxLength(10)]
    public string? BloodType { get; set; }
    public bool? IsSmoker { get; set; }
    [MaxLength(200)]
    public string? Notes { get; set; }

    public DateTime RecordDate { get; set; } = DateTime.UtcNow;

    public Patient Patient { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
    public Prescription? Prescription { get; set; }
}

// ─── Prescription ─────────────────────────────────────────────────────────────

public class Prescription
{
    public int Id { get; set; }
    public int MedicalHistoryId { get; set; }
    public int DoctorId { get; set; }

    [Required]
    public string Medications { get; set; } = string.Empty; // JSON or free text list
    [MaxLength(1000)]
    public string? Instructions { get; set; }
    [MaxLength(200)]
    public string? Diagnosis { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public MedicalHistory MedicalHistory { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
}

// ─── Consultation / AI Chat ───────────────────────────────────────────────────

public class Consultation
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Patient Patient { get; set; } = null!;
    public Doctor Doctor { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

public class Message
{
    public int Id { get; set; }
    public int ConsultationId { get; set; }
    public int SenderId { get; set; }
    [Required]
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public Consultation Consultation { get; set; } = null!;
    public User Sender { get; set; } = null!;
}

/// <summary>AI Chatbot conversation (patient ↔ AI).</summary>
public class AiChat
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required]
    public string UserMessage { get; set; } = string.Empty;
    [Required]
    public string AiReply { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

// ─── Clinic ───────────────────────────────────────────────────────────────────

public class ClinicUser
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required, MaxLength(150)]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    public ClinicRole Role { get; set; } = ClinicRole.Nurse;
    public int ClinicId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Clinic Clinic { get; set; } = null!;
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

public class Clinic
{
    public int Id { get; set; }
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? Address { get; set; }
    [MaxLength(20)]
    public string? Phone { get; set; }
    [MaxLength(150)]
    public string? Email { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }

    public ICollection<ClinicHours> Hours { get; set; } = new List<ClinicHours>();
    public ICollection<Provider> Providers { get; set; } = new List<Provider>();
    public ICollection<ClinicUser> Staff { get; set; } = new List<ClinicUser>();
    public ICollection<DoctorClinicLink> DoctorLinks { get; set; } = new List<DoctorClinicLink>();
}

public class ClinicHours
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public bool IsClosed { get; set; } = false;
    public Clinic Clinic { get; set; } = null!;
}

// ─── Provider / Slot ──────────────────────────────────────────────────────────

public class Provider
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public int? DoctorId { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? Specialization { get; set; }
    [MaxLength(20)]
    public string? Phone { get; set; }
    public decimal ConsultationFee { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Clinic Clinic { get; set; } = null!;
    public Doctor? Doctor { get; set; }
    public ProviderSchedule? Schedule { get; set; }
    public ICollection<Slot> Slots { get; set; } = new List<Slot>();
    public ICollection<ClinicAppointment> Appointments { get; set; } = new List<ClinicAppointment>();
    public ICollection<QueueItem> QueueItems { get; set; } = new List<QueueItem>();
}

public class ProviderSchedule
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public int SlotDuration { get; set; } = 30;
    public TimeOnly ShiftStart { get; set; }
    public TimeOnly ShiftEnd { get; set; }
    public TimeOnly? BreakStart { get; set; }
    public TimeOnly? BreakEnd { get; set; }
    public int MaxPatientsPerDay { get; set; } = 20;
    public string WorkingDays { get; set; } = "1,2,3,4,5";
    public Provider Provider { get; set; } = null!;
}

public class Slot
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public SlotStatus Status { get; set; } = SlotStatus.Pending;

    public Provider Provider { get; set; } = null!;
    public ClinicAppointment? ClinicAppointment { get; set; }
    public Appointment? Appointment { get; set; }
}

public class ClinicAppointment
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public int? PatientId { get; set; }
    public int? SlotId { get; set; }
    public AppointmentType Type { get; set; } = AppointmentType.Online;
    public ClinicAppointmentStatus Status { get; set; } = ClinicAppointmentStatus.Waiting;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public decimal? Fee { get; set; }
    [MaxLength(500)]
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Provider Provider { get; set; } = null!;
    public Patient? Patient { get; set; }
    public Slot? Slot { get; set; }
    public QueueItem? QueueItem { get; set; }
    public Payment? Payment { get; set; }
}

// ─── Payment / Queue ──────────────────────────────────────────────────────────

public class Payment
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ClinicAppointment Appointment { get; set; } = null!;
}

public class QueueItem
{
    public int Id { get; set; }
    public int QueueNumber { get; set; }
    public int ProviderId { get; set; }
    public int? AppointmentId { get; set; }
    [Required, MaxLength(100)]
    public string PatientName { get; set; } = string.Empty;
    public QueueType Type { get; set; } = QueueType.Online;
    public QueueStatus Status { get; set; } = QueueStatus.Waiting;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Provider Provider { get; set; } = null!;
    public ClinicAppointment? Appointment { get; set; }
}

public class WalkIn
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public int? SlotId { get; set; }
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? Phone { get; set; }
    public int? Age { get; set; }
    [MaxLength(10)]
    public string? Gender { get; set; }
    [MaxLength(500)]
    public string? ChiefComplaint { get; set; }
    public bool IsNewPatient { get; set; } = true;
    public bool HasLabResults { get; set; } = false;
    public bool HasRadiology { get; set; } = false;
    public string? Attachments { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Provider Provider { get; set; } = null!;
}

// ─── Notifications ────────────────────────────────────────────────────────────

/// <summary>Clinic-staff notifications (original model).</summary>
public class Notification
{
    public int Id { get; set; }
    public int ClinicUserId { get; set; }
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Body { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ClinicUser ClinicUser { get; set; } = null!;
}

/// <summary>Patient/Doctor notifications with deep-link support.</summary>
public class UserNotification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Body { get; set; }
    public int? AppointmentId { get; set; }   // deep-link payload
    public NotificationTrigger? Trigger { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

// ─── Blog ─────────────────────────────────────────────────────────────────────

public class Post
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    [Required]
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public PostCategory Category { get; set; } = PostCategory.General;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Doctor Doctor { get; set; } = null!;
}
