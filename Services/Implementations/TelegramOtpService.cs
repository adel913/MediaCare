using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.Helpers;
using MedicalApp.API.Models.Entities;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Services.Implementations
{
    public class TelegramOtpService : ITelegramOtpService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public TelegramOtpService(IUnitOfWork unitOfWork, IConfiguration configuration, HttpClient httpClient)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<ApiResponse<bool>> SendOtpAsync(string phoneNumber, string otpCode)
        {
            // 1. Find the Telegram Chat ID for this phone number
            var mapping = await _unitOfWork.TelegramMappings.Query()
                .FirstOrDefaultAsync(t => t.PhoneNumber == phoneNumber && !t.IsDeleted);

            if (mapping == null)
            {
                return ApiResponse<bool>.Failure(
                    "No Telegram account linked to this phone number was found. Please activate via the Telegram bot first.",
                    404,
                    new List<string> { "NoTelegramMapping" }
                );
            }

            // 2. Get Bot Token from appsettings.json
            var botToken = _configuration["TelegramSettings:BotToken"] ?? "YOUR_TELEGRAM_BOT_TOKEN";
            if (string.IsNullOrEmpty(botToken) || botToken == "YOUR_TELEGRAM_BOT_TOKEN")
            {
                // Fallback for development: if no bot token is set, we will just print to console and act as if sent.
                Console.WriteLine($"[TELEGRAM OTP MOCK] Sent OTP {otpCode} to ChatId {mapping.TelegramChatId}");
                return ApiResponse<bool>.Success(true, "Verification code sent successfully (development mode)");
            }

            // 3. Send message via Telegram Bot API
            var message = $"Your verification code for the Medical Platform is: *{otpCode}*\nValid for 5 minutes.";
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            try
            {
                var payload = new
                {
                    chat_id = mapping.TelegramChatId,
                    text = message,
                    parse_mode = "Markdown"
                };

                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (response.IsSuccessStatusCode)
                {
                    return ApiResponse<bool>.Success(true, "Verification code sent successfully via Telegram");
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Telegram API Error] {errorContent}");
                return ApiResponse<bool>.Failure("Failed to send the message via Telegram. Please try again later.", 500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Telegram Client Exception] {ex.Message}");
                return ApiResponse<bool>.Failure("An error occurred while connecting to the Telegram service.", 500);
            }
        }

        public async Task<ApiResponse<bool>> SendNotificationAsync(string phoneNumber, string message)
        {
            // 1. Find the Telegram Chat ID for this phone number
            var mapping = await _unitOfWork.TelegramMappings.Query()
                .FirstOrDefaultAsync(t => t.PhoneNumber == phoneNumber && !t.IsDeleted);

            if (mapping == null)
            {
                return ApiResponse<bool>.Failure(
                    "No Telegram account linked to this phone number was found for notifications.",
                    404,
                    new List<string> { "NoTelegramMapping" }
                );
            }

            // 2. Get Bot Token from appsettings.json
            var botToken = _configuration["TelegramSettings:BotToken"] ?? "YOUR_TELEGRAM_BOT_TOKEN";
            if (string.IsNullOrEmpty(botToken) || botToken == "YOUR_TELEGRAM_BOT_TOKEN")
            {
                Console.WriteLine($"[TELEGRAM NOTIFICATION MOCK] Sent message to ChatId {mapping.TelegramChatId}:\n{message}");
                return ApiResponse<bool>.Success(true, "Notification sent successfully (development mode)");
            }

            // 3. Send message via Telegram Bot API
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            try
            {
                var payload = new
                {
                    chat_id = mapping.TelegramChatId,
                    text = message,
                    parse_mode = "Markdown"
                };

                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (response.IsSuccessStatusCode)
                {
                    return ApiResponse<bool>.Success(true, "Notification sent successfully via Telegram");
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Telegram API Error] {errorContent}");
                return ApiResponse<bool>.Failure("Failed to send the message via Telegram.", 500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Telegram Client Exception] {ex.Message}");
                return ApiResponse<bool>.Failure("An error occurred while connecting to the Telegram service.", 500);
            }
        }

        public async Task<ApiResponse<bool>> RegisterChatIdAsync(string phoneNumber, string chatId)
        {
            // Normalize phone number to match the app (e.g. remove spaces, make sure it starts with + or standard format)
            var cleanPhone = phoneNumber.Replace(" ", "").Replace("-", "");
            if (!cleanPhone.StartsWith("+") && !cleanPhone.StartsWith("00"))
            {
                // Assuming Egyptian numbers if no prefix, let's normalize as needed, or just save exactly
            }

            var existing = await _unitOfWork.TelegramMappings.Query()
                .FirstOrDefaultAsync(t => t.PhoneNumber == cleanPhone);

            if (existing != null)
            {
                existing.TelegramChatId = chatId;
                _unitOfWork.TelegramMappings.Update(existing);
            }
            else
            {
                var mapping = new TelegramMapping
                {
                    PhoneNumber = cleanPhone,
                    TelegramChatId = chatId
                };
                await _unitOfWork.TelegramMappings.AddAsync(mapping);
            }

            await _unitOfWork.CompleteAsync();
            return ApiResponse<bool>.Success(true, "Telegram account linked successfully! You can now request a verification code.");
        }
    }
}
