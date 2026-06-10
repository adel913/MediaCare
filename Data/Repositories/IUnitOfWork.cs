using MedicalApp.API.Models.Entities;

namespace MedicalApp.API.Data.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<User> Users { get; }
        IGenericRepository<Patient> Patients { get; }
        IGenericRepository<Doctor> Doctors { get; }
        IGenericRepository<Clinic> Clinics { get; }
        IGenericRepository<ClinicAdmin> ClinicAdmins { get; }
        IGenericRepository<DoctorSchedule> DoctorSchedules { get; }
        IGenericRepository<Appointment> Appointments { get; }
        IGenericRepository<MedicalRecord> MedicalRecords { get; }
        IGenericRepository<Review> Reviews { get; }
        IGenericRepository<OtpCode> OtpCodes { get; }
        IGenericRepository<TelegramMapping> TelegramMappings { get; }
        IGenericRepository<Notification> Notifications { get; }
        IGenericRepository<PatientFavorite> PatientFavorites { get; }
        IGenericRepository<DoctorClinic> DoctorClinics { get; }
        IGenericRepository<FamilyMember> FamilyMembers { get; }
        IGenericRepository<RefreshToken> RefreshTokens { get; }

        ApplicationDbContext Context { get; }

        Task<int> CompleteAsync();
        int Complete();
    }
}
