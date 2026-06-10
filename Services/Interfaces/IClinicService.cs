using MedicalApp.API.DTOs.Clinic;
using MedicalApp.API.DTOs.Doctor;
using MedicalApp.API.Helpers;

namespace MedicalApp.API.Services.Interfaces
{
    public interface IClinicService
    {
        Task<ApiResponse<List<ClinicDto>>> GetAllClinicsAsync(string? search = null);
        Task<ApiResponse<List<NearbyClinicDto>>> GetNearbyClinicsAsync(
            double lat,
            double lng,
            double radiusKm = 5,
            string? specialization = null,
            string? search = null);
        Task<ApiResponse<ClinicDto>> GetClinicByIdAsync(int clinicId);
        Task<ApiResponse<ClinicDto>> CreateClinicAsync(int userId, CreateClinicDto dto);
        Task<ApiResponse<ClinicDto>> UpdateClinicAsync(int clinicId, int userId, UpdateClinicDto dto);
        Task<ApiResponse<List<DoctorListItemDto>>> GetClinicDoctorsAsync(int clinicAdminUserId);
        Task<ApiResponse<ClinicDto>> GetClinicProfileAsync(int clinicAdminUserId);
        Task<ApiResponse<ClinicDto>> UpdateClinicProfileAsync(int clinicAdminUserId, UpdateClinicDto dto);
        Task<ApiResponse<ScannedDoctorDto>> ScanDoctorQrAsync(int clinicAdminUserId, string qrCodeKey);
        Task<ApiResponse<ClinicDoctorDetailsDto>> RegisterClinicDoctorAsync(int clinicAdminUserId, UpdateClinicDoctorDto dto);
        Task<ApiResponse<ClinicDoctorDetailsDto>> GetClinicDoctorDetailsAsync(int clinicAdminUserId, int doctorId);
        Task<ApiResponse<ClinicDoctorDetailsDto>> UpdateClinicDoctorAsync(int clinicAdminUserId, int doctorId, UpdateClinicDoctorDto dto);
        Task<ApiResponse<bool>> RemoveClinicDoctorAsync(int clinicAdminUserId, int doctorId);
    }
}
