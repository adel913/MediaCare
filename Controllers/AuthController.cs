using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicalApp.API.DTOs.Auth;
using MedicalApp.API.Services.Interfaces;
namespace MedicalApp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseApiController
    {
        private readonly IAuthService _authService;
        private readonly ITelegramOtpService _telegramOtpService;

        public AuthController(IAuthService authService, ITelegramOtpService telegramOtpService)
        {
            _authService = authService;
            _telegramOtpService = telegramOtpService;
        }

        /// <summary>
        /// Register a new Patient account.
        /// </summary>
        [HttpPost("register/patient")]
        public async Task<IActionResult> RegisterPatient([FromBody] RegisterPatientDto dto)
        {
            var result = await _authService.RegisterPatientAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Register a new Clinic account.
        /// </summary>
        [HttpPost("register/clinic")]
        public async Task<IActionResult> RegisterClinic([FromBody] RegisterClinicDto dto)
        {
            var result = await _authService.RegisterClinicAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Register a new Doctor account.
        /// </summary>
        [HttpPost("register/doctor")]
        public async Task<IActionResult> RegisterDoctor([FromBody] RegisterDoctorDto dto)
        {
            var result = await _authService.RegisterDoctorAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _authService.LoginAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto)
        {
            var result = await _authService.RefreshTokenAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Logout and revoke refresh token(s).
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDto dto)
        {
            var result = await _authService.LogoutAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Send OTP to phone for password reset.
        /// </summary>
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var result = await _authService.ForgotPasswordAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            var result = await _authService.VerifyOtpAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var result = await _authService.ResetPasswordAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Login via social provider (Google, Apple, Facebook).
        /// </summary>
        [HttpPost("social-login")]
        public async Task<IActionResult> SocialLogin([FromBody] SocialLoginDto dto)
        {
            var result = await _authService.SocialLoginAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Register a Telegram Chat ID mapping to a phone number (100% Free OTP setup).
        /// </summary>
        [HttpPost("telegram-register")]
        public async Task<IActionResult> RegisterTelegram([FromBody] RegisterTelegramDto dto)
        {
            var result = await _telegramOtpService.RegisterChatIdAsync(dto.PhoneNumber, dto.TelegramChatId);
            return StatusCode(result.StatusCode, result);
        }
    }
}
