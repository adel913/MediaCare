using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.DTOs.Auth;
using MedicalApp.API.Helpers;
using MedicalApp.API.Models.Entities;
using MedicalApp.API.Models.Enums;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly ITelegramOtpService _telegramOtpService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUnitOfWork unitOfWork, 
            IConfiguration configuration, 
            ITelegramOtpService telegramOtpService,
            ILogger<AuthService> logger)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _telegramOtpService = telegramOtpService;
            _logger = logger;
        }

        // ===== Register Patient =====
        public async Task<ApiResponse<AuthResponseDto>> RegisterPatientAsync(RegisterPatientDto dto)
        {
            if (await PhoneExists(dto.Phone))
                return ApiResponse<AuthResponseDto>.Failure("Phone number already registered", 409);

            var user = new User
            {
                FullName = dto.Name,
                PhoneNumber = dto.Phone.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRole.Patient,
                Age = dto.Age
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CompleteAsync();

            var patient = new Patient { UserId = user.Id };
            await _unitOfWork.Patients.AddAsync(patient);
            await _unitOfWork.CompleteAsync();

            var refreshToken = GenerateRefreshToken();
            refreshToken.UserId = user.Id;
            await _unitOfWork.RefreshTokens.AddAsync(refreshToken);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<AuthResponseDto>.Success(
                BuildAuthResponse(user, patient.Id, refreshToken), "Account created successfully", 201);
        }

        // ===== Register Clinic =====
        public async Task<ApiResponse<AuthResponseDto>> RegisterClinicAsync(RegisterClinicDto dto)
        {
            if (await PhoneExists(dto.Phone))
                return ApiResponse<AuthResponseDto>.Failure("Phone number already registered", 409);

            // Create user with ClinicAdmin role
            var user = new User
            {
                FullName = dto.ClinicName,
                PhoneNumber = dto.Phone.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRole.ClinicAdmin
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CompleteAsync();

            // Create the clinic
            var clinic = new Clinic
            {
                Name = dto.ClinicName,
                Government = dto.Government,
                Area = dto.Area,
                Address = dto.Address,
                LinkMap = dto.LinkMap,
                PhoneNumber = dto.Phone,
                Email = dto.Email,
                LicenseImageUrl = dto.LicenseFileUrl,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                OpeningTime = dto.OpeningTime,
                ClosingTime = dto.ClosingTime
            };

            await _unitOfWork.Clinics.AddAsync(clinic);
            await _unitOfWork.CompleteAsync();

            // Link admin to clinic
            var clinicAdmin = new ClinicAdmin
            {
                UserId = user.Id,
                ClinicId = clinic.Id,
                Position = "Clinic Manager"
            };

            await _unitOfWork.ClinicAdmins.AddAsync(clinicAdmin);
            await _unitOfWork.CompleteAsync();

            var refreshToken = GenerateRefreshToken();
            refreshToken.UserId = user.Id;
            await _unitOfWork.RefreshTokens.AddAsync(refreshToken);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<AuthResponseDto>.Success(
                BuildAuthResponse(user, clinicAdmin.Id, refreshToken), "Clinic account created successfully", 201);
        }

        // ===== Register Doctor =====
        public async Task<ApiResponse<AuthResponseDto>> RegisterDoctorAsync(RegisterDoctorDto dto)
        {
            if (await PhoneExists(dto.Phone))
                return ApiResponse<AuthResponseDto>.Failure("Phone number already registered", 409);

            var user = new User
            {
                FullName = dto.Name,
                PhoneNumber = dto.Phone.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = UserRole.Doctor
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CompleteAsync();

            var doctor = new Doctor
            {
                UserId = user.Id,
                Specialization = dto.Specialization,
                LicenseImageUrl = dto.LicenseFileUrl
            };

            await _unitOfWork.Doctors.AddAsync(doctor);
            await _unitOfWork.CompleteAsync();

            var refreshToken = GenerateRefreshToken();
            refreshToken.UserId = user.Id;
            await _unitOfWork.RefreshTokens.AddAsync(refreshToken);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<AuthResponseDto>.Success(
                BuildAuthResponse(user, doctor.Id, refreshToken), "Doctor account created successfully", 201);
        }

        // ===== Login =====
        public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto dto)
        {
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.PhoneNumber == dto.Phone.Trim());

            if (user == null)
                return ApiResponse<AuthResponseDto>.Failure("Phone number or password is incorrect", 401);

            if (!user.IsActive)
                return ApiResponse<AuthResponseDto>.Failure("Account is disabled. Please contact support", 403);

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return ApiResponse<AuthResponseDto>.Failure("Phone number or password is incorrect", 401);

            int? profileId = await GetProfileId(user);
            var refreshToken = GenerateRefreshToken();
            refreshToken.UserId = user.Id;

            await _unitOfWork.RefreshTokens.AddAsync(refreshToken);
            await _unitOfWork.CompleteAsync();

            return ApiResponse<AuthResponseDto>.Success(
                BuildAuthResponse(user, profileId, refreshToken), "Signed in successfully");
        }

        public async Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto dto)
        {
            var refreshToken = await _unitOfWork.RefreshTokens.Query()
                .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow);

            if (refreshToken == null)
                return ApiResponse<AuthResponseDto>.Failure("Refresh token is invalid or expired", 401);

            var user = await _unitOfWork.Users.GetByIdAsync(refreshToken.UserId);
            if (user == null)
                return ApiResponse<AuthResponseDto>.Failure("User not found", 404);

            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;
            _unitOfWork.RefreshTokens.Update(refreshToken);

            var newRefreshToken = GenerateRefreshToken();
            newRefreshToken.UserId = user.Id;
            await _unitOfWork.RefreshTokens.AddAsync(newRefreshToken);
            await _unitOfWork.CompleteAsync();

            var profileId = await GetProfileId(user);
            return ApiResponse<AuthResponseDto>.Success(
                BuildAuthResponse(user, profileId, newRefreshToken), "Session refreshed successfully");
        }

        public async Task<ApiResponse> LogoutAsync(int userId, LogoutRequestDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.RefreshToken))
            {
                var token = await _unitOfWork.RefreshTokens.Query()
                    .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken && rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow);

                if (token != null)
                {
                    token.IsRevoked = true;
                    token.RevokedAt = DateTime.UtcNow;
                    _unitOfWork.RefreshTokens.Update(token);
                    await _unitOfWork.CompleteAsync();
                }

                return ApiResponse.Success("Signed out successfully");
            }

            var activeTokens = await _unitOfWork.RefreshTokens.Query()
                .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            if (activeTokens.Any())
            {
                foreach (var token in activeTokens)
                {
                    token.IsRevoked = true;
                    token.RevokedAt = DateTime.UtcNow;
                    _unitOfWork.RefreshTokens.Update(token);
                }

                await _unitOfWork.CompleteAsync();
            }

            return ApiResponse.Success("Signed out successfully");
        }

        private RefreshToken GenerateRefreshToken()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(64);
            var token = Convert.ToBase64String(randomBytes);

            return new RefreshToken
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays()),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = "system",
                IsRevoked = false
            };
        }

        // ===== Forgot Password (Send OTP via Telegram) =====
        public async Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordDto dto)
        {
            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.PhoneNumber == dto.Phone.Trim());

            if (user == null)
                return ApiResponse.Failure("Phone number is not registered", 404);

            // Generate 4-digit OTP
            var otp = new Random().Next(1000, 9999).ToString();

            // Invalidate old OTPs for this phone
            var oldOtps = await _unitOfWork.OtpCodes.Query()
                .Where(o => o.PhoneNumber == dto.Phone.Trim() && !o.IsUsed)
                .ToListAsync();
            foreach (var old in oldOtps) old.IsUsed = true;

            // Save new OTP
            var otpCode = new OtpCode
            {
                PhoneNumber = dto.Phone.Trim(),
                Code = otp,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };

            await _unitOfWork.OtpCodes.AddAsync(otpCode);
            await _unitOfWork.CompleteAsync();

            // Send OTP via Telegram Bot
            var telegramResult = await _telegramOtpService.SendOtpAsync(dto.Phone.Trim(), otp);
            if (!telegramResult.IsSuccess)
            {
                var botUsername = _configuration["TelegramSettings:BotUsername"] ?? "MedicalPlatformOtpBot";
                return ApiResponse.Failure(
                    $"Please activate the Telegram bot first to receive your verification code for free.\nOpen the bot @{botUsername}, press Start, and share your phone number.",
                    400,
                    new List<string> { "RequireTelegramActivation", botUsername }
                );
            }

            _logger.LogInformation("OTP sent via Telegram for phone ending in {PhoneSuffix}", dto.Phone.Trim().TakeLast(4));

            return ApiResponse.Success("Verification code sent successfully to your Telegram account");
        }

        // ===== Verify OTP =====
        public async Task<ApiResponse> VerifyOtpAsync(VerifyOtpDto dto)
        {
            var otpCode = await _unitOfWork.OtpCodes.Query()
                .Where(o => o.PhoneNumber == dto.Phone.Trim()
                    && o.Code == dto.OtpCode
                    && !o.IsUsed
                    && o.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otpCode == null)
                return ApiResponse.Failure("Verification code is incorrect or expired", 400);

            return ApiResponse.Success("Verification code is valid");
        }

        // ===== Reset Password =====
        public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordDto dto)
        {
            var otpCode = await _unitOfWork.OtpCodes.Query()
                .Where(o => o.PhoneNumber == dto.Phone.Trim()
                    && o.Code == dto.OtpCode
                    && !o.IsUsed
                    && o.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otpCode == null)
                return ApiResponse.Failure("Verification code is incorrect or expired", 400);

            var user = await _unitOfWork.Users.Query()
                .FirstOrDefaultAsync(u => u.PhoneNumber == dto.Phone.Trim());

            if (user == null)
                return ApiResponse.Failure("User not found", 404);

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            otpCode.IsUsed = true;

            await _unitOfWork.CompleteAsync();

            return ApiResponse.Success("Password changed successfully");
        }

        // ===== Social Login =====
        public async Task<ApiResponse<AuthResponseDto>> SocialLoginAsync(SocialLoginDto dto)
        {
            return ApiResponse<AuthResponseDto>.Failure(
                "Sign in via " + dto.Provider + " is not yet enabled, it will be available soon", 501);
        }

        // ===== Helper Methods =====
        private async Task<bool> PhoneExists(string phone)
        {
            return await _unitOfWork.Users.Query().AnyAsync(u => u.PhoneNumber == phone.Trim());
        }

        private async Task<int?> GetProfileId(User user)
        {
            return user.Role switch
            {
                UserRole.Patient => (await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == user.Id))?.Id,
                UserRole.Doctor => (await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == user.Id))?.Id,
                UserRole.ClinicAdmin => (await _unitOfWork.ClinicAdmins.Query().FirstOrDefaultAsync(ca => ca.UserId == user.Id))?.Id,
                _ => null
            };
        }

        private AuthResponseDto BuildAuthResponse(User user, int? profileId, Models.Entities.RefreshToken refreshToken)
        {
            var token = GenerateJwtToken(user);
            var expiration = DateTime.UtcNow.AddMinutes(GetAccessTokenExpirationMinutes());

            return new AuthResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Phone = user.PhoneNumber,
                Role = user.Role,
                Token = token,
                TokenExpiration = expiration,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiration = refreshToken.ExpiresAt,
                ProfileId = profileId
            };
        }

        private int GetAccessTokenExpirationMinutes()
        {
            var expirationValue = _configuration["JwtSettings:AccessTokenExpirationInMinutes"]
                                  ?? _configuration["JwtSettings:ExpirationInMinutes"];
            return int.TryParse(expirationValue, out var minutes) ? minutes : 60;
        }

        private int GetRefreshTokenExpirationDays()
        {
            var expirationValue = _configuration["JwtSettings:RefreshTokenExpirationInDays"] ?? "30";
            return int.TryParse(expirationValue, out var days) ? days : 30;
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.MobilePhone, user.PhoneNumber),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("userId", user.Id.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(GetAccessTokenExpirationMinutes()),
                signingCredentials: new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
