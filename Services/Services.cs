using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MediaCare.Data;
using MediaCare.DTOs;
using MediaCare.Models;

namespace MediaCare.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Auth Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<UserProfileResponse>> GetProfileAsync(int userId);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config) { _db = db; _config = config; }

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return ApiResponse<AuthResponse>.Fail("Email already registered.");

        var user = new User
        {
            Name = req.Name,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = req.Role
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        if (req.Role == UserRole.Doctor)
        {
            _db.Doctors.Add(new Doctor
            {
                UserId = user.Id,
                Specialization = req.Specialization ?? "General",
                Gender = req.Gender,
                ConsultationFee = req.ConsultationFee ?? 0,
                Bio = req.Bio
            });
        }
        else if (req.Role == UserRole.Patient)
        {
            _db.Patients.Add(new Patient
            {
                UserId = user.Id,
                DateOfBirth = req.DateOfBirth,
                Gender = req.PatientGender
            });
        }

        await _db.SaveChangesAsync();
        return ApiResponse<AuthResponse>.Ok(GenerateToken(user), "Registration successful.");
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return ApiResponse<AuthResponse>.Fail("Invalid email or password.");
        return ApiResponse<AuthResponse>.Ok(GenerateToken(user), "Login successful.");
    }

    public async Task<ApiResponse<UserProfileResponse>> GetProfileAsync(int userId)
    {
        var user = await _db.Users
            .Include(u => u.DoctorProfile)
            .Include(u => u.PatientProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return ApiResponse<UserProfileResponse>.Fail("User not found.");

        var profile = new UserProfileResponse
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            CreatedAt = user.CreatedAt
        };

        if (user.DoctorProfile is { } d)
        {
            profile.Specialization = d.Specialization;
            profile.Gender = d.Gender;
            profile.ConsultationFee = d.ConsultationFee;
            profile.Bio = d.Bio;
            profile.IsAvailable = d.IsAvailable;
        }

        if (user.PatientProfile is { } p)
        {
            profile.DateOfBirth = p.DateOfBirth;
            profile.PatientGender = p.Gender;
            profile.Allergies = p.Allergies;
        }

        return ApiResponse<UserProfileResponse>.Ok(profile);
    }

    private AuthResponse GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(double.Parse(_config["Jwt:ExpiresInHours"]!));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var jwt = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new AuthResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(jwt),
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            ExpiresAt = expires
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Doctor Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IDoctorService
{
    Task<ApiResponse<List<DoctorResponse>>> GetAllAsync();
    Task<ApiResponse<DoctorResponse>> GetByIdAsync(int id);
    Task<ApiResponse<DoctorQrResponse>> GetQrAsync(int userId);
    Task<ApiResponse<List<DoctorResponse>>> GetPopularAsync(PopularDoctorFilterRequest filter);
    Task<ApiResponse<bool>> UpdateDoctorAsync(int doctorUserId, UpdateDoctorRequest req);

    // Clinic-admin actions
    Task<ApiResponse<bool>> AdminUpdateDoctorAsync(int clinicId, int doctorId, UpdateDoctorRequest req);
    Task<ApiResponse<bool>> AdminUpdateDoctorFeeAsync(int clinicId, int doctorId, decimal fee);
}

public class DoctorService : IDoctorService
{
    private readonly AppDbContext _db;
    public DoctorService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<List<DoctorResponse>>> GetAllAsync()
    {
        var doctors = await _db.Doctors
            .Include(d => d.User)
            .Where(d => d.IsAvailable)
            .Select(d => MapDoctor(d))
            .ToListAsync();
        return ApiResponse<List<DoctorResponse>>.Ok(doctors);
    }

    public async Task<ApiResponse<DoctorResponse>> GetByIdAsync(int id)
    {
        var doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
        return doctor is null
            ? ApiResponse<DoctorResponse>.Fail("Doctor not found.")
            : ApiResponse<DoctorResponse>.Ok(MapDoctor(doctor));
    }

    public async Task<ApiResponse<DoctorQrResponse>> GetQrAsync(int userId)
    {
        var doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.UserId == userId);
        if (doctor is null) return ApiResponse<DoctorQrResponse>.Fail("Doctor not found.");
        return ApiResponse<DoctorQrResponse>.Ok(new DoctorQrResponse
        {
            QrCode = doctor.QrCode,
            DoctorId = doctor.Id,
            DoctorName = doctor.User.Name
        });
    }

    public async Task<ApiResponse<List<DoctorResponse>>> GetPopularAsync(PopularDoctorFilterRequest filter)
    {
        var query = _db.Doctors.Include(d => d.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Governorate))
            query = query.Where(d => d.Governorate == filter.Governorate);

        if (!string.IsNullOrWhiteSpace(filter.Area))
            query = query.Where(d => d.Area == filter.Area);

        if (!string.IsNullOrWhiteSpace(filter.Specialization))
            query = query.Where(d => d.Specialization == filter.Specialization);

        if (filter.MaxFee.HasValue)
            query = query.Where(d => d.ConsultationFee <= filter.MaxFee.Value);

        if (!string.IsNullOrWhiteSpace(filter.Gender))
            query = query.Where(d => d.Gender == filter.Gender);

        // Availability filter – check if the doctor has approved availabilities on the requested date
        if (filter.AvailableDate.HasValue)
        {
            var dayOfWeek = filter.AvailableDate.Value.DayOfWeek;
            var dateOnly = DateOnly.FromDateTime(filter.AvailableDate.Value);

            // Find doctors with approved availability on that day that still have open slots
            var doctorIdsWithSlots = await _db.Slots
                .Where(s => s.Status == SlotStatus.Available
                    && DateOnly.FromDateTime(s.StartTime) == dateOnly)
                .Select(s => s.Provider.DoctorId)
                .Where(id => id != null)
                .Distinct()
                .ToListAsync();

            var doctorIdsWithAvailability = await _db.DoctorAvailabilities
                .Where(a => a.Status == DoctorAvailabilityStatus.Approved
                    && a.DayOfWeek == dayOfWeek)
                .Select(a => (int?)a.DoctorId)
                .ToListAsync();

            var combined = doctorIdsWithSlots.Union(doctorIdsWithAvailability).ToList();
            query = query.Where(d => combined.Contains(d.Id));
        }

        // Sort descending by average rating
        var result = await query
            .OrderByDescending(d => d.AverageRating)
            .Select(d => MapDoctor(d))
            .ToListAsync();

        return ApiResponse<List<DoctorResponse>>.Ok(result);
    }

    public async Task<ApiResponse<bool>> UpdateDoctorAsync(int doctorUserId, UpdateDoctorRequest req)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<bool>.Fail("Doctor not found.");

        ApplyDoctorUpdate(doctor, req);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Profile updated.");
    }

    public async Task<ApiResponse<bool>> AdminUpdateDoctorAsync(int clinicId, int doctorId, UpdateDoctorRequest req)
    {
        // Ensure the doctor is linked to the clinic
        var link = await _db.DoctorClinicLinks
            .FirstOrDefaultAsync(l => l.ClinicId == clinicId && l.DoctorId == doctorId && l.IsActive);
        if (link is null) return ApiResponse<bool>.Fail("Doctor not linked to this clinic.");

        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId);
        if (doctor is null) return ApiResponse<bool>.Fail("Doctor not found.");

        ApplyDoctorUpdate(doctor, req);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Doctor updated by clinic admin.");
    }

    public async Task<ApiResponse<bool>> AdminUpdateDoctorFeeAsync(int clinicId, int doctorId, decimal fee)
    {
        var link = await _db.DoctorClinicLinks
            .FirstOrDefaultAsync(l => l.ClinicId == clinicId && l.DoctorId == doctorId && l.IsActive);
        if (link is null) return ApiResponse<bool>.Fail("Doctor not linked to this clinic.");

        link.ConsultationFee = fee;
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Consultation fee updated.");
    }

    private static void ApplyDoctorUpdate(Doctor doctor, UpdateDoctorRequest req)
    {
        if (req.Specialization is not null) doctor.Specialization = req.Specialization;
        if (req.Gender is not null) doctor.Gender = req.Gender;
        if (req.ConsultationFee.HasValue) doctor.ConsultationFee = req.ConsultationFee.Value;
        if (req.Bio is not null) doctor.Bio = req.Bio;
        if (req.IsAvailable.HasValue) doctor.IsAvailable = req.IsAvailable.Value;
        if (req.Governorate is not null) doctor.Governorate = req.Governorate;
        if (req.Area is not null) doctor.Area = req.Area;
        if (req.Latitude.HasValue) doctor.Latitude = req.Latitude;
        if (req.Longitude.HasValue) doctor.Longitude = req.Longitude;
    }

    internal static DoctorResponse MapDoctor(Doctor d) => new()
    {
        Id = d.Id,
        UserId = d.UserId,
        Name = d.User.Name,
        Email = d.User.Email,
        Specialization = d.Specialization,
        Gender = d.Gender,
        Bio = d.Bio,
        ConsultationFee = d.ConsultationFee,
        IsAvailable = d.IsAvailable,
        Governorate = d.Governorate,
        Area = d.Area,
        Latitude = d.Latitude,
        Longitude = d.Longitude,
        AverageRating = d.AverageRating,
        PatientsCount = d.PatientsCount
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Doctor Clinic Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IDoctorClinicService
{
    Task<ApiResponse<bool>> LinkDoctorAsync(int clinicId, LinkDoctorRequest req);
    Task<ApiResponse<List<LinkedDoctorResponse>>> GetLinkedDoctorsAsync(int clinicId);
}

public class DoctorClinicService : IDoctorClinicService
{
    private readonly AppDbContext _db;
    public DoctorClinicService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<bool>> LinkDoctorAsync(int clinicId, LinkDoctorRequest req)
    {
        var doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.QrCode == req.QrCode);
        if (doctor is null) return ApiResponse<bool>.Fail("Invalid QR code.");

        if (await _db.DoctorClinicLinks.AnyAsync(l => l.DoctorId == doctor.Id && l.ClinicId == clinicId))
            return ApiResponse<bool>.Fail("Doctor already linked to this clinic.");

        _db.DoctorClinicLinks.Add(new DoctorClinicLink
        {
            DoctorId = doctor.Id,
            ClinicId = clinicId,
            ConsultationFee = req.ConsultationFee
        });
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Doctor linked successfully.");
    }

    public async Task<ApiResponse<List<LinkedDoctorResponse>>> GetLinkedDoctorsAsync(int clinicId)
    {
        var links = await _db.DoctorClinicLinks
            .Include(l => l.Doctor).ThenInclude(d => d.User)
            .Where(l => l.ClinicId == clinicId && l.IsActive)
            .Select(l => new LinkedDoctorResponse
            {
                DoctorId = l.DoctorId,
                Name = l.Doctor.User.Name,
                Specialization = l.Doctor.Specialization,
                ConsultationFee = l.ConsultationFee,
                LinkedAt = l.LinkedAt
            })
            .ToListAsync();
        return ApiResponse<List<LinkedDoctorResponse>>.Ok(links);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Doctor Availability Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IDoctorAvailabilityService
{
    Task<ApiResponse<DoctorAvailabilityResponse>> CreateAsync(int doctorUserId, DoctorAvailabilityRequest req);
    Task<ApiResponse<List<DoctorAvailabilityResponse>>> GetAsync(int doctorUserId);
    Task<ApiResponse<DoctorAvailabilityResponse>> UpdateAsync(int doctorUserId, int id, DoctorAvailabilityRequest req);
    Task<ApiResponse<bool>> DeleteAsync(int doctorUserId, int id);
    Task<ApiResponse<DoctorAvailabilityResponse>> ApproveAsync(int clinicId, int id);

    // Clinic Admin – update doctor schedule
    Task<ApiResponse<DoctorAvailabilityResponse>> AdminUpdateAsync(int clinicId, int availabilityId, DoctorAvailabilityRequest req);
}

public class DoctorAvailabilityService : IDoctorAvailabilityService
{
    private readonly AppDbContext _db;
    public DoctorAvailabilityService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<DoctorAvailabilityResponse>> CreateAsync(int doctorUserId, DoctorAvailabilityRequest req)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<DoctorAvailabilityResponse>.Fail("Doctor not found.");

        var av = new DoctorAvailability
        {
            DoctorId = doctor.Id,
            ClinicId = req.ClinicId,
            DayOfWeek = req.DayOfWeek,
            StartTime = TimeOnly.Parse(req.StartTime),
            EndTime = TimeOnly.Parse(req.EndTime),
            SlotDuration = req.SlotDuration,
            Status = DoctorAvailabilityStatus.Pending
        };
        _db.DoctorAvailabilities.Add(av);
        await _db.SaveChangesAsync();
        return ApiResponse<DoctorAvailabilityResponse>.Ok(MapAv(av), "Availability submitted for approval.");
    }

    public async Task<ApiResponse<List<DoctorAvailabilityResponse>>> GetAsync(int doctorUserId)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<List<DoctorAvailabilityResponse>>.Fail("Doctor not found.");

        var list = await _db.DoctorAvailabilities
            .Where(a => a.DoctorId == doctor.Id)
            .Select(a => MapAv(a))
            .ToListAsync();
        return ApiResponse<List<DoctorAvailabilityResponse>>.Ok(list);
    }

    public async Task<ApiResponse<DoctorAvailabilityResponse>> UpdateAsync(int doctorUserId, int id, DoctorAvailabilityRequest req)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<DoctorAvailabilityResponse>.Fail("Doctor not found.");

        var av = await _db.DoctorAvailabilities.FirstOrDefaultAsync(a => a.Id == id && a.DoctorId == doctor.Id);
        if (av is null) return ApiResponse<DoctorAvailabilityResponse>.Fail("Availability not found.");

        ApplyAvUpdate(av, req);
        av.Status = DoctorAvailabilityStatus.Pending;
        await _db.SaveChangesAsync();
        return ApiResponse<DoctorAvailabilityResponse>.Ok(MapAv(av));
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int doctorUserId, int id)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<bool>.Fail("Doctor not found.");

        var av = await _db.DoctorAvailabilities.FirstOrDefaultAsync(a => a.Id == id && a.DoctorId == doctor.Id);
        if (av is null) return ApiResponse<bool>.Fail("Availability not found.");

        _db.DoctorAvailabilities.Remove(av);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Deleted.");
    }

    public async Task<ApiResponse<DoctorAvailabilityResponse>> ApproveAsync(int clinicId, int id)
    {
        var av = await _db.DoctorAvailabilities.FirstOrDefaultAsync(a => a.Id == id && a.ClinicId == clinicId);
        if (av is null) return ApiResponse<DoctorAvailabilityResponse>.Fail("Availability not found.");

        av.Status = DoctorAvailabilityStatus.Approved;
        await _db.SaveChangesAsync();
        return ApiResponse<DoctorAvailabilityResponse>.Ok(MapAv(av), "Approved.");
    }

    public async Task<ApiResponse<DoctorAvailabilityResponse>> AdminUpdateAsync(int clinicId, int availabilityId, DoctorAvailabilityRequest req)
    {
        var av = await _db.DoctorAvailabilities.FirstOrDefaultAsync(a => a.Id == availabilityId && a.ClinicId == clinicId);
        if (av is null) return ApiResponse<DoctorAvailabilityResponse>.Fail("Availability not found.");

        ApplyAvUpdate(av, req);
        await _db.SaveChangesAsync();
        return ApiResponse<DoctorAvailabilityResponse>.Ok(MapAv(av), "Schedule updated by clinic admin.");
    }

    private static void ApplyAvUpdate(DoctorAvailability av, DoctorAvailabilityRequest req)
    {
        av.DayOfWeek = req.DayOfWeek;
        av.StartTime = TimeOnly.Parse(req.StartTime);
        av.EndTime = TimeOnly.Parse(req.EndTime);
        av.SlotDuration = req.SlotDuration;
    }

    private static DoctorAvailabilityResponse MapAv(DoctorAvailability a) => new()
    {
        Id = a.Id,
        DoctorId = a.DoctorId,
        ClinicId = a.ClinicId,
        DayOfWeek = a.DayOfWeek.ToString(),
        StartTime = a.StartTime.ToString("HH:mm"),
        EndTime = a.EndTime.ToString("HH:mm"),
        SlotDuration = a.SlotDuration,
        Status = a.Status.ToString()
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Appointment Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IAppointmentService
{
    Task<ApiResponse<AppointmentResponse>> CreateAsync(int userId, CreateAppointmentRequest req);
    Task<ApiResponse<List<AppointmentResponse>>> GetForUserAsync(int userId, UserRole role);
    Task<ApiResponse<bool>> CancelAsync(int appointmentId, int userId, UserRole role);
    Task<ApiResponse<AppointmentResponse>> UpdateStatusAsync(int appointmentId, int doctorUserId, AppointmentStatus status);
    Task<ApiResponse<AppointmentTrackingResponse>> GetTrackingAsync(int appointmentId, int userId);
}

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext _db;
    private readonly IUserNotificationService _notifications;

    public AppointmentService(AppDbContext db, IUserNotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<ApiResponse<AppointmentResponse>> CreateAsync(int userId, CreateAppointmentRequest req)
    {
        // Validate patient
        Patient? patient = null;
        if (req.PatientType == PatientType.User || req.PatientType == PatientType.FamilyMember)
        {
            patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient is null) return ApiResponse<AppointmentResponse>.Fail("Patient profile not found.");
        }

        // Validate family member
        FamilyMember? familyMember = null;
        if (req.PatientType == PatientType.FamilyMember)
        {
            if (!req.FamilyMemberId.HasValue)
                return ApiResponse<AppointmentResponse>.Fail("FamilyMemberId is required.");
            familyMember = await _db.FamilyMembers.FirstOrDefaultAsync(f => f.Id == req.FamilyMemberId && f.PatientId == patient!.Id);
            if (familyMember is null) return ApiResponse<AppointmentResponse>.Fail("Family member not found.");
        }

        // Double-booking validation on slot
        if (req.SlotId.HasValue)
        {
            var slot = await _db.Slots.FirstOrDefaultAsync(s => s.Id == req.SlotId);
            if (slot is null) return ApiResponse<AppointmentResponse>.Fail("Slot not found.");
            if (slot.Status != SlotStatus.Available)
                return ApiResponse<AppointmentResponse>.Fail("This slot is already booked. Please choose another.");

            // Lock the slot
            slot.Status = SlotStatus.Booked;
        }

        var appointment = new Appointment
        {
            PatientId = patient?.Id,
            FamilyMemberId = familyMember?.Id,
            DoctorId = req.DoctorId,
            SlotId = req.SlotId,
            PatientType = req.PatientType,
            ScheduledAt = req.ScheduledAt,
            DurationMinutes = req.DurationMinutes,
            Notes = req.Notes,
            Status = AppointmentStatus.Upcoming,
            WalkInName = req.WalkInName,
            WalkInPhone = req.WalkInPhone,
            WalkInAge = req.WalkInAge
        };
        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        // Notify patient and doctor
        await _notifications.SendAsync(userId, "Appointment Confirmed",
            $"Your appointment has been booked for {req.ScheduledAt:dd MMM yyyy HH:mm}.",
            appointment.Id, NotificationTrigger.AppointmentCreated);

        var doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == req.DoctorId);
        if (doctor is not null)
            await _notifications.SendAsync(doctor.UserId, "New Appointment",
                $"A new appointment has been booked for {req.ScheduledAt:dd MMM yyyy HH:mm}.",
                appointment.Id, NotificationTrigger.AppointmentCreated);

        return ApiResponse<AppointmentResponse>.Ok(await MapAppointment(appointment), "Appointment created.");
    }

    public async Task<ApiResponse<List<AppointmentResponse>>> GetForUserAsync(int userId, UserRole role)
    {
        IQueryable<Appointment> query;

        if (role == UserRole.Doctor)
        {
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor is null) return ApiResponse<List<AppointmentResponse>>.Fail("Doctor not found.");
            query = _db.Appointments.Where(a => a.DoctorId == doctor.Id);
        }
        else
        {
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient is null) return ApiResponse<List<AppointmentResponse>>.Fail("Patient not found.");
            query = _db.Appointments.Where(a => a.PatientId == patient.Id);
        }

        var appointments = await query
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Patient).ThenInclude(p => p!.User)
            .Include(a => a.FamilyMember)
            .OrderByDescending(a => a.ScheduledAt)
            .ToListAsync();

        var result = appointments.Select(MapAppointmentSync).ToList();
        return ApiResponse<List<AppointmentResponse>>.Ok(result);
    }

    public async Task<ApiResponse<bool>> CancelAsync(int appointmentId, int userId, UserRole role)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment is null) return ApiResponse<bool>.Fail("Appointment not found.");

        // Authorization: patient cancels own, doctor cancels own
        if (role == UserRole.Patient)
        {
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient is null || appointment.PatientId != patient.Id)
                return ApiResponse<bool>.Fail("Unauthorized.");
        }
        else if (role == UserRole.Doctor)
        {
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor is null || appointment.DoctorId != doctor.Id)
                return ApiResponse<bool>.Fail("Unauthorized.");
        }

        appointment.Status = AppointmentStatus.Cancelled;

        // Free the slot
        if (appointment.Slot is not null)
            appointment.Slot.Status = SlotStatus.Available;

        await _db.SaveChangesAsync();

        // Notify both parties
        if (appointment.Patient is not null)
            await _db.UserNotifications.AddAsync(new UserNotification
            {
                UserId = appointment.Patient.UserId,
                Title = "Appointment Cancelled",
                Body = $"Your appointment on {appointment.ScheduledAt:dd MMM yyyy HH:mm} has been cancelled.",
                AppointmentId = appointment.Id,
                Trigger = NotificationTrigger.AppointmentCancelled
            });

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Appointment cancelled.");
    }

    public async Task<ApiResponse<AppointmentResponse>> UpdateStatusAsync(int appointmentId, int doctorUserId, AppointmentStatus status)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<AppointmentResponse>.Fail("Doctor not found.");

        var appointment = await _db.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Patient).ThenInclude(p => p!.User)
            .Include(a => a.FamilyMember)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.DoctorId == doctor.Id);

        if (appointment is null) return ApiResponse<AppointmentResponse>.Fail("Appointment not found.");

        appointment.Status = status;

        // When completed: increment PatientsCount on doctor
        if (status == AppointmentStatus.Completed)
        {
            doctor.PatientsCount++;
            doctor.AverageRating = doctor.PatientsCount > 0
                ? doctor.TotalRating / doctor.PatientsCount
                : 0;
        }

        await _db.SaveChangesAsync();

        // Notify patient
        if (appointment.Patient is not null)
            await _db.UserNotifications.AddAsync(new UserNotification
            {
                UserId = appointment.Patient.UserId,
                Title = "Appointment Updated",
                Body = $"Your appointment status changed to {status}.",
                AppointmentId = appointment.Id,
                Trigger = NotificationTrigger.AppointmentUpdated
            });

        await _db.SaveChangesAsync();
        return ApiResponse<AppointmentResponse>.Ok(MapAppointmentSync(appointment));
    }

    public async Task<ApiResponse<AppointmentTrackingResponse>> GetTrackingAsync(int appointmentId, int userId)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment is null) return ApiResponse<AppointmentTrackingResponse>.Fail("Appointment not found.");

        return ApiResponse<AppointmentTrackingResponse>.Ok(new AppointmentTrackingResponse
        {
            AppointmentId = appointment.Id,
            YourNumber = appointment.Id,
            CurrentPatient = 0,
            AppointmentStatus = appointment.Status.ToString(),
            ProviderName = appointment.Doctor.User.Name
        });
    }

    private async Task<AppointmentResponse> MapAppointment(Appointment a)
    {
        var doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == a.DoctorId);
        return new AppointmentResponse
        {
            Id = a.Id,
            PatientId = a.PatientId,
            PatientName = a.PatientType == PatientType.WalkIn ? (a.WalkInName ?? "Walk-in") : "",
            PatientType = a.PatientType.ToString(),
            DoctorId = a.DoctorId,
            DoctorName = doctor?.User.Name ?? "",
            Specialization = doctor?.Specialization ?? "",
            ScheduledAt = a.ScheduledAt,
            DurationMinutes = a.DurationMinutes,
            Status = a.Status.ToString(),
            Notes = a.Notes,
            CreatedAt = a.CreatedAt
        };
    }

    private static AppointmentResponse MapAppointmentSync(Appointment a) => new()
    {
        Id = a.Id,
        PatientId = a.PatientId,
        PatientName = a.PatientType == PatientType.WalkIn
            ? (a.WalkInName ?? "Walk-in")
            : (a.FamilyMember?.Name ?? a.Patient?.User?.Name ?? ""),
        PatientType = a.PatientType.ToString(),
        DoctorId = a.DoctorId,
        DoctorName = a.Doctor?.User?.Name ?? "",
        Specialization = a.Doctor?.Specialization ?? "",
        ScheduledAt = a.ScheduledAt,
        DurationMinutes = a.DurationMinutes,
        Status = a.Status.ToString(),
        Notes = a.Notes,
        CreatedAt = a.CreatedAt
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Rating Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IRatingService
{
    Task<ApiResponse<RatingResponse>> CreateAsync(int patientUserId, CreateRatingRequest req);
    Task<ApiResponse<List<RatingResponse>>> GetByDoctorAsync(int doctorId);
}

public class RatingService : IRatingService
{
    private readonly AppDbContext _db;
    public RatingService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<RatingResponse>> CreateAsync(int patientUserId, CreateRatingRequest req)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == patientUserId);
        if (patient is null) return ApiResponse<RatingResponse>.Fail("Patient not found.");

        // Check appointment is completed
        var appointment = await _db.Appointments.FirstOrDefaultAsync(a =>
            a.Id == req.AppointmentId && a.PatientId == patient.Id);

        if (appointment is null) return ApiResponse<RatingResponse>.Fail("Appointment not found.");
        if (appointment.Status != AppointmentStatus.Completed)
            return ApiResponse<RatingResponse>.Fail("You can only rate a completed appointment.");

        // Prevent duplicate rating
        if (await _db.Ratings.AnyAsync(r => r.AppointmentId == req.AppointmentId))
            return ApiResponse<RatingResponse>.Fail("You have already rated this appointment.");

        if (req.Score < 1 || req.Score > 5)
            return ApiResponse<RatingResponse>.Fail("Score must be between 1 and 5.");

        var rating = new Rating
        {
            DoctorId = appointment.DoctorId,
            PatientId = patient.Id,
            AppointmentId = req.AppointmentId,
            Score = req.Score,
            Comment = req.Comment
        };
        _db.Ratings.Add(rating);

        // Update doctor rating average
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.Id == appointment.DoctorId);
        if (doctor is not null)
        {
            doctor.TotalRating += req.Score;
            // PatientsCount is incremented when appointment status changes to Completed,
            // so we use it directly for the average calculation.
            doctor.AverageRating = doctor.PatientsCount > 0
                ? doctor.TotalRating / doctor.PatientsCount
                : req.Score;
        }

        await _db.SaveChangesAsync();

        var doc = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == rating.DoctorId);
        return ApiResponse<RatingResponse>.Ok(new RatingResponse
        {
            Id = rating.Id,
            DoctorId = rating.DoctorId,
            DoctorName = doc?.User.Name ?? "",
            Score = rating.Score,
            Comment = rating.Comment,
            CreatedAt = rating.CreatedAt
        }, "Rating submitted.");
    }

    public async Task<ApiResponse<List<RatingResponse>>> GetByDoctorAsync(int doctorId)
    {
        var ratings = await _db.Ratings
            .Include(r => r.Doctor).ThenInclude(d => d.User)
            .Where(r => r.DoctorId == doctorId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RatingResponse
            {
                Id = r.Id,
                DoctorId = r.DoctorId,
                DoctorName = r.Doctor.User.Name,
                Score = r.Score,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();
        return ApiResponse<List<RatingResponse>>.Ok(ratings);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Favorites Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IFavoriteService
{
    Task<ApiResponse<bool>> ToggleFavoriteAsync(int userId, int doctorId);
    Task<ApiResponse<List<FavoriteDoctorResponse>>> GetFavoritesAsync(int userId);
}

public class FavoriteService : IFavoriteService
{
    private readonly AppDbContext _db;
    public FavoriteService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<bool>> ToggleFavoriteAsync(int userId, int doctorId)
    {
        var existing = await _db.FavoriteDoctors
            .FirstOrDefaultAsync(f => f.UserId == userId && f.DoctorId == doctorId);

        if (existing is not null)
        {
            _db.FavoriteDoctors.Remove(existing);
            await _db.SaveChangesAsync();
            return ApiResponse<bool>.Ok(false, "Removed from favorites.");
        }

        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId);
        if (doctor is null) return ApiResponse<bool>.Fail("Doctor not found.");

        _db.FavoriteDoctors.Add(new FavoriteDoctor { UserId = userId, DoctorId = doctorId });
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Added to favorites.");
    }

    public async Task<ApiResponse<List<FavoriteDoctorResponse>>> GetFavoritesAsync(int userId)
    {
        var favorites = await _db.FavoriteDoctors
            .Include(f => f.Doctor).ThenInclude(d => d.User)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FavoriteDoctorResponse
            {
                DoctorId = f.DoctorId,
                Name = f.Doctor.User.Name,
                Specialization = f.Doctor.Specialization,
                ConsultationFee = f.Doctor.ConsultationFee,
                AverageRating = f.Doctor.AverageRating,
                AddedAt = f.CreatedAt
            })
            .ToListAsync();
        return ApiResponse<List<FavoriteDoctorResponse>>.Ok(favorites);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Family Member Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IFamilyMemberService
{
    Task<ApiResponse<FamilyMemberResponse>> CreateAsync(int patientUserId, CreateFamilyMemberRequest req);
    Task<ApiResponse<List<FamilyMemberResponse>>> GetAsync(int patientUserId);
    Task<ApiResponse<bool>> DeleteAsync(int patientUserId, int memberId);
}

public class FamilyMemberService : IFamilyMemberService
{
    private readonly AppDbContext _db;
    public FamilyMemberService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<FamilyMemberResponse>> CreateAsync(int patientUserId, CreateFamilyMemberRequest req)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == patientUserId);
        if (patient is null) return ApiResponse<FamilyMemberResponse>.Fail("Patient not found.");

        var member = new FamilyMember
        {
            PatientId = patient.Id,
            Name = req.Name,
            DateOfBirth = req.DateOfBirth,
            Gender = req.Gender,
            Relation = req.Relation
        };
        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync();
        return ApiResponse<FamilyMemberResponse>.Ok(MapMember(member), "Family member added.");
    }

    public async Task<ApiResponse<List<FamilyMemberResponse>>> GetAsync(int patientUserId)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == patientUserId);
        if (patient is null) return ApiResponse<List<FamilyMemberResponse>>.Fail("Patient not found.");

        var list = await _db.FamilyMembers
            .Where(f => f.PatientId == patient.Id)
            .Select(f => MapMember(f))
            .ToListAsync();
        return ApiResponse<List<FamilyMemberResponse>>.Ok(list);
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int patientUserId, int memberId)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == patientUserId);
        if (patient is null) return ApiResponse<bool>.Fail("Patient not found.");

        var member = await _db.FamilyMembers.FirstOrDefaultAsync(f => f.Id == memberId && f.PatientId == patient.Id);
        if (member is null) return ApiResponse<bool>.Fail("Family member not found.");

        _db.FamilyMembers.Remove(member);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Deleted.");
    }

    private static FamilyMemberResponse MapMember(FamilyMember f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        DateOfBirth = f.DateOfBirth,
        Gender = f.Gender,
        Relation = f.Relation,
        CreatedAt = f.CreatedAt
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Medical History Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IMedicalHistoryService
{
    Task<ApiResponse<MedicalHistoryResponse>> CreateAsync(int doctorUserId, CreateMedicalHistoryRequest req);
    Task<ApiResponse<List<MedicalHistoryResponse>>> GetByPatientAsync(int patientId, int requestingUserId, UserRole role);
    Task<ApiResponse<List<MedicalHistoryResponse>>> GetPatientHistoryAsync(int patientId, int doctorUserId);

    // Prescription
    Task<ApiResponse<PrescriptionResponse>> AddPrescriptionAsync(int doctorUserId, CreatePrescriptionRequest req);
    Task<ApiResponse<PrescriptionResponse>> UpdatePrescriptionAsync(int doctorUserId, int prescriptionId, UpdatePrescriptionRequest req);
}

public class MedicalHistoryService : IMedicalHistoryService
{
    private readonly AppDbContext _db;
    public MedicalHistoryService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<MedicalHistoryResponse>> CreateAsync(int doctorUserId, CreateMedicalHistoryRequest req)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<MedicalHistoryResponse>.Fail("Doctor not found.");

        var history = new MedicalHistory
        {
            PatientId = req.PatientId,
            DoctorId = doctor.Id,
            AppointmentId = req.AppointmentId,
            ChiefComplaint = req.ChiefComplaint,
            ChronicDiseases = req.ChronicDiseases,
            CurrentMedications = req.CurrentMedications,
            Allergies = req.Allergies,
            PreviousSurgeries = req.PreviousSurgeries,
            FamilyHistory = req.FamilyHistory,
            BloodType = req.BloodType,
            IsSmoker = req.IsSmoker,
            Notes = req.Notes
        };
        _db.MedicalHistories.Add(history);
        await _db.SaveChangesAsync();
        return ApiResponse<MedicalHistoryResponse>.Ok(await MapHistory(history), "Medical history created.");
    }

    public async Task<ApiResponse<List<MedicalHistoryResponse>>> GetByPatientAsync(int patientId, int requestingUserId, UserRole role)
    {
        if (role == UserRole.Patient)
        {
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == requestingUserId);
            if (patient is null || patient.Id != patientId)
                return ApiResponse<List<MedicalHistoryResponse>>.Fail("Unauthorized.");
        }

        var histories = await _db.MedicalHistories
            .Include(h => h.Patient).ThenInclude(p => p.User)
            .Include(h => h.Doctor).ThenInclude(d => d.User)
            .Include(h => h.Prescription)
            .Where(h => h.PatientId == patientId)
            .OrderByDescending(h => h.RecordDate)
            .ToListAsync();

        var result = histories.Select(h => MapHistorySync(h)).ToList();
        return ApiResponse<List<MedicalHistoryResponse>>.Ok(result);
    }

    public async Task<ApiResponse<List<MedicalHistoryResponse>>> GetPatientHistoryAsync(int patientId, int doctorUserId)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<List<MedicalHistoryResponse>>.Fail("Doctor not found.");

        var histories = await _db.MedicalHistories
            .Include(h => h.Patient).ThenInclude(p => p.User)
            .Include(h => h.Doctor).ThenInclude(d => d.User)
            .Include(h => h.Prescription)
            .Where(h => h.PatientId == patientId)
            .OrderByDescending(h => h.RecordDate)
            .ToListAsync();

        return ApiResponse<List<MedicalHistoryResponse>>.Ok(histories.Select(MapHistorySync).ToList());
    }

    public async Task<ApiResponse<PrescriptionResponse>> AddPrescriptionAsync(int doctorUserId, CreatePrescriptionRequest req)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<PrescriptionResponse>.Fail("Doctor not found.");

        var history = await _db.MedicalHistories.FirstOrDefaultAsync(h => h.Id == req.MedicalHistoryId && h.DoctorId == doctor.Id);
        if (history is null) return ApiResponse<PrescriptionResponse>.Fail("Medical history not found or access denied.");

        if (await _db.Prescriptions.AnyAsync(p => p.MedicalHistoryId == req.MedicalHistoryId))
            return ApiResponse<PrescriptionResponse>.Fail("Prescription already exists. Use update instead.");

        var prescription = new Prescription
        {
            MedicalHistoryId = req.MedicalHistoryId,
            DoctorId = doctor.Id,
            Medications = req.Medications,
            Instructions = req.Instructions,
            Diagnosis = req.Diagnosis
        };
        _db.Prescriptions.Add(prescription);
        await _db.SaveChangesAsync();
        return ApiResponse<PrescriptionResponse>.Ok(MapPrescription(prescription), "Prescription added.");
    }

    public async Task<ApiResponse<PrescriptionResponse>> UpdatePrescriptionAsync(int doctorUserId, int prescriptionId, UpdatePrescriptionRequest req)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<PrescriptionResponse>.Fail("Doctor not found.");

        var prescription = await _db.Prescriptions.FirstOrDefaultAsync(p => p.Id == prescriptionId && p.DoctorId == doctor.Id);
        if (prescription is null) return ApiResponse<PrescriptionResponse>.Fail("Prescription not found.");

        if (req.Medications is not null) prescription.Medications = req.Medications;
        if (req.Instructions is not null) prescription.Instructions = req.Instructions;
        if (req.Diagnosis is not null) prescription.Diagnosis = req.Diagnosis;
        prescription.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ApiResponse<PrescriptionResponse>.Ok(MapPrescription(prescription), "Prescription updated.");
    }

    private async Task<MedicalHistoryResponse> MapHistory(MedicalHistory h)
    {
        var patient = await _db.Patients.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == h.PatientId);
        var doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == h.DoctorId);
        return new MedicalHistoryResponse
        {
            Id = h.Id,
            PatientId = h.PatientId,
            PatientName = patient?.User.Name ?? "",
            DoctorId = h.DoctorId,
            DoctorName = doctor?.User.Name ?? "",
            AppointmentId = h.AppointmentId,
            ChiefComplaint = h.ChiefComplaint,
            ChronicDiseases = h.ChronicDiseases,
            CurrentMedications = h.CurrentMedications,
            Allergies = h.Allergies,
            PreviousSurgeries = h.PreviousSurgeries,
            FamilyHistory = h.FamilyHistory,
            BloodType = h.BloodType,
            IsSmoker = h.IsSmoker,
            Notes = h.Notes,
            RecordDate = h.RecordDate,
            Prescription = h.Prescription is { } p ? MapPrescription(p) : null
        };
    }

    private static MedicalHistoryResponse MapHistorySync(MedicalHistory h) => new()
    {
        Id = h.Id,
        PatientId = h.PatientId,
        PatientName = h.Patient?.User?.Name ?? "",
        DoctorId = h.DoctorId,
        DoctorName = h.Doctor?.User?.Name ?? "",
        AppointmentId = h.AppointmentId,
        ChiefComplaint = h.ChiefComplaint,
        ChronicDiseases = h.ChronicDiseases,
        CurrentMedications = h.CurrentMedications,
        Allergies = h.Allergies,
        PreviousSurgeries = h.PreviousSurgeries,
        FamilyHistory = h.FamilyHistory,
        BloodType = h.BloodType,
        IsSmoker = h.IsSmoker,
        Notes = h.Notes,
        RecordDate = h.RecordDate,
        Prescription = h.Prescription is { } p ? MapPrescription(p) : null
    };

    private static PrescriptionResponse MapPrescription(Prescription p) => new()
    {
        Id = p.Id,
        MedicalHistoryId = p.MedicalHistoryId,
        Medications = p.Medications,
        Instructions = p.Instructions,
        Diagnosis = p.Diagnosis,
        IssuedAt = p.IssuedAt,
        UpdatedAt = p.UpdatedAt
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Consultation Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IConsultationService
{
    Task<ApiResponse<ConsultationResponse>> CreateAsync(int patientUserId, CreateConsultationRequest req);
    Task<ApiResponse<List<ConsultationResponse>>> GetForUserAsync(int userId, UserRole role);
    Task<ApiResponse<ConsultationResponse>> GetByIdAsync(int id, int userId);
    Task<ApiResponse<MessageResponse>> SendMessageAsync(int consultationId, int userId, SendMessageRequest req);
}

public class ConsultationService : IConsultationService
{
    private readonly AppDbContext _db;
    public ConsultationService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<ConsultationResponse>> CreateAsync(int patientUserId, CreateConsultationRequest req)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == patientUserId);
        if (patient is null) return ApiResponse<ConsultationResponse>.Fail("Patient not found.");

        var consultation = new Consultation
        {
            PatientId = patient.Id,
            DoctorId = req.DoctorId
        };
        _db.Consultations.Add(consultation);
        await _db.SaveChangesAsync();

        return ApiResponse<ConsultationResponse>.Ok(await MapConsultation(consultation));
    }

    public async Task<ApiResponse<List<ConsultationResponse>>> GetForUserAsync(int userId, UserRole role)
    {
        IQueryable<Consultation> query;

        if (role == UserRole.Doctor)
        {
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor is null) return ApiResponse<List<ConsultationResponse>>.Fail("Doctor not found.");
            query = _db.Consultations.Where(c => c.DoctorId == doctor.Id);
        }
        else
        {
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient is null) return ApiResponse<List<ConsultationResponse>>.Fail("Patient not found.");
            query = _db.Consultations.Where(c => c.PatientId == patient.Id);
        }

        var list = await query
            .Include(c => c.Patient).ThenInclude(p => p.User)
            .Include(c => c.Doctor).ThenInclude(d => d.User)
            .Include(c => c.Messages).ThenInclude(m => m.Sender)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return ApiResponse<List<ConsultationResponse>>.Ok(list.Select(MapConsultationSync).ToList());
    }

    public async Task<ApiResponse<ConsultationResponse>> GetByIdAsync(int id, int userId)
    {
        var consultation = await _db.Consultations
            .Include(c => c.Patient).ThenInclude(p => p.User)
            .Include(c => c.Doctor).ThenInclude(d => d.User)
            .Include(c => c.Messages).ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (consultation is null) return ApiResponse<ConsultationResponse>.Fail("Not found.");
        return ApiResponse<ConsultationResponse>.Ok(MapConsultationSync(consultation));
    }

    public async Task<ApiResponse<MessageResponse>> SendMessageAsync(int consultationId, int userId, SendMessageRequest req)
    {
        var consultation = await _db.Consultations.FirstOrDefaultAsync(c => c.Id == consultationId);
        if (consultation is null) return ApiResponse<MessageResponse>.Fail("Consultation not found.");

        var msg = new Message
        {
            ConsultationId = consultationId,
            SenderId = userId,
            Content = req.Content
        };
        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();

        var sender = await _db.Users.FindAsync(userId);
        return ApiResponse<MessageResponse>.Ok(new MessageResponse
        {
            Id = msg.Id,
            SenderId = msg.SenderId,
            SenderName = sender?.Name ?? "",
            Content = msg.Content,
            SentAt = msg.SentAt
        });
    }

    private async Task<ConsultationResponse> MapConsultation(Consultation c)
    {
        var patient = await _db.Patients.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == c.PatientId);
        var doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == c.DoctorId);
        return new ConsultationResponse
        {
            Id = c.Id,
            PatientId = c.PatientId,
            PatientName = patient?.User.Name ?? "",
            DoctorId = c.DoctorId,
            DoctorName = doctor?.User.Name ?? "",
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            Messages = new()
        };
    }

    private static ConsultationResponse MapConsultationSync(Consultation c) => new()
    {
        Id = c.Id,
        PatientId = c.PatientId,
        PatientName = c.Patient?.User?.Name ?? "",
        DoctorId = c.DoctorId,
        DoctorName = c.Doctor?.User?.Name ?? "",
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        Messages = c.Messages.Select(m => new MessageResponse
        {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderName = m.Sender?.Name ?? "",
            Content = m.Content,
            SentAt = m.SentAt
        }).ToList()
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// AI Chatbot Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IAiChatService
{
    Task<ApiResponse<AiChatResponse>> SendAsync(int userId, AiChatRequest req);
    Task<ApiResponse<List<AiChatResponse>>> GetHistoryAsync(int userId);
}

public class AiChatService : IAiChatService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    public AiChatService(AppDbContext db, IConfiguration config, IHttpClientFactory httpFactory)
    {
        _db = db;
        _config = config;
        _http = httpFactory.CreateClient("AiClient");
    }

    public async Task<ApiResponse<AiChatResponse>> SendAsync(int userId, AiChatRequest req)
    {
        var aiReply = await CallExternalAiAsync(req.Message);

        var chat = new AiChat
        {
            UserId = userId,
            UserMessage = req.Message,
            AiReply = aiReply
        };
        _db.AiChats.Add(chat);
        await _db.SaveChangesAsync();

        return ApiResponse<AiChatResponse>.Ok(new AiChatResponse
        {
            UserMessage = chat.UserMessage,
            AiReply = chat.AiReply,
            CreatedAt = chat.CreatedAt
        });
    }

    public async Task<ApiResponse<List<AiChatResponse>>> GetHistoryAsync(int userId)
    {
        var history = await _db.AiChats
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new AiChatResponse
            {
                UserMessage = c.UserMessage,
                AiReply = c.AiReply,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
        return ApiResponse<List<AiChatResponse>>.Ok(history);
    }

    /// <summary>
    /// Calls an external AI provider (configurable via appsettings).
    /// Falls back to a stub if no API key is configured.
    /// </summary>
    private async Task<string> CallExternalAiAsync(string userMessage)
    {
        var apiKey = _config["AiChat:ApiKey"];
        var endpoint = _config["AiChat:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
        var model = _config["AiChat:Model"] ?? "gpt-3.5-turbo";

        if (string.IsNullOrWhiteSpace(apiKey))
            return "AI service is not configured. Please contact the administrator.";

        try
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var body = System.Text.Json.JsonSerializer.Serialize(new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful medical assistant for MediaCare platform. Provide general health information. Always recommend consulting a doctor for medical advice." },
                    new { role = "user", content = userMessage }
                }
            });

            var response = await _http.PostAsync(endpoint,
                new StringContent(body, Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
                return "Sorry, I could not process your request at this time.";

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "No response.";
        }
        catch
        {
            return "An error occurred while contacting the AI service.";
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Post / Blog Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IPostService
{
    Task<ApiResponse<PostResponse>> CreateAsync(int doctorUserId, CreatePostRequest req);
    Task<ApiResponse<PostResponse>> UpdateAsync(int doctorUserId, int postId, UpdatePostRequest req);
    Task<ApiResponse<bool>> DeleteAsync(int doctorUserId, int postId);
    Task<ApiResponse<List<PostResponse>>> GetAllAsync(PostCategory? category = null);
    Task<ApiResponse<PostResponse>> GetByIdAsync(int postId);
}

public class PostService : IPostService
{
    private readonly AppDbContext _db;
    public PostService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<PostResponse>> CreateAsync(int doctorUserId, CreatePostRequest req)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<PostResponse>.Fail("Doctor not found.");

        var post = new Post
        {
            DoctorId = doctor.Id,
            Title = req.Title,
            Content = req.Content,
            ImageUrl = req.ImageUrl,
            Category = req.Category
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        return ApiResponse<PostResponse>.Ok(await MapPost(post));
    }

    public async Task<ApiResponse<PostResponse>> UpdateAsync(int doctorUserId, int postId, UpdatePostRequest req)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<PostResponse>.Fail("Doctor not found.");

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.DoctorId == doctor.Id);
        if (post is null) return ApiResponse<PostResponse>.Fail("Post not found.");

        if (req.Title is not null) post.Title = req.Title;
        if (req.Content is not null) post.Content = req.Content;
        if (req.ImageUrl is not null) post.ImageUrl = req.ImageUrl;
        if (req.Category.HasValue) post.Category = req.Category.Value;
        post.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ApiResponse<PostResponse>.Ok(await MapPost(post));
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int doctorUserId, int postId)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<bool>.Fail("Doctor not found.");

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.DoctorId == doctor.Id);
        if (post is null) return ApiResponse<bool>.Fail("Post not found.");

        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Post deleted.");
    }

    public async Task<ApiResponse<List<PostResponse>>> GetAllAsync(PostCategory? category = null)
    {
        var query = _db.Posts.Include(p => p.Doctor).ThenInclude(d => d.User).AsQueryable();

        if (category.HasValue)
            query = query.Where(p => p.Category == category.Value);

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var result = posts.Select(p => new PostResponse
        {
            Id = p.Id,
            DoctorId = p.DoctorId,
            DoctorName = p.Doctor.User.Name,
            Title = p.Title,
            Content = p.Content,
            ImageUrl = p.ImageUrl,
            Category = p.Category.ToString(),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        }).ToList();

        return ApiResponse<List<PostResponse>>.Ok(result);
    }

    public async Task<ApiResponse<PostResponse>> GetByIdAsync(int postId)
    {
        var post = await _db.Posts.Include(p => p.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(p => p.Id == postId);

        if (post is null) return ApiResponse<PostResponse>.Fail("Post not found.");
        return ApiResponse<PostResponse>.Ok(await MapPost(post));
    }

    private async Task<PostResponse> MapPost(Post p)
    {
        if (p.Doctor is null)
        {
            var doc = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == p.DoctorId);
            p.Doctor = doc!;
        }
        return new PostResponse
        {
            Id = p.Id,
            DoctorId = p.DoctorId,
            DoctorName = p.Doctor?.User?.Name ?? "",
            Title = p.Title,
            Content = p.Content,
            ImageUrl = p.ImageUrl,
            Category = p.Category.ToString(),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// User Notification Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IUserNotificationService
{
    Task SendAsync(int userId, string title, string body, int? appointmentId, NotificationTrigger trigger);
    Task<ApiResponse<List<NotificationResponse>>> GetAsync(int userId);
    Task<ApiResponse<bool>> MarkReadAsync(int userId, int notificationId);
}

public class UserNotificationService : IUserNotificationService
{
    private readonly AppDbContext _db;
    public UserNotificationService(AppDbContext db) => _db = db;

    public async Task SendAsync(int userId, string title, string body, int? appointmentId, NotificationTrigger trigger)
    {
        _db.UserNotifications.Add(new UserNotification
        {
            UserId = userId,
            Title = title,
            Body = body,
            AppointmentId = appointmentId,
            Trigger = trigger
        });
        await _db.SaveChangesAsync();
    }

    public async Task<ApiResponse<List<NotificationResponse>>> GetAsync(int userId)
    {
        var list = await _db.UserNotifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationResponse
            {
                Id = n.Id,
                Title = n.Title,
                Body = n.Body,
                AppointmentId = n.AppointmentId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
        return ApiResponse<List<NotificationResponse>>.Ok(list);
    }

    public async Task<ApiResponse<bool>> MarkReadAsync(int userId, int notificationId)
    {
        var n = await _db.UserNotifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (n is null) return ApiResponse<bool>.Fail("Notification not found.");
        n.IsRead = true;
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Clinic Auth Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IClinicAuthService
{
    Task<ApiResponse<ClinicAuthResponse>> LoginAsync(ClinicLoginRequest req);
}

public class ClinicAuthService : IClinicAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public ClinicAuthService(AppDbContext db, IConfiguration config) { _db = db; _config = config; }

    public async Task<ApiResponse<ClinicAuthResponse>> LoginAsync(ClinicLoginRequest req)
    {
        var user = await _db.ClinicUsers.Include(u => u.Clinic).FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return ApiResponse<ClinicAuthResponse>.Fail("Invalid credentials.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(double.Parse(_config["Jwt:ExpiresInHours"]!));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("ClinicId", user.ClinicId.ToString())
        };
        var jwt = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return ApiResponse<ClinicAuthResponse>.Ok(new ClinicAuthResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(jwt),
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            ClinicId = user.ClinicId,
            ExpiresAt = expires
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Clinic Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IClinicService
{
    Task<ApiResponse<ClinicProfileResponse>> GetProfileAsync(int clinicId);
    Task<ApiResponse<ClinicProfileResponse>> UpdateProfileAsync(int clinicId, UpdateClinicProfileRequest req);
    Task<ApiResponse<List<ClinicHoursResponse>>> GetHoursAsync(int clinicId);
    Task<ApiResponse<bool>> UpdateHoursAsync(int clinicId, UpdateClinicHoursRequest req);
}

public class ClinicService : IClinicService
{
    private readonly AppDbContext _db;
    public ClinicService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<ClinicProfileResponse>> GetProfileAsync(int clinicId)
    {
        var clinic = await _db.Clinics.FindAsync(clinicId);
        if (clinic is null) return ApiResponse<ClinicProfileResponse>.Fail("Clinic not found.");
        return ApiResponse<ClinicProfileResponse>.Ok(MapClinic(clinic));
    }

    public async Task<ApiResponse<ClinicProfileResponse>> UpdateProfileAsync(int clinicId, UpdateClinicProfileRequest req)
    {
        var clinic = await _db.Clinics.FindAsync(clinicId);
        if (clinic is null) return ApiResponse<ClinicProfileResponse>.Fail("Clinic not found.");

        clinic.Name = req.Name;
        clinic.Address = req.Address;
        clinic.Phone = req.Phone;
        clinic.Email = req.Email;
        clinic.Description = req.Description;
        clinic.LogoUrl = req.LogoUrl;

        await _db.SaveChangesAsync();
        return ApiResponse<ClinicProfileResponse>.Ok(MapClinic(clinic));
    }

    public async Task<ApiResponse<List<ClinicHoursResponse>>> GetHoursAsync(int clinicId)
    {
        var hours = await _db.ClinicHours
            .Where(h => h.ClinicId == clinicId)
            .Select(h => new ClinicHoursResponse
            {
                Id = h.Id,
                DayOfWeek = h.DayOfWeek.ToString(),
                OpenTime = h.OpenTime.ToString("HH:mm"),
                CloseTime = h.CloseTime.ToString("HH:mm"),
                IsClosed = h.IsClosed
            })
            .ToListAsync();
        return ApiResponse<List<ClinicHoursResponse>>.Ok(hours);
    }

    public async Task<ApiResponse<bool>> UpdateHoursAsync(int clinicId, UpdateClinicHoursRequest req)
    {
        var existing = _db.ClinicHours.Where(h => h.ClinicId == clinicId);
        _db.ClinicHours.RemoveRange(existing);
        foreach (var item in req.Hours)
        {
            _db.ClinicHours.Add(new ClinicHours
            {
                ClinicId = clinicId,
                DayOfWeek = item.DayOfWeek,
                OpenTime = TimeOnly.Parse(item.OpenTime),
                CloseTime = TimeOnly.Parse(item.CloseTime),
                IsClosed = item.IsClosed
            });
        }
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    private static ClinicProfileResponse MapClinic(Clinic c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Address = c.Address,
        Phone = c.Phone,
        Email = c.Email,
        Description = c.Description,
        LogoUrl = c.LogoUrl
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Provider Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IProviderService
{
    Task<ApiResponse<ProviderResponse>> CreateAsync(int clinicId, ProviderRequest req);
    Task<ApiResponse<List<ProviderResponse>>> GetAsync(int clinicId);
    Task<ApiResponse<ProviderResponse>> UpdateAsync(int clinicId, int providerId, ProviderRequest req);
    Task<ApiResponse<bool>> DeleteAsync(int clinicId, int providerId);
    Task<ApiResponse<ProviderScheduleResponse>> SetScheduleAsync(int clinicId, int providerId, ProviderScheduleRequest req);
    Task<ApiResponse<ProviderScheduleResponse>> GetScheduleAsync(int clinicId, int providerId);
    Task<ApiResponse<List<SlotResponse>>> GetSlotsAsync(int providerId, DateTime? date);
    Task<ApiResponse<bool>> GenerateSlotsAsync(int clinicId, int providerId, RegenerateSlotsRequest req);
}

public class ProviderService : IProviderService
{
    private readonly AppDbContext _db;
    public ProviderService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<ProviderResponse>> CreateAsync(int clinicId, ProviderRequest req)
    {
        var provider = new Provider
        {
            ClinicId = clinicId,
            Name = req.Name,
            Specialization = req.Specialization,
            Phone = req.Phone,
            ConsultationFee = req.ConsultationFee,
            DoctorId = req.DoctorId
        };
        _db.Providers.Add(provider);
        await _db.SaveChangesAsync();
        return ApiResponse<ProviderResponse>.Ok(MapProvider(provider));
    }

    public async Task<ApiResponse<List<ProviderResponse>>> GetAsync(int clinicId)
    {
        var list = await _db.Providers
            .Where(p => p.ClinicId == clinicId && p.IsActive)
            .Select(p => MapProvider(p))
            .ToListAsync();
        return ApiResponse<List<ProviderResponse>>.Ok(list);
    }

    public async Task<ApiResponse<ProviderResponse>> UpdateAsync(int clinicId, int providerId, ProviderRequest req)
    {
        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.Id == providerId && p.ClinicId == clinicId);
        if (provider is null) return ApiResponse<ProviderResponse>.Fail("Provider not found.");

        provider.Name = req.Name;
        provider.Specialization = req.Specialization;
        provider.Phone = req.Phone;
        provider.ConsultationFee = req.ConsultationFee;
        provider.DoctorId = req.DoctorId;

        await _db.SaveChangesAsync();
        return ApiResponse<ProviderResponse>.Ok(MapProvider(provider));
    }

    public async Task<ApiResponse<bool>> DeleteAsync(int clinicId, int providerId)
    {
        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.Id == providerId && p.ClinicId == clinicId);
        if (provider is null) return ApiResponse<bool>.Fail("Provider not found.");

        provider.IsActive = false;
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<ProviderScheduleResponse>> SetScheduleAsync(int clinicId, int providerId, ProviderScheduleRequest req)
    {
        var provider = await _db.Providers.Include(p => p.Schedule).FirstOrDefaultAsync(p => p.Id == providerId && p.ClinicId == clinicId);
        if (provider is null) return ApiResponse<ProviderScheduleResponse>.Fail("Provider not found.");

        if (provider.Schedule is null)
        {
            provider.Schedule = new ProviderSchedule { ProviderId = providerId };
            _db.ProviderSchedules.Add(provider.Schedule);
        }

        var schedule = provider.Schedule;
        schedule.SlotDuration = req.SlotDuration;
        schedule.ShiftStart = TimeOnly.Parse(req.ShiftStart);
        schedule.ShiftEnd = TimeOnly.Parse(req.ShiftEnd);
        schedule.BreakStart = req.BreakStart is null ? null : TimeOnly.Parse(req.BreakStart);
        schedule.BreakEnd = req.BreakEnd is null ? null : TimeOnly.Parse(req.BreakEnd);
        schedule.MaxPatientsPerDay = req.MaxPatientsPerDay;
        schedule.WorkingDays = req.WorkingDays;

        await _db.SaveChangesAsync();
        return ApiResponse<ProviderScheduleResponse>.Ok(MapSchedule(schedule));
    }

    public async Task<ApiResponse<ProviderScheduleResponse>> GetScheduleAsync(int clinicId, int providerId)
    {
        var schedule = await _db.ProviderSchedules
            .Include(s => s.Provider)
            .FirstOrDefaultAsync(s => s.ProviderId == providerId && s.Provider.ClinicId == clinicId);

        if (schedule is null) return ApiResponse<ProviderScheduleResponse>.Fail("Schedule not found.");
        return ApiResponse<ProviderScheduleResponse>.Ok(MapSchedule(schedule));
    }

    public async Task<ApiResponse<List<SlotResponse>>> GetSlotsAsync(int providerId, DateTime? date)
    {
        var query = _db.Slots.Where(s => s.ProviderId == providerId && s.Status == SlotStatus.Available);
        if (date.HasValue)
            query = query.Where(s => s.StartTime.Date == date.Value.Date);

        var slots = await query.OrderBy(s => s.StartTime).Select(s => MapSlot(s)).ToListAsync();
        return ApiResponse<List<SlotResponse>>.Ok(slots);
    }

    public async Task<ApiResponse<bool>> GenerateSlotsAsync(int clinicId, int providerId, RegenerateSlotsRequest req)
    {
        var provider = await _db.Providers.Include(p => p.Schedule).FirstOrDefaultAsync(p => p.Id == providerId && p.ClinicId == clinicId);
        if (provider?.Schedule is null) return ApiResponse<bool>.Fail("Provider schedule not configured.");

        var schedule = provider.Schedule;
        var workingDays = schedule.WorkingDays.Split(',').Select(d => (DayOfWeek)int.Parse(d)).ToHashSet();

        // Remove existing available slots in range
        var oldSlots = _db.Slots.Where(s => s.ProviderId == providerId
            && s.Status == SlotStatus.Available
            && s.StartTime.Date >= req.FromDate.Date
            && s.StartTime.Date <= req.ToDate.Date);
        _db.Slots.RemoveRange(oldSlots);

        for (var day = req.FromDate.Date; day <= req.ToDate.Date; day = day.AddDays(1))
        {
            if (!workingDays.Contains(day.DayOfWeek)) continue;

            var current = day + schedule.ShiftStart.ToTimeSpan();
            var end = day + schedule.ShiftEnd.ToTimeSpan();

            while (current.AddMinutes(schedule.SlotDuration) <= end)
            {
                // Skip break
                if (schedule.BreakStart.HasValue && schedule.BreakEnd.HasValue)
                {
                    var breakStart = day + schedule.BreakStart.Value.ToTimeSpan();
                    var breakEnd = day + schedule.BreakEnd.Value.ToTimeSpan();
                    if (current >= breakStart && current < breakEnd)
                    {
                        current = breakEnd;
                        continue;
                    }
                }
                _db.Slots.Add(new Slot
                {
                    ProviderId = providerId,
                    StartTime = current,
                    EndTime = current.AddMinutes(schedule.SlotDuration),
                    Status = SlotStatus.Available
                });
                current = current.AddMinutes(schedule.SlotDuration);
            }
        }

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Slots generated.");
    }

    private static ProviderResponse MapProvider(Provider p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Specialization = p.Specialization,
        Phone = p.Phone,
        ConsultationFee = p.ConsultationFee,
        IsActive = p.IsActive,
        DoctorId = p.DoctorId
    };

    private static ProviderScheduleResponse MapSchedule(ProviderSchedule s) => new()
    {
        Id = s.Id,
        ProviderId = s.ProviderId,
        SlotDuration = s.SlotDuration,
        ShiftStart = s.ShiftStart.ToString("HH:mm"),
        ShiftEnd = s.ShiftEnd.ToString("HH:mm"),
        BreakStart = s.BreakStart?.ToString("HH:mm"),
        BreakEnd = s.BreakEnd?.ToString("HH:mm"),
        MaxPatientsPerDay = s.MaxPatientsPerDay,
        WorkingDays = s.WorkingDays
    };

    private static SlotResponse MapSlot(Slot s) => new()
    {
        Id = s.Id,
        ProviderId = s.ProviderId,
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        Status = s.Status.ToString()
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Clinic Appointment Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IClinicAppointmentService
{
    Task<ApiResponse<ClinicAppointmentResponse>> CreateAsync(int clinicId, int providerId, int? patientId, int? slotId, string type, string? notes);
    Task<ApiResponse<List<ClinicAppointmentResponse>>> GetByProviderAsync(int providerId, DateTime? date);
    Task<ApiResponse<List<ClinicAppointmentResponse>>> GetDoctorAppointmentsAsync(int doctorUserId, DateTime? date);
    Task<ApiResponse<ClinicAppointmentResponse>> UpdateStatusAsync(int appointmentId, string status);
}

public class ClinicAppointmentService : IClinicAppointmentService
{
    private readonly AppDbContext _db;
    public ClinicAppointmentService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<ClinicAppointmentResponse>> CreateAsync(int clinicId, int providerId, int? patientId, int? slotId, string type, string? notes)
    {
        if (slotId.HasValue)
        {
            var slot = await _db.Slots.FirstOrDefaultAsync(s => s.Id == slotId);
            if (slot is null) return ApiResponse<ClinicAppointmentResponse>.Fail("Slot not found.");
            if (slot.Status != SlotStatus.Available)
                return ApiResponse<ClinicAppointmentResponse>.Fail("Slot is already booked.");
            slot.Status = SlotStatus.Booked;
        }

        var appt = new ClinicAppointment
        {
            ProviderId = providerId,
            PatientId = patientId,
            SlotId = slotId,
            Type = Enum.Parse<AppointmentType>(type, true),
            Notes = notes
        };
        _db.ClinicAppointments.Add(appt);
        await _db.SaveChangesAsync();

        return ApiResponse<ClinicAppointmentResponse>.Ok(await MapAppt(appt));
    }

    public async Task<ApiResponse<List<ClinicAppointmentResponse>>> GetByProviderAsync(int providerId, DateTime? date)
    {
        var query = _db.ClinicAppointments
            .Include(a => a.Provider)
            .Include(a => a.Patient).ThenInclude(p => p!.User)
            .Include(a => a.Slot)
            .Where(a => a.ProviderId == providerId);

        if (date.HasValue)
            query = query.Where(a => a.Slot != null && a.Slot.StartTime.Date == date.Value.Date);

        var list = await query.OrderBy(a => a.CreatedAt).ToListAsync();
        return ApiResponse<List<ClinicAppointmentResponse>>.Ok(list.Select(MapApptSync).ToList());
    }

    public async Task<ApiResponse<List<ClinicAppointmentResponse>>> GetDoctorAppointmentsAsync(int doctorUserId, DateTime? date)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<List<ClinicAppointmentResponse>>.Fail("Doctor not found.");

        var providerIds = await _db.Providers
            .Where(p => p.DoctorId == doctor.Id && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync();

        var query = _db.ClinicAppointments
            .Include(a => a.Provider)
            .Include(a => a.Patient).ThenInclude(p => p!.User)
            .Include(a => a.Slot)
            .Where(a => providerIds.Contains(a.ProviderId));

        if (date.HasValue)
            query = query.Where(a => a.Slot != null && a.Slot.StartTime.Date == date.Value.Date);

        var list = await query.OrderBy(a => a.CreatedAt).ToListAsync();
        return ApiResponse<List<ClinicAppointmentResponse>>.Ok(list.Select(MapApptSync).ToList());
    }

    public async Task<ApiResponse<ClinicAppointmentResponse>> UpdateStatusAsync(int appointmentId, string status)
    {
        var appt = await _db.ClinicAppointments
            .Include(a => a.Provider)
            .Include(a => a.Patient).ThenInclude(p => p!.User)
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appt is null) return ApiResponse<ClinicAppointmentResponse>.Fail("Appointment not found.");
        appt.Status = Enum.Parse<ClinicAppointmentStatus>(status, true);
        await _db.SaveChangesAsync();
        return ApiResponse<ClinicAppointmentResponse>.Ok(MapApptSync(appt));
    }

    private async Task<ClinicAppointmentResponse> MapAppt(ClinicAppointment a)
    {
        var provider = await _db.Providers.FindAsync(a.ProviderId);
        return new ClinicAppointmentResponse
        {
            Id = a.Id,
            ProviderId = a.ProviderId,
            ProviderName = provider?.Name ?? "",
            PatientId = a.PatientId,
            SlotId = a.SlotId,
            Type = a.Type.ToString(),
            Status = a.Status.ToString(),
            PaymentStatus = a.PaymentStatus.ToString(),
            Fee = a.Fee,
            Notes = a.Notes,
            CreatedAt = a.CreatedAt
        };
    }

    private static ClinicAppointmentResponse MapApptSync(ClinicAppointment a) => new()
    {
        Id = a.Id,
        ProviderId = a.ProviderId,
        ProviderName = a.Provider?.Name ?? "",
        PatientId = a.PatientId,
        PatientName = a.Patient?.User?.Name,
        SlotId = a.SlotId,
        SlotTime = a.Slot?.StartTime,
        Type = a.Type.ToString(),
        Status = a.Status.ToString(),
        PaymentStatus = a.PaymentStatus.ToString(),
        Fee = a.Fee,
        Notes = a.Notes,
        CreatedAt = a.CreatedAt
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Payment Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IPaymentService
{
    Task<ApiResponse<PaymentResponse>> CreateAsync(int appointmentId, decimal amount);
    Task<ApiResponse<PaymentResponse>> MarkPaidAsync(int paymentId);
    Task<ApiResponse<PaymentSummaryResponse>> GetSummaryAsync(int clinicId, DateTime? date);
}

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    public PaymentService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<PaymentResponse>> CreateAsync(int appointmentId, decimal amount)
    {
        if (await _db.Payments.AnyAsync(p => p.AppointmentId == appointmentId))
            return ApiResponse<PaymentResponse>.Fail("Payment already exists.");

        var payment = new Payment { AppointmentId = appointmentId, Amount = amount };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        return ApiResponse<PaymentResponse>.Ok(MapPayment(payment));
    }

    public async Task<ApiResponse<PaymentResponse>> MarkPaidAsync(int paymentId)
    {
        var payment = await _db.Payments.FindAsync(paymentId);
        if (payment is null) return ApiResponse<PaymentResponse>.Fail("Payment not found.");

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ApiResponse<PaymentResponse>.Ok(MapPayment(payment));
    }

    public async Task<ApiResponse<PaymentSummaryResponse>> GetSummaryAsync(int clinicId, DateTime? date)
    {
        var providerIds = await _db.Providers.Where(p => p.ClinicId == clinicId).Select(p => p.Id).ToListAsync();
        var appointmentIds = await _db.ClinicAppointments.Where(a => providerIds.Contains(a.ProviderId)).Select(a => a.Id).ToListAsync();

        var query = _db.Payments.Where(p => appointmentIds.Contains(p.AppointmentId));
        if (date.HasValue)
            query = query.Where(p => p.CreatedAt.Date == date.Value.Date);

        var all = await query.ToListAsync();
        var paid = all.Where(p => p.Status == PaymentStatus.Paid).ToList();
        var pending = all.Where(p => p.Status == PaymentStatus.Pending).ToList();

        return ApiResponse<PaymentSummaryResponse>.Ok(new PaymentSummaryResponse
        {
            TotalRevenue = paid.Sum(p => p.Amount),
            PaidCount = paid.Count,
            PendingCount = pending.Count,
            PendingAmount = pending.Sum(p => p.Amount)
        });
    }

    private static PaymentResponse MapPayment(Payment p) => new()
    {
        Id = p.Id,
        AppointmentId = p.AppointmentId,
        Amount = p.Amount,
        Status = p.Status.ToString(),
        PaidAt = p.PaidAt,
        CreatedAt = p.CreatedAt
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Queue Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IQueueService
{
    Task<ApiResponse<QueueOverviewResponse>> GetQueueAsync(int doctorId, DateTime? date);
    Task<ApiResponse<QueueItemResponse>> UpdateStatusAsync(int queueItemId, string status);
    Task<ApiResponse<List<QueueItemResponse>>> GetByProviderAsync(int providerId);
}

public class QueueService : IQueueService
{
    private readonly AppDbContext _db;
    public QueueService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<QueueOverviewResponse>> GetQueueAsync(int doctorId, DateTime? date)
    {
        var targetDate = date ?? DateTime.UtcNow;

        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId);
        if (doctor is null) return ApiResponse<QueueOverviewResponse>.Fail("Doctor not found.");

        var providerIds = await _db.Providers
            .Where(p => p.DoctorId == doctorId && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync();

        var items = await _db.QueueItems
            .Include(q => q.Provider)
            .Where(q => providerIds.Contains(q.ProviderId) && q.CreatedAt.Date == targetDate.Date)
            .OrderBy(q => q.QueueNumber)
            .ToListAsync();

        return ApiResponse<QueueOverviewResponse>.Ok(new QueueOverviewResponse
        {
            Waiting = items.Where(q => q.Status == QueueStatus.Waiting).Select(MapQueue).ToList(),
            InProgress = items.Where(q => q.Status == QueueStatus.InProgress).Select(MapQueue).ToList(),
            Completed = items.Where(q => q.Status == QueueStatus.Completed).Select(MapQueue).ToList()
        });
    }

    public async Task<ApiResponse<QueueItemResponse>> UpdateStatusAsync(int queueItemId, string status)
    {
        var item = await _db.QueueItems.Include(q => q.Provider).FirstOrDefaultAsync(q => q.Id == queueItemId);
        if (item is null) return ApiResponse<QueueItemResponse>.Fail("Queue item not found.");

        item.Status = Enum.Parse<QueueStatus>(status, true);
        await _db.SaveChangesAsync();
        return ApiResponse<QueueItemResponse>.Ok(MapQueue(item));
    }

    public async Task<ApiResponse<List<QueueItemResponse>>> GetByProviderAsync(int providerId)
    {
        var items = await _db.QueueItems
            .Include(q => q.Provider)
            .Where(q => q.ProviderId == providerId && q.CreatedAt.Date == DateTime.UtcNow.Date)
            .OrderBy(q => q.QueueNumber)
            .Select(q => MapQueue(q))
            .ToListAsync();
        return ApiResponse<List<QueueItemResponse>>.Ok(items);
    }

    private static QueueItemResponse MapQueue(QueueItem q) => new()
    {
        Id = q.Id,
        QueueNumber = q.QueueNumber,
        PatientName = q.PatientName,
        ProviderId = q.ProviderId,
        ProviderName = q.Provider?.Name ?? "",
        AppointmentId = q.AppointmentId,
        Type = q.Type.ToString(),
        Status = q.Status.ToString(),
        CreatedAt = q.CreatedAt
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Walk-In Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IWalkInService
{
    Task<ApiResponse<WalkInResponse>> CreateAsync(WalkInRequest req);
    Task<ApiResponse<List<WalkInResponse>>> GetByProviderAsync(int providerId);
}

public class WalkInService : IWalkInService
{
    private readonly AppDbContext _db;
    public WalkInService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<WalkInResponse>> CreateAsync(WalkInRequest req)
    {
        if (req.SlotId.HasValue)
        {
            var slot = await _db.Slots.FirstOrDefaultAsync(s => s.Id == req.SlotId);
            if (slot is null) return ApiResponse<WalkInResponse>.Fail("Slot not found.");
            if (slot.Status != SlotStatus.Available)
                return ApiResponse<WalkInResponse>.Fail("Slot is already booked.");
            slot.Status = SlotStatus.Booked;
        }

        var walkIn = new WalkIn
        {
            ProviderId = req.ProviderId,
            SlotId = req.SlotId,
            FullName = req.FullName,
            Phone = req.Phone,
            Age = req.Age,
            Gender = req.Gender,
            ChiefComplaint = req.ChiefComplaint,
            IsNewPatient = req.IsNewPatient,
            HasLabResults = req.HasLabResults,
            HasRadiology = req.HasRadiology,
            Attachments = req.Attachments
        };
        _db.WalkIns.Add(walkIn);

        // Add to queue
        var lastQueue = await _db.QueueItems
            .Where(q => q.ProviderId == req.ProviderId && q.CreatedAt.Date == DateTime.UtcNow.Date)
            .OrderByDescending(q => q.QueueNumber)
            .FirstOrDefaultAsync();

        _db.QueueItems.Add(new QueueItem
        {
            ProviderId = req.ProviderId,
            PatientName = req.FullName,
            Type = QueueType.WalkIn,
            QueueNumber = (lastQueue?.QueueNumber ?? 0) + 1
        });

        await _db.SaveChangesAsync();

        var provider = await _db.Providers.FindAsync(req.ProviderId);
        return ApiResponse<WalkInResponse>.Ok(new WalkInResponse
        {
            Id = walkIn.Id,
            ProviderId = walkIn.ProviderId,
            ProviderName = provider?.Name ?? "",
            FullName = walkIn.FullName,
            Phone = walkIn.Phone,
            Age = walkIn.Age,
            Gender = walkIn.Gender,
            ChiefComplaint = walkIn.ChiefComplaint,
            IsNewPatient = walkIn.IsNewPatient,
            HasLabResults = walkIn.HasLabResults,
            HasRadiology = walkIn.HasRadiology,
            CreatedAt = walkIn.CreatedAt
        });
    }

    public async Task<ApiResponse<List<WalkInResponse>>> GetByProviderAsync(int providerId)
    {
        var provider = await _db.Providers.FindAsync(providerId);
        var list = await _db.WalkIns
            .Where(w => w.ProviderId == providerId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        return ApiResponse<List<WalkInResponse>>.Ok(list.Select(w => new WalkInResponse
        {
            Id = w.Id,
            ProviderId = w.ProviderId,
            ProviderName = provider?.Name ?? "",
            FullName = w.FullName,
            Phone = w.Phone,
            Age = w.Age,
            Gender = w.Gender,
            ChiefComplaint = w.ChiefComplaint,
            IsNewPatient = w.IsNewPatient,
            HasLabResults = w.HasLabResults,
            HasRadiology = w.HasRadiology,
            CreatedAt = w.CreatedAt
        }).ToList());
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Dashboard Service
// ─────────────────────────────────────────────────────────────────────────────

public interface IDashboardService
{
    Task<ApiResponse<DashboardOverviewResponse>> GetOverviewAsync(int clinicId);
    Task<ApiResponse<RevenueResponse>> GetRevenueAsync(int clinicId, DateTime? date);
    Task<ApiResponse<AppointmentsStatsResponse>> GetAppointmentsStatsAsync(int clinicId, DateTime? date);
    Task<ApiResponse<DoctorDashboardOverviewResponse>> GetDoctorOverviewAsync(int doctorUserId);
    Task<ApiResponse<DoctorEarningsResponse>> GetDoctorEarningsAsync(int doctorUserId, string period = "daily");
    Task<ApiResponse<DoctorCasesResponse>> GetDoctorCasesAsync(int doctorUserId, DateTime? date);
}

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    public DashboardService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<DashboardOverviewResponse>> GetOverviewAsync(int clinicId)
    {
        var today = DateTime.UtcNow.Date;
        var providerIds = await _db.Providers.Where(p => p.ClinicId == clinicId).Select(p => p.Id).ToListAsync();

        var appointments = await _db.ClinicAppointments
            .Include(a => a.Slot)
            .Include(a => a.Payment)
            .Where(a => providerIds.Contains(a.ProviderId) && a.CreatedAt.Date == today)
            .ToListAsync();

        var queueItems = await _db.QueueItems
            .Include(q => q.Provider)
            .Where(q => providerIds.Contains(q.ProviderId) && q.CreatedAt.Date == today)
            .ToListAsync();

        return ApiResponse<DashboardOverviewResponse>.Ok(new DashboardOverviewResponse
        {
            Date = today,
            TotalRevenue = appointments.Where(a => a.Payment?.Status == PaymentStatus.Paid).Sum(a => a.Payment!.Amount),
            TotalAppointments = appointments.Count,
            WalkInCount = appointments.Count(a => a.Type == AppointmentType.WalkIn),
            OnlineAppointmentsCount = appointments.Count(a => a.Type == AppointmentType.Online),
            PaidCount = appointments.Count(a => a.Payment?.Status == PaymentStatus.Paid),
            PendingCount = appointments.Count(a => a.Payment?.Status == PaymentStatus.Pending),
            WaitingCount = appointments.Count(a => a.Status == ClinicAppointmentStatus.Waiting),
            WithDoctorCount = appointments.Count(a => a.Status == ClinicAppointmentStatus.WithDoctor),
            CompletedCount = appointments.Count(a => a.Status == ClinicAppointmentStatus.Completed),
            NoShowCount = appointments.Count(a => a.Status == ClinicAppointmentStatus.NoShow),
            CancelledCount = appointments.Count(a => a.Status == ClinicAppointmentStatus.Cancelled),
            LiveQueue = queueItems.Where(q => q.Status == QueueStatus.Waiting || q.Status == QueueStatus.InProgress)
                .OrderBy(q => q.QueueNumber)
                .Select(q => new QueueItemResponse
                {
                    Id = q.Id,
                    QueueNumber = q.QueueNumber,
                    PatientName = q.PatientName,
                    ProviderId = q.ProviderId,
                    ProviderName = q.Provider?.Name ?? "",
                    AppointmentId = q.AppointmentId,
                    Type = q.Type.ToString(),
                    Status = q.Status.ToString(),
                    CreatedAt = q.CreatedAt
                }).ToList()
        });
    }

    public async Task<ApiResponse<RevenueResponse>> GetRevenueAsync(int clinicId, DateTime? date)
    {
        var targetDate = date ?? DateTime.UtcNow;
        var providers = await _db.Providers.Where(p => p.ClinicId == clinicId).ToListAsync();
        var providerIds = providers.Select(p => p.Id).ToList();

        var payments = await _db.Payments
            .Include(p => p.Appointment).ThenInclude(a => a.Provider)
            .Where(p => providerIds.Contains(p.Appointment.ProviderId)
                && p.Status == PaymentStatus.Paid
                && p.PaidAt.HasValue && p.PaidAt.Value.Date == targetDate.Date)
            .ToListAsync();

        var byProvider = providerIds.Select(pid =>
        {
            var prov = providers.First(p => p.Id == pid);
            var provPayments = payments.Where(p => p.Appointment.ProviderId == pid).ToList();
            return new RevenueItem
            {
                ProviderId = pid,
                ProviderName = prov.Name,
                Revenue = provPayments.Sum(p => p.Amount),
                PaidCount = provPayments.Count
            };
        }).ToList();

        return ApiResponse<RevenueResponse>.Ok(new RevenueResponse
        {
            Date = targetDate,
            TotalRevenue = payments.Sum(p => p.Amount),
            PaidCount = payments.Count,
            ByProvider = byProvider
        });
    }

    public async Task<ApiResponse<AppointmentsStatsResponse>> GetAppointmentsStatsAsync(int clinicId, DateTime? date)
    {
        var targetDate = date ?? DateTime.UtcNow;
        var providerIds = await _db.Providers.Where(p => p.ClinicId == clinicId).Select(p => p.Id).ToListAsync();

        var all = await _db.ClinicAppointments
            .Where(a => providerIds.Contains(a.ProviderId) && a.CreatedAt.Date == targetDate.Date)
            .ToListAsync();

        return ApiResponse<AppointmentsStatsResponse>.Ok(new AppointmentsStatsResponse
        {
            Total = all.Count,
            Online = all.Count(a => a.Type == AppointmentType.Online),
            WalkIn = all.Count(a => a.Type == AppointmentType.WalkIn),
            Waiting = all.Count(a => a.Status == ClinicAppointmentStatus.Waiting),
            Arrived = all.Count(a => a.Status == ClinicAppointmentStatus.Arrived),
            WithDoctor = all.Count(a => a.Status == ClinicAppointmentStatus.WithDoctor),
            Completed = all.Count(a => a.Status == ClinicAppointmentStatus.Completed),
            NoShow = all.Count(a => a.Status == ClinicAppointmentStatus.NoShow),
            Cancelled = all.Count(a => a.Status == ClinicAppointmentStatus.Cancelled)
        });
    }

    public async Task<ApiResponse<DoctorDashboardOverviewResponse>> GetDoctorOverviewAsync(int doctorUserId)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<DoctorDashboardOverviewResponse>.Fail("Doctor not found.");

        var today = DateTime.UtcNow.Date;
        var providerIds = await _db.Providers.Where(p => p.DoctorId == doctor.Id).Select(p => p.Id).ToListAsync();

        var appointments = await _db.ClinicAppointments
            .Include(a => a.Provider)
            .Include(a => a.Patient).ThenInclude(p => p!.User)
            .Include(a => a.Slot)
            .Where(a => providerIds.Contains(a.ProviderId) && a.CreatedAt.Date == today)
            .ToListAsync();

        var paidAppointments = await _db.Payments
            .Include(p => p.Appointment)
            .Where(p => providerIds.Contains(p.Appointment.ProviderId)
                && p.Status == PaymentStatus.Paid
                && p.PaidAt.HasValue && p.PaidAt.Value.Date == today)
            .ToListAsync();

        return ApiResponse<DoctorDashboardOverviewResponse>.Ok(new DoctorDashboardOverviewResponse
        {
            Date = today,
            TotalCases = appointments.Count,
            CompletedCases = appointments.Count(a => a.Status == ClinicAppointmentStatus.Completed),
            WaitingCount = appointments.Count(a => a.Status == ClinicAppointmentStatus.Waiting),
            TodayEarnings = paidAppointments.Sum(p => p.Amount),
            PatientList = appointments.Select(a => new ClinicAppointmentResponse
            {
                Id = a.Id,
                ProviderId = a.ProviderId,
                ProviderName = a.Provider?.Name ?? "",
                PatientId = a.PatientId,
                PatientName = a.Patient?.User?.Name,
                SlotId = a.SlotId,
                SlotTime = a.Slot?.StartTime,
                Type = a.Type.ToString(),
                Status = a.Status.ToString(),
                PaymentStatus = a.PaymentStatus.ToString(),
                Fee = a.Fee,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt
            }).ToList()
        });
    }

    public async Task<ApiResponse<DoctorEarningsResponse>> GetDoctorEarningsAsync(int doctorUserId, string period = "daily")
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<DoctorEarningsResponse>.Fail("Doctor not found.");

        // Earnings only from Completed appointments
        var completedAppointments = await _db.Appointments
            .Where(a => a.DoctorId == doctor.Id && a.Status == AppointmentStatus.Completed)
            .ToListAsync();

        DateTime from = period.ToLower() switch
        {
            "weekly" => DateTime.UtcNow.Date.AddDays(-7),
            "monthly" => DateTime.UtcNow.Date.AddDays(-30),
            _ => DateTime.UtcNow.Date
        };

        var filtered = completedAppointments.Where(a => a.ScheduledAt.Date >= from).ToList();

        var byDate = filtered
            .GroupBy(a => a.ScheduledAt.Date)
            .Select(g => new DoctorEarningsItem
            {
                Date = g.Key,
                Amount = g.Count() * doctor.ConsultationFee,
                Count = g.Count()
            })
            .OrderBy(i => i.Date)
            .ToList();

        return ApiResponse<DoctorEarningsResponse>.Ok(new DoctorEarningsResponse
        {
            TotalEarnings = filtered.Count * doctor.ConsultationFee,
            CompletedAppointments = filtered.Count,
            ByDate = byDate
        });
    }

    public async Task<ApiResponse<DoctorCasesResponse>> GetDoctorCasesAsync(int doctorUserId, DateTime? date)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == doctorUserId);
        if (doctor is null) return ApiResponse<DoctorCasesResponse>.Fail("Doctor not found.");

        var targetDate = date ?? DateTime.UtcNow;
        var providerIds = await _db.Providers.Where(p => p.DoctorId == doctor.Id).Select(p => p.Id).ToListAsync();

        var cases = await _db.ClinicAppointments
            .Include(a => a.Provider)
            .Include(a => a.Patient).ThenInclude(p => p!.User)
            .Include(a => a.Slot)
            .Where(a => providerIds.Contains(a.ProviderId) && a.CreatedAt.Date == targetDate.Date)
            .ToListAsync();

        return ApiResponse<DoctorCasesResponse>.Ok(new DoctorCasesResponse
        {
            Total = cases.Count,
            Completed = cases.Count(a => a.Status == ClinicAppointmentStatus.Completed),
            NoShow = cases.Count(a => a.Status == ClinicAppointmentStatus.NoShow),
            Cancelled = cases.Count(a => a.Status == ClinicAppointmentStatus.Cancelled),
            Cases = cases.Select(a => new ClinicAppointmentResponse
            {
                Id = a.Id,
                ProviderId = a.ProviderId,
                ProviderName = a.Provider?.Name ?? "",
                PatientId = a.PatientId,
                PatientName = a.Patient?.User?.Name,
                SlotId = a.SlotId,
                SlotTime = a.Slot?.StartTime,
                Type = a.Type.ToString(),
                Status = a.Status.ToString(),
                PaymentStatus = a.PaymentStatus.ToString(),
                Fee = a.Fee,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt
            }).ToList()
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Clinic Notification Service (original – for clinic staff)
// ─────────────────────────────────────────────────────────────────────────────

public interface INotificationService
{
    Task<ApiResponse<List<NotificationResponse>>> GetAsync(int clinicUserId);
    Task<ApiResponse<bool>> MarkReadAsync(int clinicUserId, int notificationId);
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    public NotificationService(AppDbContext db) => _db = db;

    public async Task<ApiResponse<List<NotificationResponse>>> GetAsync(int clinicUserId)
    {
        var list = await _db.Notifications
            .Where(n => n.ClinicUserId == clinicUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationResponse
            {
                Id = n.Id,
                Title = n.Title,
                Body = n.Body,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
        return ApiResponse<List<NotificationResponse>>.Ok(list);
    }

    public async Task<ApiResponse<bool>> MarkReadAsync(int clinicUserId, int notificationId)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.ClinicUserId == clinicUserId);
        if (n is null) return ApiResponse<bool>.Fail("Notification not found.");
        n.IsRead = true;
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }
}
