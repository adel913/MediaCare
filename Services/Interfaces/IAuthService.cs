using MedicalApp.API.DTOs.Auth;
using MedicalApp.API.Helpers;

namespace MedicalApp.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<ApiResponse<AuthResponseDto>> RegisterPatientAsync(RegisterPatientDto dto);
        Task<ApiResponse<AuthResponseDto>> RegisterClinicAsync(RegisterClinicDto dto);
        Task<ApiResponse<AuthResponseDto>> RegisterDoctorAsync(RegisterDoctorDto dto);
        Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto dto);
        Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto dto);
        Task<ApiResponse> LogoutAsync(int userId, LogoutRequestDto dto);
        Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordDto dto);
        Task<ApiResponse> VerifyOtpAsync(VerifyOtpDto dto);
        Task<ApiResponse> ResetPasswordAsync(ResetPasswordDto dto);
        Task<ApiResponse<AuthResponseDto>> SocialLoginAsync(SocialLoginDto dto);
    }
}
