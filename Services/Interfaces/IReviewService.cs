using MedicalApp.API.DTOs.Review;
using MedicalApp.API.Helpers;

namespace MedicalApp.API.Services.Interfaces
{
    public interface IReviewService
    {
        Task<ApiResponse<ReviewDto>> CreateReviewAsync(int userId, CreateReviewDto dto);
        Task<ApiResponse<List<ReviewDto>>> GetDoctorReviewsAsync(int doctorId);
    }
}
