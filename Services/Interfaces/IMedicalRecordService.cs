using MedicalApp.API.DTOs.MedicalRecord;
using MedicalApp.API.Helpers;

namespace MedicalApp.API.Services.Interfaces
{
    public interface IMedicalRecordService
    {
        Task<ApiResponse<MedicalRecordDto>> CreateRecordAsync(int userId, CreateMedicalRecordDto dto);
        Task<ApiResponse<List<MedicalRecordDto>>> GetPatientRecordsAsync(int patientId, int userId);
        Task<ApiResponse<MedicalRecordDto>> GetRecordByIdAsync(int recordId, int userId);
    }
}
