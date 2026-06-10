using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MedicalApp.API.Data;
using MedicalApp.API.Middleware;
using MedicalApp.API.Services.Implementations;
using MedicalApp.API.Services.Interfaces;
using MedicalApp.API.Data.Repositories;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ===== Database Context =====
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ===== HttpClient =====
builder.Services.AddHttpClient();

// ===== JWT Authentication =====
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)),
        ClockSkew = TimeSpan.Zero
    };

    // Return JSON errors instead of HTML (important for Flutter)
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                isSuccess = false,
                message = "You are not authorized to access this resource. Please sign in.",
                statusCode = 401
            });
            return context.Response.WriteAsync(result);
        },
        OnForbidden = context =>
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                isSuccess = false,
                message = "You do not have permission to access this resource",
                statusCode = 403
            });
            return context.Response.WriteAsync(result);
        }
    };
});

// ===== Register Services (Dependency Injection) =====
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();
builder.Services.AddScoped<IClinicService, ClinicService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ITelegramOtpService, TelegramOtpService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICommunityService, CommunityService>();

// ===== Hosted Background Services =====
builder.Services.AddHostedService<AppointmentReminderWorker>();

// ===== AutoMapper =====
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// ===== Controllers & JSON Options =====
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationFilterAttribute>(); // Global validation filter
    })
    .AddJsonOptions(options =>
    {
        // Convert enums to strings in JSON (easier for Flutter to parse)
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new MedicalApp.API.Helpers.TrimStringConverter());
    });

// ===== CORS (Allow Flutter app to connect) =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ===== Swagger =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Medical App API",
        Version = "v1",
        Description = "Backend API for the Medical Application - Built for Flutter Integration"
    });

    // Add JWT auth to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ===== Rate Limiting (DISABLED) =====
// builder.Services.AddRateLimiter(options =>
// {
//     options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
//
//     options.AddPolicy("AuthRateLimit", httpContext =>
//         RateLimitPartition.GetFixedWindowLimiter(
//             partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
//             factory: partition => new FixedWindowRateLimiterOptions
//             {
//                 AutoReplenishment = true,
//                 PermitLimit = 5,
//                 QueueLimit = 0,
//                 Window = TimeSpan.FromMinutes(1)
//             }));
//
//     options.AddPolicy("GeneralRateLimit", httpContext =>
//         RateLimitPartition.GetFixedWindowLimiter(
//             partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
//             factory: partition => new FixedWindowRateLimiterOptions
//             {
//                 AutoReplenishment = true,
//                 PermitLimit = 100,
//                 QueueLimit = 0,
//                 Window = TimeSpan.FromMinutes(1)
//             }));
// });

var app = builder.Build();

// Apply pending EF Core migrations at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database migration failed");
    }
}

// ===== Middleware Pipeline =====

// Global exception handler (MUST be first)
app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger (Development & Production for testing)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Medical App API v1");
        c.RoutePrefix = string.Empty; // Swagger at root URL
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Serve uploaded files (licenses, profile images)
app.UseCors("AllowAll");
// app.UseRateLimiter(); // Apply Rate Limiting
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers(); // Rate limiting disabled

app.Run();
