namespace MedicalApp.API.Models.Enums
{
    public enum UserRole
    {
        Patient = 0,
        Doctor = 1,
        ClinicAdmin = 2
    }

    public enum Gender
    {
        Male = 0,
        Female = 1
    }

    public enum AppointmentStatus
    {
        Pending = 0,
        Confirmed = 1,
        InProgress = 2,
        Completed = 3,
        Cancelled = 4,
        NoShow = 5
    }

    public enum QueueStatus
    {
        Waiting = 0,
        InConsultation = 1,
        Completed = 2,
        Refunded = 3
    }

    public enum RefundStatus
    {
        None = 0,
        Pending = 1,
        Processed = 2
    }

    public enum DayOfWeekArabic
    {
        Sunday = 0,
        Monday = 1,
        Tuesday = 2,
        Wednesday = 3,
        Thursday = 4,
        Friday = 5,
        Saturday = 6
    }

    public enum PaymentMethod
    {
        Cash = 0,
        Online = 1
    }

    public enum PaymentStatus
    {
        Pending = 0,
        Paid = 1,
        Refunded = 2
    }
}
