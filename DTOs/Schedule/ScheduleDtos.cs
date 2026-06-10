using System.ComponentModel.DataAnnotations;

namespace MedicalApp.API.DTOs.Schedule
{
    public class DoctorScheduleDto
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public string DayName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int SlotDurationMinutes { get; set; }
        public int MaxPatients { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateScheduleDto
    {
        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public int SlotDurationMinutes { get; set; } = 20;

        public int MaxPatients { get; set; } = 20;
    }

    public class AvailableSlotDto
    {
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public bool IsAvailable { get; set; }
    }
}
