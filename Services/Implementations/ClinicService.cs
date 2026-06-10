using Microsoft.EntityFrameworkCore;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.DTOs.Clinic;
using MedicalApp.API.DTOs.Doctor;
using MedicalApp.API.Helpers;
using MedicalApp.API.Models.Enums;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Services.Implementations
{
    public class ClinicService : IClinicService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ClinicService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<List<ClinicDto>>> GetAllClinicsAsync(string? search = null)
        {
            var query = _unitOfWork.Clinics.Query().Where(c => c.IsActive).AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(c => c.Name.Contains(search)
                    || (c.Government != null && c.Government.Contains(search))
                    || (c.Area != null && c.Area.Contains(search)));

            var clinics = await query.Select(c => new ClinicDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Government = c.Government,
                Area = c.Area,
                Address = c.Address,
                LinkMap = c.LinkMap,
                PhoneNumber = c.PhoneNumber,
                Email = c.Email,
                LogoUrl = c.LogoUrl,
                LicenseImageUrl = c.LicenseImageUrl,
                Latitude = c.Latitude,
                Longitude = c.Longitude,
                OpeningTime = c.OpeningTime,
                ClosingTime = c.ClosingTime,
                IsActive = c.IsActive,
                DoctorsCount = c.DoctorClinics.Count(dc => dc.IsActive)
            }).ToListAsync();

            return ApiResponse<List<ClinicDto>>.Success(clinics);
        }

        public async Task<ApiResponse<List<NearbyClinicDto>>> GetNearbyClinicsAsync(
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
                return ApiResponse<List<NearbyClinicDto>>.Success(new List<NearbyClinicDto>());
            }

            var query = _unitOfWork.Clinics.Query()
                .Include(c => c.DoctorClinics).ThenInclude(dc => dc.Doctor)
                .Where(c => c.IsActive && c.Latitude != null && c.Longitude != null);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(c => c.Name.Contains(search)
                    || (c.Government != null && c.Government.Contains(search))
                    || (c.Area != null && c.Area.Contains(search)));

            if (!string.IsNullOrEmpty(specialization))
                query = query.Where(c => c.DoctorClinics.Any(dc =>
                    dc.IsActive && dc.Doctor.Specialization == specialization));

            var clinics = await query.ToListAsync();

            var result = clinics
                .Select(c => new NearbyClinicDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    FacilityId = c.FacilityId,
                    Description = c.Description,
                    Government = c.Government,
                    Area = c.Area,
                    Address = c.Address,
                    LinkMap = c.LinkMap,
                    PhoneNumber = c.PhoneNumber,
                    Email = c.Email,
                    LogoUrl = c.LogoUrl,
                    LicenseImageUrl = c.LicenseImageUrl,
                    Latitude = c.Latitude,
                    Longitude = c.Longitude,
                    OpeningTime = c.OpeningTime,
                    ClosingTime = c.ClosingTime,
                    IsActive = c.IsActive,
                    DoctorsCount = c.DoctorClinics?.Count(dc => dc.IsActive) ?? 0,
                    DistanceKm = GeoUtils.HaversineKm(lat, lng, c.Latitude!.Value, c.Longitude!.Value),
                    MatchingDoctorsCount = c.DoctorClinics?.Count(dc =>
                        dc.IsActive && (specialization == null || dc.Doctor.Specialization == specialization)) ?? 0
                })
                .Where(c => c.DistanceKm <= radiusKm)
                .OrderBy(c => c.DistanceKm)
                .ToList();

            return ApiResponse<List<NearbyClinicDto>>.Success(result);
        }

        public async Task<ApiResponse<ClinicDto>> GetClinicByIdAsync(int clinicId)
        {
            var clinic = await _unitOfWork.Clinics.Query()
                .Include(c => c.DoctorClinics)
                .FirstOrDefaultAsync(c => c.Id == clinicId);

            if (clinic == null)
                return ApiResponse<ClinicDto>.Failure("Clinic not found", 404);

            return ApiResponse<ClinicDto>.Success(MapToDto(clinic));
        }

        public async Task<ApiResponse<ClinicDto>> CreateClinicAsync(int userId, CreateClinicDto dto)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null || user.Role != UserRole.ClinicAdmin)
                return ApiResponse<ClinicDto>.Failure("You are not authorized to create a clinic");

            var clinic = new Models.Entities.Clinic
            {
                Name = dto.Name,
                FacilityId = dto.FacilityId,
                Description = dto.Description,
                Government = dto.Government,
                Area = dto.Area,
                Address = dto.Address,
                LinkMap = dto.LinkMap,
                PhoneNumber = dto.PhoneNumber,
                Email = dto.Email,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                OpeningTime = dto.OpeningTime,
                ClosingTime = dto.ClosingTime
            };

            await _unitOfWork.Clinics.AddAsync(clinic);
            await _unitOfWork.CompleteAsync();

            var clinicAdmin = new Models.Entities.ClinicAdmin
            {
                UserId = userId,
                ClinicId = clinic.Id,
                Position = "Clinic Manager"
            };

            await _unitOfWork.ClinicAdmins.AddAsync(clinicAdmin);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<ClinicDto>.Success(MapToDto(clinic), "Clinic created successfully", 201);
        }

        public async Task<ApiResponse<ClinicDto>> UpdateClinicAsync(int clinicId, int userId, UpdateClinicDto dto)
        {
            var isAdmin = await _unitOfWork.ClinicAdmins.Query()
                .AnyAsync(ca => ca.ClinicId == clinicId && ca.UserId == userId);

            if (!isAdmin)
                return ApiResponse<ClinicDto>.Failure("You are not authorized to update this clinic", 403);

            var clinic = await _unitOfWork.Clinics.Query()
                .Include(c => c.DoctorClinics)
                .FirstOrDefaultAsync(c => c.Id == clinicId);

            if (clinic == null)
                return ApiResponse<ClinicDto>.Failure("Clinic not found", 404);

            if (!string.IsNullOrEmpty(dto.Name)) clinic.Name = dto.Name;
            if (dto.FacilityId != null) clinic.FacilityId = dto.FacilityId;
            if (dto.Description != null) clinic.Description = dto.Description;
            if (!string.IsNullOrEmpty(dto.Government)) clinic.Government = dto.Government;
            if (!string.IsNullOrEmpty(dto.Area)) clinic.Area = dto.Area;
            if (dto.Address != null) clinic.Address = dto.Address;
            if (dto.LinkMap != null) clinic.LinkMap = dto.LinkMap;
            if (dto.PhoneNumber != null) clinic.PhoneNumber = dto.PhoneNumber;
            if (dto.Email != null) clinic.Email = dto.Email;
            if (dto.Latitude.HasValue) clinic.Latitude = dto.Latitude;
            if (dto.Longitude.HasValue) clinic.Longitude = dto.Longitude;
            if (dto.OpeningTime.HasValue) clinic.OpeningTime = dto.OpeningTime;
            if (dto.ClosingTime.HasValue) clinic.ClosingTime = dto.ClosingTime;

            _unitOfWork.Clinics.Update(clinic);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<ClinicDto>.Success(MapToDto(clinic), "Clinic details updated successfully");
        }

        private static ClinicDto MapToDto(Models.Entities.Clinic clinic) => new()
        {
            Id = clinic.Id,
            Name = clinic.Name,
            FacilityId = clinic.FacilityId,
            Description = clinic.Description,
            Government = clinic.Government,
            Area = clinic.Area,
            Address = clinic.Address,
            LinkMap = clinic.LinkMap,
            PhoneNumber = clinic.PhoneNumber,
            Email = clinic.Email,
            LogoUrl = clinic.LogoUrl,
            LicenseImageUrl = clinic.LicenseImageUrl,
            Latitude = clinic.Latitude,
            Longitude = clinic.Longitude,
            OpeningTime = clinic.OpeningTime,
            ClosingTime = clinic.ClosingTime,
            IsActive = clinic.IsActive,
            DoctorsCount = clinic.DoctorClinics?.Count(dc => dc.IsActive) ?? 0
        };

        public async Task<ApiResponse<List<DoctorListItemDto>>> GetClinicDoctorsAsync(int clinicAdminUserId)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(ca => ca.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<List<DoctorListItemDto>>.Failure("You are not authorized to fetch clinic doctors as an admin", 403);

            int clinicId = admin.ClinicId;

            var doctors = await _unitOfWork.DoctorClinics.Query()
                .Include(dc => dc.Doctor)
                    .ThenInclude(d => d.User)
                .Where(dc => dc.ClinicId == clinicId && dc.IsActive && dc.Doctor.User.IsActive)
                .Select(dc => new DoctorListItemDto
                {
                    Id = dc.Doctor.Id,
                    FullName = dc.Doctor.User.FullName,
                    Specialization = dc.Doctor.Specialization,
                    ProfileImageUrl = dc.Doctor.User.ProfileImageUrl,
                    ConsultationFee = dc.ConsultationFee ?? dc.Doctor.ConsultationFee,
                    AverageRating = dc.Doctor.AverageRating,
                    IsAvailable = dc.IsAvailable,
                    ClinicName = dc.Clinic.Name,
                    ClinicArea = dc.Clinic.Area
                })
                .ToListAsync();

            return ApiResponse<List<DoctorListItemDto>>.Success(doctors, "Clinic doctors retrieved successfully");
        }

        public async Task<ApiResponse<ClinicDto>> GetClinicProfileAsync(int clinicAdminUserId)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(ca => ca.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<ClinicDto>.Failure("You are not authorized to fetch clinic details as an admin", 403);

            return await GetClinicByIdAsync(admin.ClinicId);
        }

        public async Task<ApiResponse<ClinicDto>> UpdateClinicProfileAsync(int clinicAdminUserId, UpdateClinicDto dto)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(ca => ca.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<ClinicDto>.Failure("You are not authorized to update clinic details as an admin", 403);

            return await UpdateClinicAsync(admin.ClinicId, clinicAdminUserId, dto);
        }

        // ===== Doctor Management =====
        public async Task<ApiResponse<ScannedDoctorDto>> ScanDoctorQrAsync(int clinicAdminUserId, string qrCodeKey)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(ca => ca.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<ScannedDoctorDto>.Failure("You are not authorized to scan the code as an admin", 403);

            var doctor = await _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.QrCodeKey == qrCodeKey);

            if (doctor == null)
                return ApiResponse<ScannedDoctorDto>.Failure("Doctor is not registered in the system", 404);

            var isRegistered = await _unitOfWork.DoctorClinics.Query()
                .AnyAsync(dc => dc.ClinicId == admin.ClinicId && dc.DoctorId == doctor.Id && dc.IsActive);

            var dto = new ScannedDoctorDto
            {
                DoctorId = doctor.Id,
                FullName = doctor.User.FullName,
                Specialization = doctor.Specialization,
                ProfileImageUrl = doctor.User.ProfileImageUrl,
                YearsOfExperience = doctor.YearsOfExperience,
                Bio = doctor.Bio,
                DefaultConsultationFee = doctor.ConsultationFee,
                IsAlreadyRegisteredInClinic = isRegistered,
                QrCodeKey = doctor.QrCodeKey
            };

            return ApiResponse<ScannedDoctorDto>.Success(dto, "Doctor details read successfully");
        }

        public async Task<ApiResponse<ClinicDoctorDetailsDto>> RegisterClinicDoctorAsync(int clinicAdminUserId, UpdateClinicDoctorDto dto)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(ca => ca.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<ClinicDoctorDetailsDto>.Failure("You are not authorized to add a doctor as an admin", 403);

            var existingLink = await _unitOfWork.DoctorClinics.Query()
                .FirstOrDefaultAsync(dc => dc.ClinicId == admin.ClinicId && dc.DoctorId == dto.DoctorId);

            Models.Entities.Doctor? doctor;

            if (existingLink != null)
            {
                doctor = await _unitOfWork.Doctors.Query()
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.Id == dto.DoctorId);

                if (doctor == null)
                    return ApiResponse<ClinicDoctorDetailsDto>.Failure("Doctor is not found in the system", 404);

                existingLink.IsActive = true;
                existingLink.ConsultationFee = dto.ConsultationFee;
                existingLink.IsAvailable = dto.IsAvailable;
                existingLink.InternalNotes = dto.InternalNotes;
                _unitOfWork.DoctorClinics.Update(existingLink);
            }
            else
            {
                if (string.IsNullOrEmpty(dto.QrCodeKey))
                    return ApiResponse<ClinicDoctorDetailsDto>.Failure("Please provide the QR code key to register a new doctor", 400);

                doctor = await _unitOfWork.Doctors.Query()
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.Id == dto.DoctorId && d.QrCodeKey == dto.QrCodeKey);

                if (doctor == null)
                    return ApiResponse<ClinicDoctorDetailsDto>.Failure("Doctor details or verification key is incorrect", 404);

                var newLink = new Models.Entities.DoctorClinic
                {
                    ClinicId = admin.ClinicId,
                    DoctorId = dto.DoctorId,
                    ConsultationFee = dto.ConsultationFee,
                    IsAvailable = dto.IsAvailable,
                    InternalNotes = dto.InternalNotes,
                    IsActive = true
                };
                await _unitOfWork.DoctorClinics.AddAsync(newLink);
            }

            if (dto.Schedules.Count > 0)
            {
                var oldSchedules = await _unitOfWork.DoctorSchedules.Query()
                    .Where(s => s.ClinicId == admin.ClinicId && s.DoctorId == dto.DoctorId)
                    .ToListAsync();

                foreach (var os in oldSchedules)
                {
                    _unitOfWork.DoctorSchedules.Remove(os);
                }

                foreach (var sDto in dto.Schedules)
                {
                    var breakStart = string.IsNullOrEmpty(sDto.BreakStartTime) ? (TimeSpan?)null : TimeSpan.Parse(sDto.BreakStartTime);
                    var breakEnd = string.IsNullOrEmpty(sDto.BreakEndTime) ? (TimeSpan?)null : TimeSpan.Parse(sDto.BreakEndTime);

                    var newSchedule = new Models.Entities.DoctorSchedule
                    {
                        ClinicId = admin.ClinicId,
                        DoctorId = dto.DoctorId,
                        DayOfWeek = sDto.DayOfWeek,
                        StartTime = TimeSpan.Parse(sDto.StartTime),
                        EndTime = TimeSpan.Parse(sDto.EndTime),
                        BreakStartTime = breakStart,
                        BreakEndTime = breakEnd,
                        SlotDurationMinutes = sDto.SlotDurationMinutes,
                        MaxPatients = sDto.MaxPatients,
                        IsActive = sDto.IsActive
                    };
                    await _unitOfWork.DoctorSchedules.AddAsync(newSchedule);
                }
            }

            await _unitOfWork.CompleteAsync();

            return await GetClinicDoctorDetailsAsync(clinicAdminUserId, dto.DoctorId);
        }

        public async Task<ApiResponse<ClinicDoctorDetailsDto>> GetClinicDoctorDetailsAsync(int clinicAdminUserId, int doctorId)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(ca => ca.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<ClinicDoctorDetailsDto>.Failure("You are not authorized to fetch doctor details as an admin", 403);

            var link = await _unitOfWork.DoctorClinics.Query()
                .Include(dc => dc.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(dc => dc.ClinicId == admin.ClinicId && dc.DoctorId == doctorId && dc.IsActive);

            if (link == null)
                return ApiResponse<ClinicDoctorDetailsDto>.Failure("Doctor is not registered in this clinic", 404);

            var schedules = await _unitOfWork.DoctorSchedules.Query()
                .Where(s => s.ClinicId == admin.ClinicId && s.DoctorId == doctorId)
                .Select(s => new DoctorScheduleDto
                {
                    Id = s.Id,
                    DayOfWeek = s.DayOfWeek,
                    StartTime = s.StartTime.ToString(@"hh\:mm\:ss"),
                    EndTime = s.EndTime.ToString(@"hh\:mm\:ss"),
                    BreakStartTime = s.BreakStartTime.HasValue ? s.BreakStartTime.Value.ToString(@"hh\:mm\:ss") : null,
                    BreakEndTime = s.BreakEndTime.HasValue ? s.BreakEndTime.Value.ToString(@"hh\:mm\:ss") : null,
                    SlotDurationMinutes = s.SlotDurationMinutes,
                    MaxPatients = s.MaxPatients,
                    IsActive = s.IsActive
                })
                .ToListAsync();

            var dto = new ClinicDoctorDetailsDto
            {
                DoctorId = link.DoctorId,
                FullName = link.Doctor.User.FullName,
                Specialization = link.Doctor.Specialization,
                ProfileImageUrl = link.Doctor.User.ProfileImageUrl,
                ConsultationFee = link.ConsultationFee ?? link.Doctor.ConsultationFee,
                IsAvailable = link.IsAvailable,
                InternalNotes = link.InternalNotes,
                Schedules = schedules
            };

            return ApiResponse<ClinicDoctorDetailsDto>.Success(dto, "Doctor details retrieved successfully");
        }

        public async Task<ApiResponse<ClinicDoctorDetailsDto>> UpdateClinicDoctorAsync(int clinicAdminUserId, int doctorId, UpdateClinicDoctorDto dto)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(ca => ca.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<ClinicDoctorDetailsDto>.Failure("You are not authorized to update a doctor as an admin", 403);

            var link = await _unitOfWork.DoctorClinics.Query()
                .FirstOrDefaultAsync(dc => dc.ClinicId == admin.ClinicId && dc.DoctorId == doctorId && dc.IsActive);

            if (link == null)
                return ApiResponse<ClinicDoctorDetailsDto>.Failure("Doctor is not registered in this clinic", 404);

            // Only update the DoctorClinic link fields — never touch schedules.
            link.ConsultationFee = dto.ConsultationFee;
            link.IsAvailable = dto.IsAvailable;
            link.InternalNotes = dto.InternalNotes;
            _unitOfWork.DoctorClinics.Update(link);

            // If schedules are explicitly provided, update them too.
            if (dto.Schedules.Count > 0)
            {
                var oldSchedules = await _unitOfWork.DoctorSchedules.Query()
                    .Where(s => s.ClinicId == admin.ClinicId && s.DoctorId == doctorId)
                    .ToListAsync();

                foreach (var os in oldSchedules)
                {
                    _unitOfWork.DoctorSchedules.Remove(os);
                }

                foreach (var sDto in dto.Schedules)
                {
                    var breakStart = string.IsNullOrEmpty(sDto.BreakStartTime) ? (TimeSpan?)null : TimeSpan.Parse(sDto.BreakStartTime);
                    var breakEnd = string.IsNullOrEmpty(sDto.BreakEndTime) ? (TimeSpan?)null : TimeSpan.Parse(sDto.BreakEndTime);

                    var newSchedule = new Models.Entities.DoctorSchedule
                    {
                        ClinicId = admin.ClinicId,
                        DoctorId = doctorId,
                        DayOfWeek = sDto.DayOfWeek,
                        StartTime = TimeSpan.Parse(sDto.StartTime),
                        EndTime = TimeSpan.Parse(sDto.EndTime),
                        BreakStartTime = breakStart,
                        BreakEndTime = breakEnd,
                        SlotDurationMinutes = sDto.SlotDurationMinutes,
                        MaxPatients = sDto.MaxPatients,
                        IsActive = sDto.IsActive
                    };
                    await _unitOfWork.DoctorSchedules.AddAsync(newSchedule);
                }
            }

            await _unitOfWork.CompleteAsync();

            return await GetClinicDoctorDetailsAsync(clinicAdminUserId, doctorId);
        }

        public async Task<ApiResponse<bool>> RemoveClinicDoctorAsync(int clinicAdminUserId, int doctorId)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(ca => ca.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<bool>.Failure("You are not authorized to remove a doctor as an admin", 403);

            var link = await _unitOfWork.DoctorClinics.Query()
                .FirstOrDefaultAsync(dc => dc.ClinicId == admin.ClinicId && dc.DoctorId == doctorId);

            if (link == null || !link.IsActive)
                return ApiResponse<bool>.Failure("Doctor is not currently registered in this clinic", 404);

            link.IsActive = false;
            _unitOfWork.DoctorClinics.Update(link);

            // Also deactivate doctor's schedules for this clinic
            var schedules = await _unitOfWork.DoctorSchedules.Query()
                .Where(s => s.ClinicId == admin.ClinicId && s.DoctorId == doctorId)
                .ToListAsync();

            foreach (var s in schedules)
            {
                s.IsActive = false;
                _unitOfWork.DoctorSchedules.Update(s);
            }

            await _unitOfWork.CompleteAsync();
            return ApiResponse<bool>.Success(true, "Doctor removed from clinic successfully");
        }
    }
}
