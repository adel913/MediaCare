using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Controllers
{
    [Authorize]
    public class NotificationController : BaseApiController
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get all notifications for the current logged-in user.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var result = await _notificationService.GetNotificationsAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get unread notifications count for the badge of the bell icon on the home screen.
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var result = await _notificationService.GetUnreadCountAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var result = await _notificationService.MarkAsReadAsync(GetUserId(), id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _notificationService.DeleteNotificationAsync(GetUserId(), id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
