using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalApp.API.Models.Entities
{
    /// <summary>
    /// Doctor's weekly schedule defining available time slots.
    /// </summary>
    public class DoctorSchedule : BaseEntity
    {
        [Required]
        public int DoctorId { get; set; }

        [ForeignKey(nameof(DoctorId))]
        public Doctor Doctor { get; set; } = null!;

        [Required]
        public int ClinicId { get; set; }

        [ForeignKey(nameof(ClinicId))]
        public Clinic Clinic { get; set; } = null!;

        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        /// <summary>
        /// Slot duration in minutes (e.g., 15, 20, 30).
        /// </summary>
        public int SlotDurationMinutes { get; set; } = 20;

        /// <summary>
        /// Maximum number of patients in this schedule block.
        /// </summary>
        public int MaxPatients { get; set; } = 20;

        public TimeSpan? BreakStartTime { get; set; }

        public TimeSpan? BreakEndTime { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
