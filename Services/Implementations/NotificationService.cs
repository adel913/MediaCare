using Microsoft.EntityFrameworkCore;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.DTOs.Notification;
using MedicalApp.API.Helpers;
using MedicalApp.API.Models.Entities;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public NotificationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<List<NotificationDto>>> GetNotificationsAsync(int userId)
        {
            var notifications = await _unitOfWork.Notifications.Query()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            var result = notifications.Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            }).ToList();

            return ApiResponse<List<NotificationDto>>.Success(result);
        }

        public async Task<ApiResponse<NotificationUnreadCountDto>> GetUnreadCountAsync(int userId)
        {
            var unreadCount = await _unitOfWork.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            var result = new NotificationUnreadCountDto { UnreadCount = unreadCount };
            return ApiResponse<NotificationUnreadCountDto>.Success(result);
        }

        public async Task<ApiResponse> MarkAsReadAsync(int userId, int notificationId)
        {
            var notification = await _unitOfWork.Notifications.Query()
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                return ApiResponse.Failure("Notification not found", 404);

            notification.IsRead = true;
            await _unitOfWork.CompleteAsync();

            return ApiResponse.Success("Notification status updated successfully");
        }

        public async Task<ApiResponse> DeleteNotificationAsync(int userId, int notificationId)
        {
            var notification = await _unitOfWork.Notifications.Query()
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                return ApiResponse.Failure("Notification not found", 404);

            _unitOfWork.Notifications.Remove(notification);
            await _unitOfWork.CompleteAsync();

            return ApiResponse.Success("Notification deleted successfully");
        }

        public async Task<ApiResponse> CreateNotificationAsync(int userId, string title, string message)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                IsRead = false
            };

            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.CompleteAsync();

            return ApiResponse.Success("Notification created successfully");
        }
    }
}
