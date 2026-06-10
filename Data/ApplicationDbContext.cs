using Microsoft.EntityFrameworkCore;
using MedicalApp.API.Models.Entities;

namespace MedicalApp.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Clinic> Clinics { get; set; }
        public DbSet<ClinicAdmin> ClinicAdmins { get; set; }
        public DbSet<DoctorSchedule> DoctorSchedules { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<MedicalRecord> MedicalRecords { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<OtpCode> OtpCodes { get; set; }
        public DbSet<DoctorClinic> DoctorClinics { get; set; }
        public DbSet<TelegramMapping> TelegramMappings { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PatientFavorite> PatientFavorites { get; set; }
        public DbSet<CommunityPost> CommunityPosts { get; set; }
        public DbSet<CommunityComment> CommunityComments { get; set; }
        public DbSet<FamilyMember> FamilyMembers { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== User Configuration =====
            modelBuilder.Entity<User>(entity =>
            {
                // Phone is the primary login identifier
                entity.HasIndex(u => u.PhoneNumber).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
                entity.HasQueryFilter(u => !u.IsDeleted);
            });

            // ===== Patient Configuration =====
            modelBuilder.Entity<Patient>(entity =>
            {
                entity.HasOne(p => p.User)
                      .WithOne(u => u.Patient)
                      .HasForeignKey<Patient>(p => p.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasQueryFilter(p => !p.IsDeleted);
            });

            // ===== Family Member Configuration =====
            modelBuilder.Entity<FamilyMember>(entity =>
            {
                entity.HasOne(fm => fm.Patient)
                      .WithMany(p => p.FamilyMembers)
                      .HasForeignKey(fm => fm.PatientId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasQueryFilter(fm => !fm.IsDeleted);
            });

            // ===== Doctor Configuration =====
            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.HasOne(d => d.User)
                      .WithOne(u => u.Doctor)
                      .HasForeignKey<Doctor>(d => d.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasQueryFilter(d => !d.IsDeleted);

                entity.Property(d => d.ConsultationFee)
                      .HasColumnType("decimal(10,2)");
            });

            // ===== DoctorClinic Configuration =====
            modelBuilder.Entity<DoctorClinic>(entity =>
            {
                entity.HasIndex(dc => new { dc.DoctorId, dc.ClinicId }).IsUnique();

                entity.HasOne(dc => dc.Doctor)
                      .WithMany(d => d.DoctorClinics)
                      .HasForeignKey(dc => dc.DoctorId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(dc => dc.Clinic)
                      .WithMany(c => c.DoctorClinics)
                      .HasForeignKey(dc => dc.ClinicId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(dc => !dc.IsDeleted);

                entity.Property(dc => dc.ConsultationFee)
                      .HasColumnType("decimal(10,2)");
            });

            // ===== ClinicAdmin Configuration =====
            modelBuilder.Entity<ClinicAdmin>(entity =>
            {
                entity.HasOne(ca => ca.User)
                      .WithOne(u => u.ClinicAdmin)
                      .HasForeignKey<ClinicAdmin>(ca => ca.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ca => ca.Clinic)
                      .WithMany(c => c.Admins)
                      .HasForeignKey(ca => ca.ClinicId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasQueryFilter(ca => !ca.IsDeleted);
            });

            // ===== Clinic Configuration =====
            modelBuilder.Entity<Clinic>(entity =>
            {
                entity.HasQueryFilter(c => !c.IsDeleted);
                entity.HasIndex(c => c.Government);
                entity.HasIndex(c => c.Area);
            });

            // ===== DoctorSchedule Configuration =====
            modelBuilder.Entity<DoctorSchedule>(entity =>
            {
                entity.HasOne(ds => ds.Doctor)
                      .WithMany(d => d.Schedules)
                      .HasForeignKey(ds => ds.DoctorId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ds => ds.Clinic)
                      .WithMany()
                      .HasForeignKey(ds => ds.ClinicId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasQueryFilter(ds => !ds.IsDeleted);
            });

            // ===== Appointment Configuration =====
            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.HasOne(a => a.Patient)
                      .WithMany(p => p.Appointments)
                      .HasForeignKey(a => a.PatientId)
                      .OnDelete(DeleteBehavior.NoAction); // Changed from SetNull: breaks cascade cycle (Patient→Appointment & Patient→FamilyMember→Appointment)

                entity.HasOne(a => a.FamilyMember)
                      .WithMany(fm => fm.Appointments)
                      .HasForeignKey(a => a.FamilyMemberId)
                      .OnDelete(DeleteBehavior.NoAction); // Changed from SetNull: breaks cascade cycle; soft-delete (IsDeleted) handles cleanup

                entity.HasOne(a => a.Doctor)
                      .WithMany(d => d.Appointments)
                      .HasForeignKey(a => a.DoctorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasQueryFilter(a => !a.IsDeleted);

                entity.HasIndex(a => new { a.DoctorId, a.AppointmentDate });
                entity.HasIndex(a => new { a.PatientId, a.AppointmentDate });
                entity.HasIndex(a => new { a.DoctorId, a.AppointmentDate, a.StartTime })
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0 AND [Status] <> 4");

                entity.Property(a => a.ConsultationFee)
                      .HasColumnType("decimal(10,2)");
            });

            // ===== MedicalRecord Configuration =====
            modelBuilder.Entity<MedicalRecord>(entity =>
            {
                entity.HasOne(mr => mr.Patient)
                      .WithMany(p => p.MedicalRecords)
                      .HasForeignKey(mr => mr.PatientId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(mr => mr.FamilyMember)
                      .WithMany()
                      .HasForeignKey(mr => mr.FamilyMemberId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(mr => mr.Doctor)
                      .WithMany(d => d.MedicalRecords)
                      .HasForeignKey(mr => mr.DoctorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(mr => mr.Appointment)
                      .WithOne(a => a.MedicalRecord)
                      .HasForeignKey<MedicalRecord>(mr => mr.AppointmentId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasQueryFilter(mr => !mr.IsDeleted);
            });

            // ===== Review Configuration =====
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasOne(r => r.Patient)
                      .WithMany(p => p.Reviews)
                      .HasForeignKey(r => r.PatientId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Doctor)
                      .WithMany(d => d.Reviews)
                      .HasForeignKey(r => r.DoctorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Appointment)
                      .WithOne(a => a.Review)
                      .HasForeignKey<Review>(r => r.AppointmentId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasQueryFilter(r => !r.IsDeleted);
            });

            // ===== OtpCode Configuration =====
            modelBuilder.Entity<OtpCode>(entity =>
            {
                entity.HasIndex(o => new { o.PhoneNumber, o.Code });
                entity.HasQueryFilter(o => !o.IsDeleted);
            });

            // ===== TelegramMapping Configuration =====
            modelBuilder.Entity<TelegramMapping>(entity =>
            {
                entity.HasIndex(t => t.PhoneNumber).IsUnique();
                entity.HasQueryFilter(t => !t.IsDeleted);
            });

            // ===== Notification Configuration =====
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasQueryFilter(n => !n.IsDeleted);
                entity.HasOne(n => n.User)
                      .WithMany()
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== PatientFavorite Configuration =====
            modelBuilder.Entity<PatientFavorite>(entity =>
            {
                entity.HasIndex(pf => new { pf.PatientId, pf.DoctorId }).IsUnique();
                entity.HasQueryFilter(pf => !pf.IsDeleted);

                entity.HasOne(pf => pf.Patient)
                      .WithMany()
                      .HasForeignKey(pf => pf.PatientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pf => pf.Doctor)
                      .WithMany()
                      .HasForeignKey(pf => pf.DoctorId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== RefreshToken Configuration =====
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasOne(rt => rt.User)
                      .WithMany(u => u.RefreshTokens)
                      .HasForeignKey(rt => rt.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(rt => rt.Token).IsUnique();
                entity.HasQueryFilter(rt => !rt.IsDeleted);
            });

            // ===== CommunityPost Configuration =====
            modelBuilder.Entity<CommunityPost>(entity =>
            {
                entity.HasQueryFilter(cp => !cp.IsDeleted);

                entity.HasOne(cp => cp.User)
                      .WithMany()
                      .HasForeignKey(cp => cp.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ===== CommunityComment Configuration =====
            modelBuilder.Entity<CommunityComment>(entity =>
            {
                entity.HasQueryFilter(cc => !cc.IsDeleted);

                entity.HasOne(cc => cc.Post)
                      .WithMany(cp => cp.Comments)
                      .HasForeignKey(cc => cc.PostId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cc => cc.User)
                      .WithMany()
                      .HasForeignKey(cc => cc.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }

        /// <summary>
        /// Auto-set UpdatedAt on save changes.
        /// </summary>
        public override int SaveChanges()
        {
            SetTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SetTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void SetTimestamps()
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
