using System.ComponentModel.DataAnnotations;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.DTOs.Auth
{
    // ===== Login DTO (by Phone) =====
    public class LoginDto
    {
        [Required(ErrorMessage = "Phone number is required")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
    }

    // ===== Register Patient =====
    public class RegisterPatientDto
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Age is required")]
        public int Age { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password confirmation is required")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // ===== Register Clinic =====
    public class RegisterClinicDto
    {
        [Required(ErrorMessage = "Clinic name is required")]
        [MaxLength(200)]
        public string ClinicName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? LinkMap { get; set; }

        [Required(ErrorMessage = "Government is required")]
        [MaxLength(100)]
        public string Government { get; set; } = string.Empty;

        [Required(ErrorMessage = "Area is required")]
        [MaxLength(100)]
        public string Area { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(100)]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string? Email { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password confirmation is required")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Clinic license is required")]
        [MaxLength(500)]
        public string LicenseFileUrl { get; set; } = string.Empty;

        public TimeSpan? OpeningTime { get; set; }
        public TimeSpan? ClosingTime { get; set; }
    }

    // ===== Register Doctor =====
    public class RegisterDoctorDto
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password confirmation is required")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Specialization is required")]
        [MaxLength(100)]
        public string Specialization { get; set; } = string.Empty;

        [Required(ErrorMessage = "Doctor license is required")]
        [MaxLength(500)]
        public string LicenseFileUrl { get; set; } = string.Empty;
    }

    // ===== Forgot Password =====
    public class ForgotPasswordDto
    {
        [Required(ErrorMessage = "Phone number is required")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;
    }

    // ===== Verify OTP =====
    public class VerifyOtpDto
    {
        [Required(ErrorMessage = "Phone number is required")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "OTP code is required")]
        [MaxLength(6)]
        public string OtpCode { get; set; } = string.Empty;
    }

    // ===== Reset Password =====
    public class ResetPasswordDto
    {
        [Required(ErrorMessage = "Phone number is required")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "OTP code is required")]
        [MaxLength(6)]
        public string OtpCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "New password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password confirmation is required")]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // ===== Social Login =====
    public class SocialLoginDto
    {
        [Required]
        public string Provider { get; set; } = string.Empty; // "Google", "Apple", "Facebook"

        [Required]
        public string AccessToken { get; set; } = string.Empty;
    }

    public class RefreshTokenRequestDto
    {
        [Required(ErrorMessage = "Refresh token is required")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class LogoutRequestDto
    {
        [MaxLength(2000)]
        public string? RefreshToken { get; set; }
    }

    // ===== Auth Response =====
    public class AuthResponseDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime TokenExpiration { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiration { get; set; }
        public int? ProfileId { get; set; }
    }

    public class RegisterTelegramDto
    {
        [Required(ErrorMessage = "Phone number is required")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telegram Chat ID is required")]
        public string TelegramChatId { get; set; } = string.Empty;
    }
}
