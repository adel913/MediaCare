using Microsoft.EntityFrameworkCore;
using AutoMapper;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.DTOs.Doctor;
using MedicalApp.API.DTOs.Schedule;
using MedicalApp.API.DTOs.Appointment;
using MedicalApp.API.DTOs.MedicalRecord;
using MedicalApp.API.Helpers;
using MedicalApp.API.Models.Entities;
using MedicalApp.API.Models.Enums;
using MedicalApp.API.Services.Interfaces;
using System.Data;
using System.Text.Json;

namespace MedicalApp.API.Services.Implementations
{
    public class DoctorService : IDoctorService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IMedicalRecordService _medicalRecordService;
        private readonly ILogger<DoctorService> _logger;

        public DoctorService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IMedicalRecordService medicalRecordService,
            ILogger<DoctorService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _medicalRecordService = medicalRecordService;
            _logger = logger;
        }

        public async Task<ApiResponse<List<DoctorListItemDto>>> GetAllDoctorsAsync(
            string? specialization = null,
            string? search = null,
            string? government = null,
            string? area = null,
            string? appointmentDay = null,
            Models.Enums.Gender? gender = null,
            decimal? minFee = null,
            decimal? maxFee = null,
            double? minRating = null,
            int? currentPatientUserId = null,
            double? userLat = null,
            double? userLng = null)
        {
            var patient = currentPatientUserId.HasValue
                ? await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == currentPatientUserId.Value)
                : null;

            var favoriteDoctorIds = new HashSet<int>();
            if (patient != null)
            {
                favoriteDoctorIds = (await _unitOfWork.PatientFavorites.Query()
                    .Where(pf => pf.PatientId == patient.Id)
                    .Select(pf => pf.DoctorId)
                    .ToListAsync())
                    .ToHashSet();
            }

            var query = _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .Include(d => d.DoctorClinics)
                    .ThenInclude(dc => dc.Clinic)
                .Where(d => d.User.IsActive);

            if (!string.IsNullOrEmpty(specialization))
                query = query.Where(d => d.Specialization.Contains(specialization));

            if (!string.IsNullOrEmpty(search))
                query = query.Where(d => d.User.FullName.Contains(search) || d.Specialization.Contains(search));

            if (!string.IsNullOrEmpty(government))
                query = query.Where(d => d.DoctorClinics.Any(dc => dc.IsActive && dc.Clinic.Government == government));

            if (!string.IsNullOrEmpty(area))
                query = query.Where(d => d.DoctorClinics.Any(dc => dc.IsActive && dc.Clinic.Area == area));

            if (gender.HasValue)
                query = query.Where(d => d.User.Gender == gender.Value);

            if (minFee.HasValue)
                query = query.Where(d => d.ConsultationFee >= minFee.Value);

            if (maxFee.HasValue)
                query = query.Where(d => d.ConsultationFee <= maxFee.Value);

            if (minRating.HasValue)
                query = query.Where(d => d.AverageRating >= minRating.Value);

            if (!string.IsNullOrEmpty(appointmentDay))
            {
                var todayDayOfWeek = DateTime.Today.DayOfWeek;
                var tomorrowDayOfWeek = DateTime.Today.AddDays(1).DayOfWeek;

                if (appointmentDay.Equals("today", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(d => d.Schedules.Any(s => s.DayOfWeek == todayDayOfWeek && s.IsActive));
                }
                else if (appointmentDay.Equals("tomorrow", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(d => d.Schedules.Any(s => s.DayOfWeek == tomorrowDayOfWeek && s.IsActive));
                }
                else if (appointmentDay.Equals("next7days", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(d => d.Schedules.Any(s => s.IsActive));
                }
            }

            var doctors = await query.ToListAsync();

            var hasUserCoords = userLat.HasValue && userLng.HasValue
                && !double.IsNaN(userLat.Value) && !double.IsNaN(userLng.Value)
                && !double.IsInfinity(userLat.Value) && !double.IsInfinity(userLng.Value)
                && userLat.Value >= -90 && userLat.Value <= 90
                && userLng.Value >= -180 && userLng.Value <= 180;

            var result = doctors.Select(d =>
            {
                var dto = _mapper.Map<DoctorListItemDto>(d);
                dto.FullName = d.User.FullName;
                dto.ProfileImageUrl = d.User.ProfileImageUrl;
                dto.ConsultationFee = d.DoctorClinics
                    .Where(dc => dc.IsActive)
                    .Select(dc => dc.ConsultationFee)
                    .FirstOrDefault() ?? d.ConsultationFee;
                dto.ClinicName = string.Join(", ", d.DoctorClinics.Where(dc => dc.IsActive).Select(dc => dc.Clinic.Name));
                dto.ClinicArea = string.Join(", ", d.DoctorClinics.Where(dc => dc.IsActive).Select(dc => dc.Clinic.Area));
                dto.IsFavorited = favoriteDoctorIds.Contains(d.Id);

                var activeClinic = d.DoctorClinics.FirstOrDefault(dc => dc.IsActive)?.Clinic;
                if (activeClinic != null)
                {
                    dto.Latitude = activeClinic.Latitude;
                    dto.Longitude = activeClinic.Longitude;
                    if (hasUserCoords && activeClinic.Latitude.HasValue && activeClinic.Longitude.HasValue)
                    {
                        dto.DistanceKm = GeoUtils.HaversineKm(
                            userLat!.Value, userLng!.Value,
                            activeClinic.Latitude.Value, activeClinic.Longitude.Value);
                    }
                }
                return dto;
            }).ToList();

            if (hasUserCoords)
                result = result.OrderBy(d => d.DistanceKm ?? double.MaxValue).ToList();

            return ApiResponse<List<DoctorListItemDto>>.Success(result);
        }

        public async Task<ApiResponse<List<DoctorListItemDto>>> GetPopularDoctorsAsync(int? currentPatientUserId = null, double? userLat = null, double? userLng = null)
        {
            var patient = currentPatientUserId.HasValue
                ? await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == currentPatientUserId.Value)
                : null;

            var favoriteDoctorIds = new HashSet<int>();
            if (patient != null)
            {
                favoriteDoctorIds = (await _unitOfWork.PatientFavorites.Query()
                    .Where(pf => pf.PatientId == patient.Id)
                    .Select(pf => pf.DoctorId)
                    .ToListAsync())
                    .ToHashSet();
            }

            var query = _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .Include(d => d.DoctorClinics)
                    .ThenInclude(dc => dc.Clinic)
                .Where(d => d.User.IsActive)
                .OrderByDescending(d => d.AverageRating)
                .Take(10); // Get top 10 doctors based on actual rating

            var doctors = await query.ToListAsync();

            var hasUserCoords = userLat.HasValue && userLng.HasValue
                && !double.IsNaN(userLat.Value) && !double.IsNaN(userLng.Value)
                && !double.IsInfinity(userLat.Value) && !double.IsInfinity(userLng.Value)
                && userLat.Value >= -90 && userLat.Value <= 90
                && userLng.Value >= -180 && userLng.Value <= 180;

            var result = doctors.Select(d =>
            {
                var dto = _mapper.Map<DoctorListItemDto>(d);
                dto.FullName = d.User.FullName;
                dto.ProfileImageUrl = d.User.ProfileImageUrl;
                dto.ConsultationFee = d.DoctorClinics
                    .Where(dc => dc.IsActive)
                    .Select(dc => dc.ConsultationFee)
                    .FirstOrDefault() ?? d.ConsultationFee;
                dto.ClinicName = string.Join(", ", d.DoctorClinics.Where(dc => dc.IsActive).Select(dc => dc.Clinic.Name));
                dto.ClinicArea = string.Join(", ", d.DoctorClinics.Where(dc => dc.IsActive).Select(dc => dc.Clinic.Area));
                dto.IsFavorited = favoriteDoctorIds.Contains(d.Id);

                var activeClinic = d.DoctorClinics.FirstOrDefault(dc => dc.IsActive)?.Clinic;
                if (activeClinic != null)
                {
                    dto.Latitude = activeClinic.Latitude;
                    dto.Longitude = activeClinic.Longitude;
                    if (hasUserCoords && activeClinic.Latitude.HasValue && activeClinic.Longitude.HasValue)
                    {
                        dto.DistanceKm = GeoUtils.HaversineKm(
                            userLat!.Value, userLng!.Value,
                            activeClinic.Latitude.Value, activeClinic.Longitude.Value);
                    }
                }
                return dto;
            }).ToList();

            return ApiResponse<List<DoctorListItemDto>>.Success(result);
        }

        public async Task<ApiResponse<List<NearbyDoctorDto>>> GetNearbyDoctorsAsync(
            double lat,
            double lng,
            double radiusKm = 5,
            string? specialization = null,
            string? search = null)
        {
            if (double.IsNaN(lat) || double.IsNaN(lng) ||
                double.IsInfinity(lat) || double.IsInfinity(lng) ||
                lat < -90 || lat > 90 || lng < -180 || lng > 180)
            {
                return ApiResponse<List<NearbyDoctorDto>>.Success(new List<NearbyDoctorDto>());
            }

            var query = _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .Include(d => d.DoctorClinics)
                    .ThenInclude(dc => dc.Clinic)
                .Where(d => d.User.IsActive
                    && d.DoctorClinics.Any(dc => dc.IsActive
                        && dc.Clinic.Latitude != null
                        && dc.Clinic.Longitude != null));

            if (!string.IsNullOrEmpty(specialization))
                query = query.Where(d => d.Specialization == specialization);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(d => d.User.FullName.Contains(search) || d.Specialization.Contains(search));

            var doctors = await query.ToListAsync();

            var result = new List<NearbyDoctorDto>();

            foreach (var d in doctors)
            {
                var locationDoctorClinic = d.DoctorClinics
                    .Where(dc => dc.IsActive
                        && dc.Clinic.Latitude.HasValue
                        && dc.Clinic.Longitude.HasValue)
                    .OrderBy(dc => dc.Id)
                    .FirstOrDefault();

                if (locationDoctorClinic == null) continue;

                var locationClinic = locationDoctorClinic.Clinic;
                var distance = GeoUtils.HaversineKm(lat, lng, locationClinic.Latitude!.Value, locationClinic.Longitude!.Value);
                if (distance > radiusKm) continue;

                var dto = new NearbyDoctorDto
                {
                    Id = d.Id,
                    FullName = d.User.FullName,
                    Specialization = d.Specialization,
                    ProfileImageUrl = d.User.ProfileImageUrl,
                    ConsultationFee = locationDoctorClinic.ConsultationFee ?? d.ConsultationFee,
                    AverageRating = d.AverageRating,
                    TotalReviews = d.TotalReviews,
                    IsAvailable = d.IsAvailable,
                    ClinicName = string.Join(", ", d.DoctorClinics.Where(dc => dc.IsActive).Select(dc => dc.Clinic.Name)),
                    ClinicArea = string.Join(", ", d.DoctorClinics.Where(dc => dc.IsActive).Select(dc => dc.Clinic.Area)),
                    IsFavorited = false,
                    Latitude = locationClinic.Latitude,
                    Longitude = locationClinic.Longitude,
                    DistanceKm = distance,
                    ClinicIdForLocation = locationClinic.Id
                };
                result.Add(dto);
            }

            return ApiResponse<List<NearbyDoctorDto>>.Success(
                result.OrderBy(r => r.DistanceKm).ToList());
        }

        public async Task<ApiResponse<DoctorProfileDto>> GetDoctorByIdAsync(int doctorId)
        {
            var doctor = await _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .Include(d => d.DoctorClinics)
                    .ThenInclude(dc => dc.Clinic)
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (doctor == null)
                return ApiResponse<DoctorProfileDto>.Failure("Doctor not found", 404);

            var dto = _mapper.Map<DoctorProfileDto>(doctor);
            dto.FullName = doctor.User.FullName;
            dto.PhoneNumber = doctor.User.PhoneNumber;
            dto.Email = doctor.User.Email;
            dto.ProfileImageUrl = doctor.User.ProfileImageUrl;
            dto.ConsultationFee = doctor.DoctorClinics
                .Where(dc => dc.IsActive)
                .Select(dc => dc.ConsultationFee)
                .FirstOrDefault() ?? doctor.ConsultationFee;

            dto.Degree = doctor.Degree;
            dto.University = doctor.University;
            dto.SubSpecialty = doctor.SubSpecialty;
            dto.GraduationYear = doctor.GraduationYear;
            dto.BoardCertification = doctor.BoardCertification;
            dto.QrCodeKey = doctor.QrCodeKey;
            dto.Languages = string.IsNullOrEmpty(doctor.Languages)
                ? new List<string>()
                : doctor.Languages.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();

            dto.AssociatedClinics = doctor.DoctorClinics
                .Where(dc => dc.IsActive)
                .Select(dc => dc.Clinic.Name)
                .ToList();

            var activeClinic = doctor.DoctorClinics.FirstOrDefault(dc => dc.IsActive)?.Clinic;
            if (activeClinic != null)
            {
                dto.ClinicId = activeClinic.Id;
                dto.ClinicName = activeClinic.Name;
                dto.ClinicLatitude = activeClinic.Latitude;
                dto.ClinicLongitude = activeClinic.Longitude;
            }

            dto.TotalPatients = await _unitOfWork.Appointments.Query()
                .Where(a => a.DoctorId == doctor.Id
                         && a.PatientId != null
                         && a.Status != AppointmentStatus.Cancelled)
                .Select(a => a.PatientId!.Value)
                .Distinct()
                .CountAsync();

            return ApiResponse<DoctorProfileDto>.Success(dto);
        }

        public async Task<ApiResponse<DoctorProfileDto>> GetProfileAsync(int userId)
        {
            var doctor = await _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .Include(d => d.DoctorClinics)
                    .ThenInclude(dc => dc.Clinic)
                .FirstOrDefaultAsync(d => d.UserId == userId);

            if (doctor == null)
                return ApiResponse<DoctorProfileDto>.Failure("Profile not found", 404);

            var dto = _mapper.Map<DoctorProfileDto>(doctor);
            dto.FullName = doctor.User.FullName;
            dto.PhoneNumber = doctor.User.PhoneNumber;
            dto.Email = doctor.User.Email;
            dto.ProfileImageUrl = doctor.User.ProfileImageUrl;
            dto.ConsultationFee = doctor.DoctorClinics
                .Where(dc => dc.IsActive)
                .Select(dc => dc.ConsultationFee)
                .FirstOrDefault() ?? doctor.ConsultationFee;

            dto.Degree = doctor.Degree;
            dto.University = doctor.University;
            dto.SubSpecialty = doctor.SubSpecialty;
            dto.GraduationYear = doctor.GraduationYear;
            dto.BoardCertification = doctor.BoardCertification;
            dto.QrCodeKey = doctor.QrCodeKey;
            dto.Languages = string.IsNullOrEmpty(doctor.Languages)
                ? new List<string>()
                : doctor.Languages.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();

            dto.AssociatedClinics = doctor.DoctorClinics
                .Where(dc => dc.IsActive)
                .Select(dc => dc.Clinic.Name)
                .ToList();

            var activeClinic = doctor.DoctorClinics.FirstOrDefault(dc => dc.IsActive)?.Clinic;
            if (activeClinic != null)
            {
                dto.ClinicId = activeClinic.Id;
                dto.ClinicName = activeClinic.Name;
                dto.ClinicLatitude = activeClinic.Latitude;
                dto.ClinicLongitude = activeClinic.Longitude;
            }

            dto.TotalPatients = await _unitOfWork.Appointments.Query()
                .Where(a => a.DoctorId == doctor.Id
                         && a.PatientId != null
                         && a.Status != AppointmentStatus.Cancelled)
                .Select(a => a.PatientId!.Value)
                .Distinct()
                .CountAsync();

            return ApiResponse<DoctorProfileDto>.Success(dto);
        }

        public async Task<ApiResponse<DoctorProfileDto>> UpdateProfileAsync(int userId, UpdateDoctorProfileDto dto)
        {
            var doctor = await _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == userId);

            if (doctor == null)
                return ApiResponse<DoctorProfileDto>.Failure("Profile not found", 404);

            if (!string.IsNullOrEmpty(dto.FullName)) doctor.User.FullName = dto.FullName;
            if (!string.IsNullOrEmpty(dto.PhoneNumber)) doctor.User.PhoneNumber = dto.PhoneNumber;
            if (dto.Email != null) doctor.User.Email = dto.Email;
            if (!string.IsNullOrEmpty(dto.Specialization)) doctor.Specialization = dto.Specialization;
            if (dto.YearsOfExperience.HasValue) doctor.YearsOfExperience = dto.YearsOfExperience.Value;
            if (dto.Bio != null) doctor.Bio = dto.Bio;
            if (dto.ConsultationFee.HasValue) doctor.ConsultationFee = dto.ConsultationFee.Value;
            if (dto.IsAvailable.HasValue) doctor.IsAvailable = dto.IsAvailable.Value;
            if (dto.ProfileImageUrl != null) doctor.User.ProfileImageUrl = dto.ProfileImageUrl;
            if (dto.LicenseImageUrl != null) doctor.LicenseImageUrl = dto.LicenseImageUrl;

            // New profile fields updates
            if (dto.Degree != null) doctor.Degree = dto.Degree;
            if (dto.University != null) doctor.University = dto.University;
            if (dto.SubSpecialty != null) doctor.SubSpecialty = dto.SubSpecialty;
            if (dto.GraduationYear.HasValue) doctor.GraduationYear = dto.GraduationYear.Value;
            if (dto.BoardCertification != null) doctor.BoardCertification = dto.BoardCertification;
            if (dto.Languages != null)
            {
                doctor.Languages = string.Join(", ", dto.Languages);
            }

            await _unitOfWork.CompleteAsync();
            return await GetProfileAsync(userId);
        }

        public async Task<ApiResponse<List<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>>> GetSchedulesAsync(int doctorId)
        {
            var schedules = await _unitOfWork.DoctorSchedules.Query()
                .Where(s => s.DoctorId == doctorId && s.IsActive)
                .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                .ToListAsync();

            var result = schedules.Select(s => 
            {
                var dto = _mapper.Map<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>(s);
                dto.DayName = s.DayOfWeek.ToString();
                return dto;
            }).ToList();

            return ApiResponse<List<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>>.Success(result);
        }

        public async Task<ApiResponse<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>> AddScheduleAsync(int clinicAdminUserId, int doctorId, CreateScheduleDto dto)
        {
            // 1. Get ClinicAdmin and their ClinicId
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(ca => ca.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>.Failure("You are not authorized to perform this operation as a clinic admin", 403);

            int clinicId = admin.ClinicId;

            // 2. Validate Clinic exists and check its opening/closing times
            var clinic = await _unitOfWork.Clinics.GetByIdAsync(clinicId);
            if (clinic == null)
                return ApiResponse<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>.Failure("Clinic not found", 404);

            if (clinic.OpeningTime.HasValue && clinic.ClosingTime.HasValue)
            {
                if (dto.StartTime < clinic.OpeningTime.Value || dto.EndTime > clinic.ClosingTime.Value)
                {
                    return ApiResponse<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>.Failure($"Clinic operating hours are from {clinic.OpeningTime.Value:hh\\:mm} to {clinic.ClosingTime.Value:hh\\:mm}. Schedules outside these times cannot be registered.", 400);
                }
            }

            // 3. Verify Doctor is linked to this Clinic
            var doctor = await _unitOfWork.Doctors.Query()
                .Include(d => d.DoctorClinics)
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (doctor == null)
                return ApiResponse<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>.Failure("Doctor not found", 404);

            if (!doctor.DoctorClinics.Any(dc => dc.ClinicId == clinicId && dc.IsActive))
            {
                return ApiResponse<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>.Failure("This doctor is not registered in this clinic", 400);
            }

            // 4. Validate StartTime < EndTime
            if (dto.StartTime >= dto.EndTime)
                return ApiResponse<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>.Failure("Start time must be before end time");

            // 5. Validate Doctor Overlap globally across ALL clinics
            var hasOverlap = await _unitOfWork.DoctorSchedules.AnyAsync(s =>
                s.DoctorId == doctorId &&
                s.DayOfWeek == dto.DayOfWeek &&
                s.IsActive &&
                s.StartTime < dto.EndTime &&
                s.EndTime > dto.StartTime);

            if (hasOverlap)
                return ApiResponse<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>.Failure("Doctor has a conflicting schedule on the same day and time (in this clinic or another)", 409);

            // 6. Create Schedule
            var schedule = new DoctorSchedule
            {
                DoctorId = doctorId,
                ClinicId = clinicId,
                DayOfWeek = dto.DayOfWeek,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                SlotDurationMinutes = dto.SlotDurationMinutes,
                MaxPatients = dto.MaxPatients
            };

            await _unitOfWork.DoctorSchedules.AddAsync(schedule);
            await _unitOfWork.CompleteAsync();

            var resultDto = _mapper.Map<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>(schedule);
            resultDto.DayName = dto.DayOfWeek.ToString();

            return ApiResponse<MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>.Success(resultDto, "Schedule added successfully", 201);
        }

        public async Task<ApiResponse<bool>> DeleteScheduleAsync(int clinicAdminUserId, int scheduleId)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(ca => ca.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<bool>.Failure("You are not authorized to perform this operation as a clinic admin", 403);

            var schedule = await _unitOfWork.DoctorSchedules.GetByIdAsync(scheduleId);
            if (schedule == null)
                return ApiResponse<bool>.Failure("Schedule not found", 404);

            if (schedule.ClinicId != admin.ClinicId)
                return ApiResponse<bool>.Failure("This schedule does not belong to your clinic", 403);

            schedule.IsActive = false;
            _unitOfWork.DoctorSchedules.Update(schedule);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<bool>.Success(true, "Schedule deleted successfully");
        }

        public async Task<ApiResponse<List<AvailableSlotDto>>> GetAvailableSlotsAsync(int doctorId, DateTime date)
        {
            var schedules = await _unitOfWork.DoctorSchedules.Query()
                .Where(s => s.DoctorId == doctorId && s.DayOfWeek == date.DayOfWeek && s.IsActive)
                .ToListAsync();

            if (!schedules.Any())
                return ApiResponse<List<AvailableSlotDto>>.Success(new List<AvailableSlotDto>(), "No available slots");

            var existingAppointments = await _unitOfWork.Appointments.Query()
                .Where(a => a.DoctorId == doctorId && a.AppointmentDate.Date == date.Date
                    && a.Status != Models.Enums.AppointmentStatus.Cancelled)
                .Select(a => a.StartTime).ToListAsync();

            var slots = new List<AvailableSlotDto>();
            foreach (var schedule in schedules)
            {
                var currentTime = schedule.StartTime;
                while (currentTime.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes)) <= schedule.EndTime)
                {
                    slots.Add(new AvailableSlotDto
                    {
                        Date = date.Date,
                        Time = currentTime,
                        IsAvailable = !existingAppointments.Contains(currentTime)
                    });
                    currentTime = currentTime.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes));
                }
            }

            return ApiResponse<List<AvailableSlotDto>>.Success(slots);
        }

        public async Task<ApiResponse<List<string>>> GetSpecializationsAsync()
        {
            var specializations = await _unitOfWork.Doctors.Query()
                .Where(d => d.User.IsActive && !string.IsNullOrWhiteSpace(d.Specialization))
                .Select(d => d.Specialization)
                .Distinct()
                .ToListAsync();

            return ApiResponse<List<string>>.Success(specializations, "Specializations retrieved successfully");
        }

        // ===== Doctor Dashboard =====
        public async Task<ApiResponse<DoctorDashboardDto>> GetDoctorDashboardAsync(int doctorUserId)
        {
            var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == doctorUserId);
            if (doctor == null)
                return ApiResponse<DoctorDashboardDto>.Failure("Doctor profile not found", 404);

            var today = DateTime.Today;
            var appointments = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                .Where(a => a.DoctorId == doctor.Id && a.AppointmentDate.Date == today && a.Status != AppointmentStatus.Cancelled)
                .ToListAsync();

            decimal earnings = appointments
                .Where(a => a.PaymentStatus == PaymentStatus.Paid)
                .Sum(a => a.ConsultationFee);

            var completedPatientIds = await _unitOfWork.Appointments.Query()
                .Where(ap => ap.DoctorId == doctor.Id && ap.Status == AppointmentStatus.Completed && ap.AppointmentDate.Date < today && ap.PatientId.HasValue)
                .Select(ap => ap.PatientId!.Value)
                .Distinct()
                .ToListAsync();

            var completedOfflinePhones = await _unitOfWork.Appointments.Query()
                .Where(ap => ap.DoctorId == doctor.Id && ap.Status == AppointmentStatus.Completed && ap.AppointmentDate.Date < today && !ap.PatientId.HasValue && !string.IsNullOrEmpty(ap.OfflinePatientPhone))
                .Select(ap => ap.OfflinePatientPhone!)
                .Distinct()
                .ToListAsync();

            int newPatientsCount = 0;
            int followUpsCount = 0;

            foreach (var app in appointments)
            {
                bool isFollowUp = false;
                if (app.PatientId.HasValue)
                {
                    isFollowUp = completedPatientIds.Contains(app.PatientId.Value);
                }
                else if (!string.IsNullOrEmpty(app.OfflinePatientPhone))
                {
                    isFollowUp = completedOfflinePhones.Contains(app.OfflinePatientPhone);
                }

                if (isFollowUp)
                {
                    followUpsCount++;
                }
                else
                {
                    newPatientsCount++;
                }
            }

            var dto = new DoctorDashboardDto
            {
                TotalAppointments = appointments.Count,
                NewPatientsCount = newPatientsCount,
                FollowUpsCount = followUpsCount,
                WalkInsCount = appointments.Count(a => a.PatientId == null || !string.IsNullOrEmpty(a.OfflinePatientName)),
                OnlineCount = appointments.Count(a => a.PatientId != null && string.IsNullOrEmpty(a.OfflinePatientName)),
                TodayEarnings = earnings,
                WaitingCount = appointments.Count(a => a.QueueStatus == QueueStatus.Waiting),
                WithDoctorCount = appointments.Count(a => a.QueueStatus == QueueStatus.InConsultation),
                CompletedCount = appointments.Count(a => a.QueueStatus == QueueStatus.Completed || a.Status == AppointmentStatus.Completed)
            };

            return ApiResponse<DoctorDashboardDto>.Success(dto, "Dashboard statistics retrieved successfully");
        }

        // ===== Doctor Live Queue =====
        public async Task<ApiResponse<List<AppointmentDto>>> GetDoctorLiveQueueAsync(int doctorUserId, string? filterStatus = null)
        {
            var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == doctorUserId);
            if (doctor == null)
                return ApiResponse<List<AppointmentDto>>.Failure("Doctor profile not found", 404);

            var today = DateTime.Today;
            var query = _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.FamilyMember)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor.DoctorClinics)
                    .ThenInclude(dc => dc.Clinic)
                .Where(a => a.DoctorId == doctor.Id && a.AppointmentDate.Date == today && a.Status != AppointmentStatus.Cancelled);

            var appointments = await query.ToListAsync();

            var sortedAppointments = appointments
                .OrderBy(a => a.QueueStatus == QueueStatus.InConsultation ? 0 :
                              a.QueueStatus == QueueStatus.Waiting && a.IsEmergency ? 1 :
                              a.QueueStatus == QueueStatus.Waiting && !a.IsEmergency ? 2 : 3)
                .ThenBy(a => a.StartTime)
                .Select(MapAppointmentToDto)
                .ToList();

            if (!string.IsNullOrEmpty(filterStatus))
            {
                var norm = filterStatus.ToLower().Trim();
                if (norm == "confirmed")
                {
                    sortedAppointments = sortedAppointments.Where(a => a.Status == AppointmentStatus.Confirmed).ToList();
                }
                else if (norm == "arrived")
                {
                    sortedAppointments = sortedAppointments.Where(a => a.QueueStatus == QueueStatus.Waiting).ToList();
                }
                else if (norm == "in progress" || norm == "inprogress")
                {
                    sortedAppointments = sortedAppointments.Where(a => a.QueueStatus == QueueStatus.InConsultation).ToList();
                }
            }

            return ApiResponse<List<AppointmentDto>>.Success(sortedAppointments, "Queue retrieved successfully");
        }

        // ===== Doctor Medical History =====
        public async Task<ApiResponse<PatientHistoryDto>> GetPatientHistoryAsync(int doctorUserId, int patientId)
        {
            var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == doctorUserId);
            if (doctor == null)
                return ApiResponse<PatientHistoryDto>.Failure("Doctor profile not found", 404);

            var patient = await _unitOfWork.Patients.Query()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == patientId);

            if (patient == null)
                return ApiResponse<PatientHistoryDto>.Failure("Patient not found", 404);

            var hasBooking = await _unitOfWork.Appointments.Query()
                .AnyAsync(a => a.DoctorId == doctor.Id
                    && a.PatientId == patientId
                    && a.Status != AppointmentStatus.Cancelled);

            if (!hasBooking)
                return ApiResponse<PatientHistoryDto>.Failure("You are not authorized to view this patient's history", 403);

            var records = await _unitOfWork.MedicalRecords.Query()
                .Include(r => r.Patient).ThenInclude(p => p!.User)
                .Include(r => r.FamilyMember)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .Where(r => r.PatientId == patientId && r.FamilyMemberId == null)
                .OrderByDescending(r => r.VisitDate)
                .ToListAsync();

            var recordDtos = records.Select(MapMedicalRecordToDto).ToList();

            var chronicConditions = string.IsNullOrEmpty(patient.ChronicDiseases)
                ? new List<string>()
                : patient.ChronicDiseases.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

            var currentMedications = string.IsNullOrEmpty(patient.MedicalHistory)
                ? new List<string>()
                : patient.MedicalHistory.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

            foreach (var rec in recordDtos)
            {
                if (rec.Medications != null)
                {
                    foreach (var med in rec.Medications)
                    {
                        if (!string.IsNullOrEmpty(med.Name) && !currentMedications.Contains(med.Name))
                        {
                            currentMedications.Add($"{med.Name} {med.Dosage}".Trim());
                        }
                    }
                }
            }

            int age = 0;
            if (patient.User != null && patient.User.DateOfBirth.HasValue)
            {
                age = DateTime.Today.Year - patient.User.DateOfBirth.Value.Year;
                if (patient.User.DateOfBirth.Value.Date > DateTime.Today.AddYears(-age)) age--;
            }

            var dto = new PatientHistoryDto
            {
                PatientId = patient.Id,
                FullName = patient.User?.FullName ?? string.Empty,
                ProfileImageUrl = patient.User?.ProfileImageUrl,
                Age = age,
                Gender = patient.User?.Gender.ToString(),
                BloodType = patient.BloodType,
                ChronicConditions = chronicConditions,
                CurrentMedications = currentMedications,
                PastRecords = recordDtos
            };

            return ApiResponse<PatientHistoryDto>.Success(dto, "Patient medical history retrieved successfully");
        }

        // ===== Doctor QR Code Key =====
        public async Task<ApiResponse<string>> GetDoctorQrCodeAsync(int doctorUserId)
        {
            var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == doctorUserId);
            if (doctor == null)
                return ApiResponse<string>.Failure("Doctor profile not found", 404);

            if (string.IsNullOrEmpty(doctor.QrCodeKey))
            {
                var rand = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
                doctor.QrCodeKey = $"DOC-{doctor.Id}-{rand}";
                _unitOfWork.Doctors.Update(doctor);
                await _unitOfWork.CompleteAsync();
            }

            return ApiResponse<string>.Success(doctor.QrCodeKey, "QR code key retrieved successfully");
        }

        // ===== Consultation Screen =====
        public async Task<ApiResponse<ConsultationScreenDto>> GetConsultationScreenAsync(int doctorUserId, int appointmentId)
        {
            var doctor = await _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == doctorUserId);
            if (doctor == null)
                return ApiResponse<ConsultationScreenDto>.Failure("Doctor profile not found", 404);

            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .Include(a => a.FamilyMember)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor.DoctorClinics).ThenInclude(dc => dc.Clinic)
                .Include(a => a.MedicalRecord)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.DoctorId == doctor.Id);

            if (appointment == null)
                return ApiResponse<ConsultationScreenDto>.Failure("Appointment not found or does not belong to this doctor", 404);

            if (appointment.Status == AppointmentStatus.Cancelled)
                return ApiResponse<ConsultationScreenDto>.Failure("This appointment has been cancelled", 400);

            var patientInfo = BuildConsultationPatient(appointment);
            var medicalHistory = await LoadMedicalHistoryAsync(appointment);
            var previousVisits = await LoadPreviousVisitsAsync(appointment);

            var previousDiagnoses = medicalHistory
                .Select(r => r.Diagnosis)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .ToList();

            var previousPrescriptions = medicalHistory
                .Where(r => r.Medications != null)
                .SelectMany(r => r.Medications!)
                .ToList();

            var screen = new ConsultationScreenDto
            {
                Appointment = MapAppointmentToDto(appointment),
                Patient = patientInfo,
                MedicalHistory = medicalHistory,
                PreviousVisits = previousVisits,
                PreviousDiagnoses = previousDiagnoses,
                PreviousPrescriptions = previousPrescriptions
            };

            return ApiResponse<ConsultationScreenDto>.Success(screen, "Consultation screen loaded successfully");
        }

        // ===== Active Consultations =====
        public async Task<ApiResponse<List<AppointmentDto>>> GetActiveConsultationsAsync(int doctorUserId)
        {
            var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == doctorUserId);
            if (doctor == null)
                return ApiResponse<List<AppointmentDto>>.Failure("Doctor profile not found", 404);

            var today = DateTime.Today;
            var active = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .Include(a => a.FamilyMember)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor.DoctorClinics).ThenInclude(dc => dc.Clinic)
                .Where(a => a.DoctorId == doctor.Id
                    && a.AppointmentDate.Date == today
                    && a.Status == AppointmentStatus.InProgress
                    && a.QueueStatus == QueueStatus.InConsultation)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            return ApiResponse<List<AppointmentDto>>.Success(
                active.Select(MapAppointmentToDto).ToList(),
                "Active consultations retrieved successfully");
        }

        // ===== Complete Consultation =====
        public async Task<ApiResponse<MedicalRecordDto>> CompleteConsultationAsync(
            int doctorUserId, int appointmentId, CompleteConsultationDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Diagnosis))
                return ApiResponse<MedicalRecordDto>.Failure("Diagnosis is required", 400);

            var doctor = await _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == doctorUserId);
            if (doctor == null)
                return ApiResponse<MedicalRecordDto>.Failure("Doctor profile not found", 404);

            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient).ThenInclude(p => p!.User)
                .Include(a => a.FamilyMember)
                .Include(a => a.MedicalRecord)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.DoctorId == doctor.Id);

            if (appointment == null)
                return ApiResponse<MedicalRecordDto>.Failure("Appointment not found or does not belong to this doctor", 404);

            if (appointment.Status == AppointmentStatus.Completed)
                return ApiResponse<MedicalRecordDto>.Failure("This consultation has already been completed", 400);

            if (appointment.Status == AppointmentStatus.Cancelled)
                return ApiResponse<MedicalRecordDto>.Failure("This appointment has been cancelled", 400);

            if (appointment.MedicalRecord != null)
                return ApiResponse<MedicalRecordDto>.Failure("This consultation has already been completed", 400);

            if (appointment.Status != AppointmentStatus.InProgress
                || appointment.QueueStatus != QueueStatus.InConsultation)
            {
                return ApiResponse<MedicalRecordDto>.Failure(
                    "Consultation must be started before it can be completed", 400);
            }

            var patientId = await EnsureAppointmentPatientIdAsync(appointment);
            if (patientId == null)
                return ApiResponse<MedicalRecordDto>.Failure("Unable to resolve patient for this appointment", 400);

            string? prescriptionContent = null;
            if (dto.Medications != null && dto.Medications.Any())
                prescriptionContent = JsonSerializer.Serialize(dto.Medications);

            MedicalRecord record;
            using (var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                try
                {
                    record = new MedicalRecord
                    {
                        PatientId = patientId.Value,
                        FamilyMemberId = appointment.FamilyMemberId,
                        DoctorId = doctor.Id,
                        AppointmentId = appointmentId,
                        Diagnosis = dto.Diagnosis.Trim(),
                        Prescription = prescriptionContent,
                        Instructions = dto.Instructions,
                        VisitDate = DateTime.UtcNow
                    };

                    await _unitOfWork.MedicalRecords.AddAsync(record);

                    appointment.Status = AppointmentStatus.Completed;
                    appointment.QueueStatus = QueueStatus.Completed;
                    appointment.IsPaid = true;
                    appointment.PaymentStatus = PaymentStatus.Paid;

                    _unitOfWork.Appointments.Update(appointment);
                    await _unitOfWork.CompleteAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            record.Doctor = doctor;
            record.Patient = appointment.Patient!;
            record.FamilyMember = appointment.FamilyMember;

            var recordDto = MapMedicalRecordToDto(record);

            try
            {
                await SendConsultationCompletedNotificationAsync(appointment, dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Consultation completed but notification failed for appointment {AppointmentId}", appointmentId);
            }

            return ApiResponse<MedicalRecordDto>.Success(recordDto, "Consultation completed successfully");
        }

        // ===== Submit Consultation Checkup Session (legacy endpoint) =====
        public async Task<ApiResponse<MedicalRecordDto>> SubmitConsultationSessionAsync(
            int doctorUserId, int appointmentId, CreateMedicalRecordDto dto)
        {
            var completeDto = new CompleteConsultationDto
            {
                Diagnosis = dto.Diagnosis,
                Medications = dto.Medications,
                Instructions = dto.Instructions
                    ?? dto.TreatmentPlan
                    ?? dto.Notes
                    ?? dto.Observations
            };

            return await CompleteConsultationAsync(doctorUserId, appointmentId, completeDto);
        }

        private async Task<int?> EnsureAppointmentPatientIdAsync(Appointment appointment)
        {
            if (appointment.PatientId.HasValue)
                return appointment.PatientId.Value;

            var phone = appointment.OfflinePatientPhone;
            if (string.IsNullOrEmpty(phone))
                phone = "01000000000";

            var existingUser = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.PhoneNumber == phone && u.Role == UserRole.Patient);

            if (existingUser != null)
            {
                var existingPatient = await _unitOfWork.Patients.Query()
                    .FirstOrDefaultAsync(p => p.UserId == existingUser.Id);
                if (existingPatient != null)
                {
                    appointment.PatientId = existingPatient.Id;
                    _unitOfWork.Appointments.Update(appointment);
                    await _unitOfWork.CompleteAsync();
                    return existingPatient.Id;
                }

                var newPatient = new Patient { UserId = existingUser.Id };
                await _unitOfWork.Patients.AddAsync(newPatient);
                await _unitOfWork.CompleteAsync();
                appointment.PatientId = newPatient.Id;
                _unitOfWork.Appointments.Update(appointment);
                await _unitOfWork.CompleteAsync();
                return newPatient.Id;
            }

            var tempUser = new User
            {
                FullName = appointment.OfflinePatientName ?? "Walk-in patient",
                PhoneNumber = phone,
                Role = UserRole.Patient,
                IsActive = true
            };
            await _unitOfWork.Users.AddAsync(tempUser);
            await _unitOfWork.CompleteAsync();

            var walkInPatient = new Patient
            {
                UserId = tempUser.Id,
                MedicalHistory = appointment.ChiefComplaint
            };
            await _unitOfWork.Patients.AddAsync(walkInPatient);
            await _unitOfWork.CompleteAsync();

            appointment.PatientId = walkInPatient.Id;
            _unitOfWork.Appointments.Update(appointment);
            await _unitOfWork.CompleteAsync();
            return walkInPatient.Id;
        }

        private ConsultationPatientDto BuildConsultationPatient(Appointment appointment)
        {
            if (appointment.FamilyMemberId.HasValue && appointment.FamilyMember != null)
            {
                var fm = appointment.FamilyMember;
                return new ConsultationPatientDto
                {
                    PatientId = appointment.PatientId,
                    FamilyMemberId = fm.Id,
                    FullName = fm.Name,
                    Age = fm.Age,
                    Gender = fm.Gender.ToString(),
                    BloodType = fm.BloodType,
                    ChronicConditions = SplitCsv(fm.ChronicDiseases),
                    Allergies = SplitCsv(fm.Allergies),
                    IsFamilyMember = true
                };
            }

            if (appointment.PatientId.HasValue && appointment.Patient?.User != null)
            {
                var patient = appointment.Patient;
                var user = patient.User;
                int age = 0;
                if (user.DateOfBirth.HasValue)
                {
                    age = DateTime.Today.Year - user.DateOfBirth.Value.Year;
                    if (user.DateOfBirth.Value.Date > DateTime.Today.AddYears(-age)) age--;
                }

                return new ConsultationPatientDto
                {
                    PatientId = patient.Id,
                    FullName = user.FullName,
                    ProfileImageUrl = user.ProfileImageUrl,
                    Age = age,
                    Gender = user.Gender.ToString(),
                    BloodType = patient.BloodType,
                    ChronicConditions = SplitCsv(patient.ChronicDiseases),
                    Allergies = SplitCsv(patient.Allergies),
                    IsFamilyMember = false
                };
            }

            return new ConsultationPatientDto
            {
                FullName = appointment.OfflinePatientName ?? "Walk-in patient",
                Age = appointment.OfflinePatientAge ?? 0,
                Gender = appointment.OfflinePatientGender?.ToString(),
                IsFamilyMember = false
            };
        }

        private async Task<List<MedicalRecordDto>> LoadMedicalHistoryAsync(Appointment appointment)
        {
            IQueryable<MedicalRecord> query = _unitOfWork.MedicalRecords.Query()
                .Include(r => r.Patient).ThenInclude(p => p!.User)
                .Include(r => r.FamilyMember)
                .Include(r => r.Doctor).ThenInclude(d => d.User);

            if (appointment.FamilyMemberId.HasValue)
            {
                query = query.Where(r => r.FamilyMemberId == appointment.FamilyMemberId);
            }
            else if (appointment.PatientId.HasValue)
            {
                query = query.Where(r => r.PatientId == appointment.PatientId && r.FamilyMemberId == null);
            }
            else
            {
                return new List<MedicalRecordDto>();
            }

            var records = await query.OrderByDescending(r => r.VisitDate).ToListAsync();
            return records.Select(MapMedicalRecordToDto).ToList();
        }

        private async Task<List<PreviousVisitDto>> LoadPreviousVisitsAsync(Appointment appointment)
        {
            var visitsQuery = _unitOfWork.Appointments.Query()
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.MedicalRecord)
                .Where(a => a.Status == AppointmentStatus.Completed && a.Id != appointment.Id);

            if (appointment.FamilyMemberId.HasValue)
                visitsQuery = visitsQuery.Where(a => a.FamilyMemberId == appointment.FamilyMemberId);
            else if (appointment.PatientId.HasValue)
                visitsQuery = visitsQuery.Where(a => a.PatientId == appointment.PatientId && a.FamilyMemberId == null);
            else
                return new List<PreviousVisitDto>();

            var visits = await visitsQuery
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();

            return visits.Select(a => new PreviousVisitDto
            {
                AppointmentId = a.Id,
                VisitDate = a.AppointmentDate.Date.Add(a.StartTime),
                DoctorName = a.Doctor.User.FullName,
                Diagnosis = a.MedicalRecord?.Diagnosis,
                ChiefComplaint = a.ChiefComplaint
            }).ToList();
        }

        private async Task SendConsultationCompletedNotificationAsync(Appointment appointment, CompleteConsultationDto dto)
        {
            if (!appointment.PatientId.HasValue || appointment.Patient?.User == null)
                return;

            var medicationsText = "None";
            if (dto.Medications != null && dto.Medications.Any())
            {
                medicationsText = string.Join(", ",
                    dto.Medications.Select(m => $"{m.Name} ({m.Dosage})"));
            }

            var dosageText = dto.Medications != null && dto.Medications.Any()
                ? string.Join(", ", dto.Medications.Select(m => m.Dosage))
                : "N/A";

            var message = $"Diagnosis: {dto.Diagnosis}\n" +
                          $"Medications: {medicationsText}\n" +
                          $"Dosage: {dosageText}\n" +
                          $"Instructions: {dto.Instructions ?? "None"}";

            var notification = new Notification
            {
                UserId = appointment.Patient.UserId,
                Title = "Consultation completed",
                Message = message
            };

            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.CompleteAsync();
        }

        private static List<string> SplitCsv(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        // ===== Helpers Mappers =====
        private AppointmentDto MapAppointmentToDto(Appointment app)
        {
            var activeClinicLink = app.Doctor?.DoctorClinics?.FirstOrDefault(dc => dc.IsActive);
            var clinic = activeClinicLink?.Clinic;

            return new AppointmentDto
            {
                Id = app.Id,
                PatientId = app.PatientId,
                PatientName = app.PatientId.HasValue
                    ? (app.Patient?.User?.FullName ?? string.Empty)
                    : (app.OfflinePatientName ?? "Unregistered patient"),
                OfflinePatientPhone = app.OfflinePatientPhone,
                DoctorId = app.DoctorId,
                DoctorName = app.Doctor?.User?.FullName ?? string.Empty,
                Specialization = app.Doctor?.Specialization ?? string.Empty,
                AppointmentDate = app.AppointmentDate,
                StartTime = app.StartTime,
                EndTime = app.EndTime,
                Status = app.Status,
                StatusText = app.Status.ToString(),
                QueueNumber = app.QueueNumber,
                QueueStatus = app.QueueStatus,
                RefundStatus = app.RefundStatus,
                RefundStatusText = app.RefundStatus.ToString(),
                Notes = app.Notes,
                CancellationReason = app.CancellationReason,
                DoctorProfileImageUrl = app.Doctor?.User?.ProfileImageUrl,
                ClinicId = clinic?.Id,
                ClinicName = clinic?.Name,
                ClinicAddress = clinic != null ? $"{clinic.Area}, {clinic.Government}" : null,
                IsEmergency = app.IsEmergency,
                ChiefComplaint = app.ChiefComplaint,
                FamilyMemberId = app.FamilyMemberId,
                FamilyMemberName = app.FamilyMember?.Name,
                IsPaid = app.IsPaid,
                PaymentStatus = app.PaymentStatus,
                PaymentStatusText = app.PaymentStatus.ToString(),
                ConsultationFee = app.ConsultationFee,
                PaymentMethod = app.PaymentMethod,
                PaymentMethodText = app.PaymentMethod.ToString(),
                OfflinePatientAge = app.OfflinePatientAge,
                OfflinePatientGender = app.OfflinePatientGender,
                CreatedAt = app.CreatedAt
            };
        }

        private MedicalRecordDto MapMedicalRecordToDto(MedicalRecord record)
        {
            var dto = new MedicalRecordDto
            {
                Id = record.Id,
                PatientId = record.PatientId,
                PatientName = record.FamilyMember?.Name ?? record.Patient?.User?.FullName ?? string.Empty,
                FamilyMemberId = record.FamilyMemberId,
                FamilyMemberName = record.FamilyMember?.Name,
                DoctorId = record.DoctorId,
                DoctorName = record.Doctor?.User?.FullName ?? string.Empty,
                DoctorSpecialization = record.Doctor?.Specialization ?? string.Empty,
                DoctorProfileImageUrl = record.Doctor?.User?.ProfileImageUrl ?? string.Empty,
                AppointmentId = record.AppointmentId,
                Diagnosis = record.Diagnosis,
                Prescription = record.Prescription,
                Instructions = record.Instructions,
                TreatmentPlan = record.TreatmentPlan,
                Notes = record.Notes,
                Symptoms = record.Symptoms,
                Subjective = record.Subjective,
                Objective = record.Objective,
                Assessment = record.Assessment,
                Plan = record.Plan,
                BloodPressure = record.BloodPressure,
                HeartRate = record.HeartRate,
                Weight = record.Weight,
                Observations = record.Notes,
                VisitDate = record.VisitDate,
                CreatedAt = record.CreatedAt
            };

            if (!string.IsNullOrWhiteSpace(record.Prescription))
            {
                var trim = record.Prescription.Trim();
                if (trim.StartsWith("[") && trim.EndsWith("]"))
                {
                    try
                    {
                        dto.Medications = JsonSerializer.Deserialize<List<PrescribedMedicationDto>>(record.Prescription, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch
                    {
                        dto.Medications = new List<PrescribedMedicationDto> { new PrescribedMedicationDto { Name = record.Prescription } };
                    }
                }
                else
                {
                    dto.Medications = new List<PrescribedMedicationDto> { new PrescribedMedicationDto { Name = record.Prescription } };
                }
            }
            return dto;
        }
    }
}
