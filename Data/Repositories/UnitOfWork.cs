using MedicalApp.API.Models.Entities;

namespace MedicalApp.API.Data.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public IGenericRepository<User> Users { get; private set; }
        public IGenericRepository<Patient> Patients { get; private set; }
        public IGenericRepository<Doctor> Doctors { get; private set; }
        public IGenericRepository<Clinic> Clinics { get; private set; }
        public IGenericRepository<ClinicAdmin> ClinicAdmins { get; private set; }
        public IGenericRepository<DoctorSchedule> DoctorSchedules { get; private set; }
        public IGenericRepository<Appointment> Appointments { get; private set; }
        public IGenericRepository<MedicalRecord> MedicalRecords { get; private set; }
        public IGenericRepository<Review> Reviews { get; private set; }
        public IGenericRepository<OtpCode> OtpCodes { get; private set; }
        public IGenericRepository<TelegramMapping> TelegramMappings { get; private set; }
        public IGenericRepository<Notification> Notifications { get; private set; }
        public IGenericRepository<PatientFavorite> PatientFavorites { get; private set; }
        public IGenericRepository<DoctorClinic> DoctorClinics { get; private set; }
        public IGenericRepository<FamilyMember> FamilyMembers { get; private set; }
        public IGenericRepository<RefreshToken> RefreshTokens { get; private set; }

        public ApplicationDbContext Context => _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
            
            Users = new GenericRepository<User>(_context);
            Patients = new GenericRepository<Patient>(_context);
            Doctors = new GenericRepository<Doctor>(_context);
            Clinics = new GenericRepository<Clinic>(_context);
            ClinicAdmins = new GenericRepository<ClinicAdmin>(_context);
            DoctorSchedules = new GenericRepository<DoctorSchedule>(_context);
            Appointments = new GenericRepository<Appointment>(_context);
            MedicalRecords = new GenericRepository<MedicalRecord>(_context);
            Reviews = new GenericRepository<Review>(_context);
            OtpCodes = new GenericRepository<OtpCode>(_context);
            TelegramMappings = new GenericRepository<TelegramMapping>(_context);
            Notifications = new GenericRepository<Notification>(_context);
            PatientFavorites = new GenericRepository<PatientFavorite>(_context);
            DoctorClinics = new GenericRepository<DoctorClinic>(_context);
            FamilyMembers = new GenericRepository<FamilyMember>(_context);
            RefreshTokens = new GenericRepository<RefreshToken>(_context);
        }

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public int Complete()
        {
            return _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
