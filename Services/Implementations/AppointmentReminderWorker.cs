using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MedicalApp.API.Data;
using MedicalApp.API.Models.Entities;
using MedicalApp.API.Models.Enums;

namespace MedicalApp.API.Services.Implementations
{
    /// <summary>
    /// Background Hosted Service that periodically checks for upcoming appointments and sends a reminder 1 hour before.
    /// </summary>
    public class AppointmentReminderWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AppointmentReminderWorker> _logger;
        private readonly TimeSpan _period = TimeSpan.FromMinutes(2); // Runs every 2 minutes

        public AppointmentReminderWorker(IServiceProvider serviceProvider, ILogger<AppointmentReminderWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Appointment Reminder Background Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SendRemindersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing Appointment Reminder Background Worker.");
                }

                try
                {
                    await Task.Delay(_period, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Appointment Reminder Background Worker stopped.");
        }

        private async Task SendRemindersAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var now = DateTime.Now;
                var targetMin = now.AddMinutes(50);
                var targetMax = now.AddMinutes(70);

                // Fetch confirmed appointments for today/tomorrow that might be in the window
                var appointments = await dbContext.Appointments
                    .Include(a => a.Patient)
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.User)
                    .Where(a => a.Status == AppointmentStatus.Confirmed 
                        && a.PatientId != null)
                    .ToListAsync();

                var upcomingAppointments = appointments
                    .Where(a => {
                        var startDateTime = a.AppointmentDate.Date.Add(a.StartTime);
                        return startDateTime >= targetMin && startDateTime <= targetMax;
                    })
                    .ToList();

                foreach (var appointment in upcomingAppointments)
                {
                    if (appointment.Patient == null) continue;

                    var userId = appointment.Patient.UserId;
                    var appointmentTimeStr = appointment.StartTime.ToString(@"hh\:mm");
                    var expectedMessagePart = $"at {appointmentTimeStr}";

                    // Check if reminder notification already exists for this appointment
                    // to prevent duplicate notifications if worker runs multiple times in the window.
                    var alreadyNotified = await dbContext.Notifications
                        .AnyAsync(n => n.UserId == userId
                            && n.Title == "1-hour appointment reminder"
                            && n.Message.Contains(expectedMessagePart)
                            && n.Message.Contains(appointment.Doctor.User.FullName));

                    if (!alreadyNotified)
                    {
                        var notification = new Notification
                        {
                            UserId = userId,
                            Title = "1-hour appointment reminder",
                            Message = $"Reminder: your appointment with Dr. {appointment.Doctor.User.FullName} is in one hour (today at {appointmentTimeStr}). Please arrive on time."
                        };

                        await dbContext.Notifications.AddAsync(notification);
                        _logger.LogInformation("Sent 1-hour appointment reminder to patient user ID {UserId} for appointment ID {AppId}", userId, appointment.Id);
                    }
                }

                if (upcomingAppointments.Any())
                {
                    await dbContext.SaveChangesAsync();
                }
            }
        }
    }
}
