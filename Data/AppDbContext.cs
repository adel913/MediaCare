using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MediaCare.Models;

namespace MediaCare.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Doctor> Doctors { get; set; }
    public DbSet<Patient> Patients { get; set; }
    public DbSet<FamilyMember> FamilyMembers { get; set; }
    public DbSet<FavoriteDoctor> FavoriteDoctors { get; set; }
    public DbSet<Rating> Ratings { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<MedicalHistory> MedicalHistories { get; set; }
    public DbSet<Prescription> Prescriptions { get; set; }
    public DbSet<Consultation> Consultations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<AiChat> AiChats { get; set; }
    public DbSet<ClinicUser> ClinicUsers { get; set; }
    public DbSet<Clinic> Clinics { get; set; }
    public DbSet<ClinicHours> ClinicHours { get; set; }
    public DbSet<Provider> Providers { get; set; }
    public DbSet<ProviderSchedule> ProviderSchedules { get; set; }
    public DbSet<Slot> Slots { get; set; }
    public DbSet<ClinicAppointment> ClinicAppointments { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<QueueItem> QueueItems { get; set; }
    public DbSet<WalkIn> WalkIns { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<UserNotification> UserNotifications { get; set; }
    public DbSet<DoctorClinicLink> DoctorClinicLinks { get; set; }
    public DbSet<DoctorAvailability> DoctorAvailabilities { get; set; }
    public DbSet<Post> Posts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured) return;
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── User ─────────────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasConversion<string>();
        });

        // ── Doctor ───────────────────────────────────────────
        mb.Entity<Doctor>(e =>
        {
            e.HasOne(d => d.User)
             .WithOne(u => u.DoctorProfile)
             .HasForeignKey<Doctor>(d => d.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(d => d.ConsultationFee).HasColumnType("decimal(10,2)");
            e.Property(d => d.TotalRating).HasColumnType("decimal(10,2)");
            e.Property(d => d.AverageRating).HasColumnType("decimal(5,2)");
            e.HasIndex(d => d.QrCode).IsUnique();
        });

        // ── Patient ──────────────────────────────────────────
        mb.Entity<Patient>(e =>
        {
            e.HasOne(p => p.User)
             .WithOne(u => u.PatientProfile)
             .HasForeignKey<Patient>(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── FamilyMember ─────────────────────────────────────
        mb.Entity<FamilyMember>(e =>
        {
            e.HasOne(f => f.Patient)
             .WithMany(p => p.FamilyMembers)
             .HasForeignKey(f => f.PatientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── FavoriteDoctor ───────────────────────────────────
        mb.Entity<FavoriteDoctor>(e =>
        {
            e.HasIndex(f => new { f.UserId, f.DoctorId }).IsUnique();

            e.HasOne(f => f.User)
             .WithMany(u => u.FavoriteDoctors)
             .HasForeignKey(f => f.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(f => f.Doctor)
             .WithMany(d => d.FavoritedBy)
             .HasForeignKey(f => f.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Rating ───────────────────────────────────────────
        mb.Entity<Rating>(e =>
        {
            e.HasIndex(r => new { r.PatientId, r.AppointmentId }).IsUnique();

            e.HasOne(r => r.Doctor)
             .WithMany(d => d.Ratings)
             .HasForeignKey(r => r.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Patient)
             .WithMany()
             .HasForeignKey(r => r.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Appointment)
             .WithOne(a => a.Rating)
             .HasForeignKey<Rating>(r => r.AppointmentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Appointment ──────────────────────────────────────
        mb.Entity<Appointment>(e =>
        {
            e.Property(a => a.Status).HasConversion<string>();
            e.Property(a => a.PatientType).HasConversion<string>();

            e.HasOne(a => a.Patient)
             .WithMany(p => p.Appointments)
             .HasForeignKey(a => a.PatientId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);

            e.HasOne(a => a.FamilyMember)
             .WithMany(f => f.Appointments)
             .HasForeignKey(a => a.FamilyMemberId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);

            e.HasOne(a => a.Doctor)
             .WithMany(d => d.Appointments)
             .HasForeignKey(a => a.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Slot)
             .WithOne(s => s.Appointment)
             .HasForeignKey<Appointment>(a => a.SlotId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);
        });

        // ── MedicalHistory ───────────────────────────────────
        mb.Entity<MedicalHistory>(e =>
        {
            e.HasOne(h => h.Patient)
             .WithMany(p => p.MedicalHistories)
             .HasForeignKey(h => h.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(h => h.Doctor)
             .WithMany(d => d.MedicalHistories)
             .HasForeignKey(h => h.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Prescription ──────────────────────────────────────
        mb.Entity<Prescription>(e =>
        {
            e.HasOne(p => p.MedicalHistory)
             .WithOne(h => h.Prescription)
             .HasForeignKey<Prescription>(p => p.MedicalHistoryId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Doctor)
             .WithMany()
             .HasForeignKey(p => p.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Consultation ──────────────────────────────────────
        mb.Entity<Consultation>(e =>
        {
            e.HasOne(c => c.Patient)
             .WithMany(p => p.Consultations)
             .HasForeignKey(c => c.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(c => c.Doctor)
             .WithMany(d => d.Consultations)
             .HasForeignKey(c => c.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Message>(e =>
        {
            e.HasOne(m => m.Consultation)
             .WithMany(c => c.Messages)
             .HasForeignKey(m => m.ConsultationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.Sender)
             .WithMany()
             .HasForeignKey(m => m.SenderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AiChat ───────────────────────────────────────────
        mb.Entity<AiChat>(e =>
        {
            e.HasOne(a => a.User)
             .WithMany()
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Post ──────────────────────────────────────────────
        mb.Entity<Post>(e =>
        {
            e.Property(p => p.Category).HasConversion<string>();

            e.HasOne(p => p.Doctor)
             .WithMany(d => d.Posts)
             .HasForeignKey(p => p.DoctorId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── UserNotification ─────────────────────────────────
        mb.Entity<UserNotification>(e =>
        {
            e.Property(n => n.Trigger).HasConversion<string>();

            e.HasOne(n => n.User)
             .WithMany(u => u.Notifications)
             .HasForeignKey(n => n.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ClinicUser ───────────────────────────────────────
        mb.Entity<ClinicUser>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasConversion<string>();

            e.HasOne(u => u.Clinic)
             .WithMany(c => c.Staff)
             .HasForeignKey(u => u.ClinicId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Clinic>(e =>
        {
            e.HasMany(c => c.Hours)
             .WithOne(h => h.Clinic)
             .HasForeignKey(h => h.ClinicId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(c => c.Providers)
             .WithOne(p => p.Clinic)
             .HasForeignKey(p => p.ClinicId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── DoctorClinicLink ──────────────────────────────────
        mb.Entity<DoctorClinicLink>(e =>
        {
            e.HasIndex(l => new { l.DoctorId, l.ClinicId }).IsUnique();
            e.Property(l => l.ConsultationFee).HasColumnType("decimal(10,2)");

            e.HasOne(l => l.Doctor)
             .WithMany(d => d.ClinicLinks)
             .HasForeignKey(l => l.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(l => l.Clinic)
             .WithMany(c => c.DoctorLinks)
             .HasForeignKey(l => l.ClinicId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<DoctorAvailability>(e =>
        {
            e.Property(a => a.Status).HasConversion<string>();

            e.HasOne(a => a.Doctor)
             .WithMany(d => d.Availabilities)
             .HasForeignKey(a => a.DoctorId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Clinic)
             .WithMany()
             .HasForeignKey(a => a.ClinicId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);
        });

        mb.Entity<Provider>(e =>
        {
            e.Property(p => p.ConsultationFee).HasColumnType("decimal(10,2)");

            e.HasOne(p => p.Schedule)
             .WithOne(s => s.Provider)
             .HasForeignKey<ProviderSchedule>(s => s.ProviderId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Doctor)
             .WithMany()
             .HasForeignKey(p => p.DoctorId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);
        });

        mb.Entity<Slot>(e =>
        {
            e.Property(s => s.Status).HasConversion<string>();

            e.HasOne(s => s.Provider)
             .WithMany(p => p.Slots)
             .HasForeignKey(s => s.ProviderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<ClinicAppointment>(e =>
        {
            e.Property(a => a.Status).HasConversion<string>();
            e.Property(a => a.Type).HasConversion<string>();
            e.Property(a => a.PaymentStatus).HasConversion<string>();
            e.Property(a => a.Fee).HasColumnType("decimal(10,2)");

            e.HasOne(a => a.Provider)
             .WithMany(p => p.Appointments)
             .HasForeignKey(a => a.ProviderId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Patient)
             .WithMany(p => p.ClinicAppointments)
             .HasForeignKey(a => a.PatientId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);

            e.HasOne(a => a.Slot)
             .WithOne(s => s.ClinicAppointment)
             .HasForeignKey<ClinicAppointment>(a => a.SlotId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);
        });

        mb.Entity<Payment>(e =>
        {
            e.Property(p => p.Status).HasConversion<string>();
            e.Property(p => p.Amount).HasColumnType("decimal(10,2)");

            e.HasOne(p => p.Appointment)
             .WithOne(a => a.Payment)
             .HasForeignKey<Payment>(p => p.AppointmentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<QueueItem>(e =>
        {
            e.Property(q => q.Status).HasConversion<string>();
            e.Property(q => q.Type).HasConversion<string>();

            e.HasOne(q => q.Provider)
             .WithMany(p => p.QueueItems)
             .HasForeignKey(q => q.ProviderId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(q => q.Appointment)
             .WithOne(a => a.QueueItem)
             .HasForeignKey<QueueItem>(q => q.AppointmentId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);
        });

        mb.Entity<WalkIn>(e =>
        {
            e.HasOne(w => w.Provider)
             .WithMany()
             .HasForeignKey(w => w.ProviderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Notification>(e =>
        {
            e.HasOne(n => n.ClinicUser)
             .WithMany(u => u.Notifications)
             .HasForeignKey(n => n.ClinicUserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
