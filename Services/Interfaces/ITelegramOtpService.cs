using MedicalApp.API.Helpers;

namespace MedicalApp.API.Services.Interfaces
{
    public interface ITelegramOtpService
    {
        Task<ApiResponse<bool>> SendOtpAsync(string phoneNumber, string otpCode);

        Task<ApiResponse<bool>> SendNotificationAsync(string phoneNumber, string message);

        /// <summary>
        /// Registers or updates the mapping between a phone number and Telegram Chat ID.
        /// Typically called by a Telegram Bot webhook or client when the user interacts with the bot.
        /// </summary>
        Task<ApiResponse<bool>> RegisterChatIdAsync(string phoneNumber, string chatId);
    }
}
