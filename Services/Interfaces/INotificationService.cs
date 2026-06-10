using MedicalApp.API.DTOs.Notification;
using MedicalApp.API.Helpers;

namespace MedicalApp.API.Services.Interfaces
{
    public interface INotificationService
    {
        Task<ApiResponse<List<NotificationDto>>> GetNotificationsAsync(int userId);
        Task<ApiResponse<NotificationUnreadCountDto>> GetUnreadCountAsync(int userId);
        Task<ApiResponse> MarkAsReadAsync(int userId, int notificationId);
        Task<ApiResponse> CreateNotificationAsync(int userId, string title, string message);
        Task<ApiResponse> DeleteNotificationAsync(int userId, int notificationId);
    }
}
