using Microsoft.EntityFrameworkCore;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.DTOs.Doctor;
using MedicalApp.API.DTOs.Patient;
using MedicalApp.API.Helpers;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Services.Implementations
{
    public class PatientService : IPatientService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PatientService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<PatientProfileDto>> GetProfileAsync(int userId)
        {
            var patient = await _unitOfWork.Patients.Query()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return ApiResponse<PatientProfileDto>.Failure("Profile not found", 404);

            return ApiResponse<PatientProfileDto>.Success(MapToDto(patient));
        }

        public async Task<ApiResponse<PatientProfileDto>> UpdateProfileAsync(int userId, UpdatePatientProfileDto dto)
        {
            var patient = await _unitOfWork.Patients.Query()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return ApiResponse<PatientProfileDto>.Failure("Profile not found", 404);

            // Update user fields
            if (!string.IsNullOrEmpty(dto.FullName)) patient.User.FullName = dto.FullName;
            if (!string.IsNullOrEmpty(dto.PhoneNumber)) patient.User.PhoneNumber = dto.PhoneNumber;
            if (dto.Email != null) patient.User.Email = dto.Email;
            if (dto.Gender.HasValue) patient.User.Gender = dto.Gender;
            if (dto.Age.HasValue) patient.User.Age = dto.Age;
            if (dto.DateOfBirth.HasValue) patient.User.DateOfBirth = dto.DateOfBirth;
            if (dto.ProfileImageUrl != null) patient.User.ProfileImageUrl = dto.ProfileImageUrl;

            // Update patient fields
            if (dto.Address != null) patient.Address = dto.Address;
            if (dto.BloodType != null) patient.BloodType = dto.BloodType;
            if (dto.Allergies != null) patient.Allergies = dto.Allergies;
            if (dto.ChronicDiseases != null) patient.ChronicDiseases = dto.ChronicDiseases;
            if (dto.EmergencyContactName != null) patient.EmergencyContactName = dto.EmergencyContactName;
            if (dto.EmergencyContactPhone != null) patient.EmergencyContactPhone = dto.EmergencyContactPhone;

            _unitOfWork.Patients.Update(patient);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<PatientProfileDto>.Success(MapToDto(patient), "Profile updated successfully");
        }

        private static PatientProfileDto MapToDto(Models.Entities.Patient patient) => new()
        {
            Id = patient.Id,
            UserId = patient.UserId,
            FullName = patient.User.FullName,
            PhoneNumber = patient.User.PhoneNumber,
            Email = patient.User.Email,
            Gender = patient.User.Gender,
            Age = patient.User.Age,
            DateOfBirth = patient.User.DateOfBirth,
            ProfileImageUrl = patient.User.ProfileImageUrl,
            Address = patient.Address,
            BloodType = patient.BloodType,
            Allergies = patient.Allergies,
            ChronicDiseases = patient.ChronicDiseases,
            EmergencyContactName = patient.EmergencyContactName,
            EmergencyContactPhone = patient.EmergencyContactPhone
        };

        public async Task<ApiResponse<bool>> ToggleFavoriteDoctorAsync(int userId, int doctorId)
        {
            var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null)
                return ApiResponse<bool>.Failure("Patient not found", 404);

            var doctorExists = await _unitOfWork.Doctors.AnyAsync(d => d.Id == doctorId);
            if (!doctorExists)
                return ApiResponse<bool>.Failure("Doctor not found", 404);

            var favorite = await _unitOfWork.PatientFavorites.Query()
                .FirstOrDefaultAsync(pf => pf.PatientId == patient.Id && pf.DoctorId == doctorId);

            if (favorite != null)
            {
                _unitOfWork.PatientFavorites.Remove(favorite);
                await _unitOfWork.CompleteAsync();
                return ApiResponse<bool>.Success(false, "Doctor removed from favorites");
            }
            else
            {
                var newFavorite = new Models.Entities.PatientFavorite
                {
                    PatientId = patient.Id,
                    DoctorId = doctorId
                };
                await _unitOfWork.PatientFavorites.AddAsync(newFavorite);
                await _unitOfWork.CompleteAsync();
                return ApiResponse<bool>.Success(true, "Doctor added to favorites");
            }
        }

        public async Task<ApiResponse<List<DoctorListItemDto>>> GetFavoritesAsync(int userId)
        {
            var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null)
                return ApiResponse<List<DoctorListItemDto>>.Failure("Patient not found", 404);

            var favorites = await _unitOfWork.PatientFavorites.Query()
                .Include(pf => pf.Doctor)
                    .ThenInclude(d => d.User)
                .Include(pf => pf.Doctor)
                    .ThenInclude(d => d.DoctorClinics)
                        .ThenInclude(dc => dc.Clinic)
                .Where(pf => pf.PatientId == patient.Id)
                .Select(pf => new DoctorListItemDto
                {
                    Id = pf.Doctor.Id,
                    FullName = pf.Doctor.User.FullName,
                    Specialization = pf.Doctor.Specialization,
                    ProfileImageUrl = pf.Doctor.User.ProfileImageUrl,
                    ConsultationFee = pf.Doctor.DoctorClinics
                        .Where(dc => dc.IsActive)
                        .Select(dc => dc.ConsultationFee)
                        .FirstOrDefault() ?? pf.Doctor.ConsultationFee,
                    AverageRating = pf.Doctor.AverageRating,
                    TotalReviews = pf.Doctor.TotalReviews,
                    IsAvailable = pf.Doctor.IsAvailable,
                    ClinicName = pf.Doctor.DoctorClinics
                        .Where(dc => dc.IsActive)
                        .Select(dc => dc.Clinic.Name)
                        .FirstOrDefault(),
                    ClinicArea = pf.Doctor.DoctorClinics
                        .Where(dc => dc.IsActive)
                        .Select(dc => dc.Clinic.Area)
                        .FirstOrDefault(),
                    IsFavorited = true
                })
                .ToListAsync();

            return ApiResponse<List<DoctorListItemDto>>.Success(favorites, "Favorites retrieved successfully");
        }

        public async Task<ApiResponse<List<PatientProfileDto>>> SearchPatientsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return ApiResponse<List<PatientProfileDto>>.Success(new List<PatientProfileDto>());

            var patients = await _unitOfWork.Patients.Query()
                .Include(p => p.User)
                .Where(p => p.User.FullName.Contains(query) 
                    || p.User.PhoneNumber.Contains(query) 
                    || p.Id.ToString() == query)
                .Take(20)
                .Select(p => new PatientProfileDto
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    FullName = p.User.FullName,
                    PhoneNumber = p.User.PhoneNumber,
                    Email = p.User.Email,
                    Gender = p.User.Gender,
                    Age = p.User.Age,
                    DateOfBirth = p.User.DateOfBirth,
                    ProfileImageUrl = p.User.ProfileImageUrl,
                    Address = p.Address,
                    BloodType = p.BloodType,
                    Allergies = p.Allergies,
                    ChronicDiseases = p.ChronicDiseases,
                    EmergencyContactName = p.EmergencyContactName,
                    EmergencyContactPhone = p.EmergencyContactPhone
                })
                .ToListAsync();

            return ApiResponse<List<PatientProfileDto>>.Success(patients, "Search results retrieved successfully");
        }
        public async Task<ApiResponse<List<FamilyMemberDto>>> GetFamilyMembersAsync(int userId)
        {
            var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null) return ApiResponse<List<FamilyMemberDto>>.Failure("Patient not found", 404);

            var members = await _unitOfWork.FamilyMembers.Query()
                .Where(fm => fm.PatientId == patient.Id)
                .Select(fm => new FamilyMemberDto
                {
                    Id = fm.Id,
                    PatientId = fm.PatientId,
                    Name = fm.Name,
                    Relation = fm.Relation,
                    Age = fm.Age,
                    Gender = fm.Gender,
                    BloodType = fm.BloodType,
                    MedicalHistory = fm.MedicalHistory,
                    Allergies = fm.Allergies,
                    ChronicDiseases = fm.ChronicDiseases
                }).ToListAsync();

            return ApiResponse<List<FamilyMemberDto>>.Success(members);
        }

        public async Task<ApiResponse<FamilyMemberDto>> AddFamilyMemberAsync(int userId, CreateFamilyMemberDto dto)
        {
            var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null) return ApiResponse<FamilyMemberDto>.Failure("Patient not found", 404);

            var newMember = new Models.Entities.FamilyMember
            {
                PatientId = patient.Id,
                Name = dto.Name,
                Relation = dto.Relation,
                Age = dto.Age,
                Gender = dto.Gender,
                BloodType = dto.BloodType,
                MedicalHistory = dto.MedicalHistory,
                Allergies = dto.Allergies,
                ChronicDiseases = dto.ChronicDiseases
            };

            await _unitOfWork.FamilyMembers.AddAsync(newMember);
            await _unitOfWork.CompleteAsync();

            var resultDto = new FamilyMemberDto
            {
                Id = newMember.Id,
                PatientId = newMember.PatientId,
                Name = newMember.Name,
                Relation = newMember.Relation,
                Age = newMember.Age,
                Gender = newMember.Gender,
                BloodType = newMember.BloodType,
                MedicalHistory = newMember.MedicalHistory,
                Allergies = newMember.Allergies,
                ChronicDiseases = newMember.ChronicDiseases
            };

            return ApiResponse<FamilyMemberDto>.Success(resultDto, "Family member added successfully");
        }

        public async Task<ApiResponse<bool>> RemoveFamilyMemberAsync(int userId, int memberId)
        {
            var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null) return ApiResponse<bool>.Failure("Patient not found", 404);

            var member = await _unitOfWork.FamilyMembers.Query()
                .FirstOrDefaultAsync(fm => fm.Id == memberId && fm.PatientId == patient.Id);

            if (member == null) return ApiResponse<bool>.Failure("Family member not found or you do not have permission to delete", 404);

            _unitOfWork.FamilyMembers.Remove(member);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<bool>.Success(true, "Family member deleted successfully");
        }
    }
}
