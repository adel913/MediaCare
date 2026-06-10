using MedicalApp.API.DTOs.Doctor;
using MedicalApp.API.DTOs.Patient;
using MedicalApp.API.Helpers;

namespace MedicalApp.API.Services.Interfaces
{
    public interface IPatientService
    {
        Task<ApiResponse<PatientProfileDto>> GetProfileAsync(int userId);
        Task<ApiResponse<PatientProfileDto>> UpdateProfileAsync(int userId, UpdatePatientProfileDto dto);
        Task<ApiResponse<bool>> ToggleFavoriteDoctorAsync(int userId, int doctorId);
        Task<ApiResponse<List<DoctorListItemDto>>> GetFavoritesAsync(int userId);
        Task<ApiResponse<List<PatientProfileDto>>> SearchPatientsAsync(string query);
        Task<ApiResponse<List<FamilyMemberDto>>> GetFamilyMembersAsync(int userId);
        Task<ApiResponse<FamilyMemberDto>> AddFamilyMemberAsync(int userId, CreateFamilyMemberDto dto);
        Task<ApiResponse<bool>> RemoveFamilyMemberAsync(int userId, int memberId);
    }
}
