# MedicalApp API — Frontend Handover

**Base URL:** `http://localhost:5002`  
**Swagger UI:** `http://localhost:5002/` (root, `RoutePrefix = string.Empty`)
**Swagger JSON:** `http://localhost:5002/swagger/v1/swagger.json`

---

## 1. AUTHENTICATION

### JWT Config

| Setting | Value |
|---|---|
| Token endpoint | `POST /api/auth/login` |
| Refresh endpoint | `POST /api/auth/refresh-token` |
| Access token lifetime | **1 hour** |
| Refresh token lifetime | **30 days** |
| Auth header format | `Authorization: Bearer <token>` |

### Login

```
POST /api/auth/login
Body: { "phone": "01012345678", "password": "password123" }
```

**Response** (`AuthResponseDto`):
```json
{
  "isSuccess": true,
  "message": "تم تسجيل الدخول بنجاح",
  "data": {
    "userId": 1,
    "fullName": "أحمد علي",
    "phone": "01012345678",
    "role": "Patient",
    "token": "eyJhbGci...",
    "tokenExpiration": "2026-05-26T14:00:00Z",
    "refreshToken": "NHM0v0WL...",
    "refreshTokenExpiration": "2026-06-25T13:00:00Z",
    "profileId": 1
  },
  "statusCode": 200
}
```

### Token Refresh (Rotation)

Every refresh returns a **new** access token AND a **new** refresh token. The old refresh token is revoked — it cannot be reused.

```
POST /api/auth/refresh-token
Body: { "refreshToken": "NHM0v0WL..." }
```

If refresh fails (expired or revoked), redirect to login.

### Frontend Auth Flow

```
1. Login → store token + refreshToken + role + profileId
2. Every API call → attach Authorization: Bearer <token>
3. On 401 → call /refresh-token → get new token pair → retry original request
4. If refresh also 401 → send user to login screen
```

### Registration Endpoints

| Endpoint | For | Extra Fields |
|---|---|---|
| `POST /api/auth/register/patient` | Patients | `name`, `age` |
| `POST /api/auth/register/clinic` | Clinics | `clinicName`, `government`, `area`, `licenseFileUrl` |
| `POST /api/auth/register/doctor` | Doctors | `specialization`, `licenseFileUrl` |

All require: `phone`, `password`, `confirmPassword` (min 6 chars).

### Password Reset Flow

```
POST /api/auth/forgot-password  →  { phone }  →  sends OTP via Telegram
POST /api/auth/verify-otp       →  { phone, otpCode }
POST /api/auth/reset-password   →  { phone, otpCode, newPassword, confirmPassword }
```

OTP is 4 digits, expires in 5 minutes. User must have linked their Telegram via `POST /api/auth/telegram-register` first.

### JWT Token Payload Claims

| Claim | Value |
|---|---|
| `nameidentifier` | `user.Id` (int) |
| `mobilephone` | Phone number |
| `name` | Full name |
| `role` | `"Patient"`, `"Doctor"`, or `"ClinicAdmin"` |
| `userId` | `user.Id` (duplicate, convenience) |

Use the `role` claim for role-based navigation. The `profileId` in `AuthResponseDto` is the role-specific entity ID (Patient.Id, Doctor.Id, or ClinicAdmin.Id).

---

## 2. RESPONSE FORMAT

Every endpoint returns this envelope:

```json
{
  "isSuccess": true,
  "message": "تمت العملية بنجاح",
  "data": { ... },
  "errors": null,
  "statusCode": 200
}
```

**Always check `isSuccess` before accessing `data`.**
Error messages are in Arabic. Validation errors come in `errors: ["message1", "message2"]`.

---

## 3. ENUMS (send integer values)

| Enum | Values |
|---|---|
| `UserRole` | Patient=0, Doctor=1, ClinicAdmin=2 |
| `Gender` | Male=0, Female=1 |
| `AppointmentStatus` | Pending=0, Confirmed=1, InProgress=2, Completed=3, Cancelled=4, NoShow=5 |
| `QueueStatus` | Waiting=0, InConsultation=1, Completed=2, Refunded=3 |
| `RefundStatus` | None=0, Pending=1, Processed=2 |
| `PaymentMethod` | Cash=0, Online=1 |
| `RelationType` | Parent=0, Child=1, Spouse=2, Sibling=3, Other=4 |

---

## 4. KEY DTO SHAPES

### TimeSpan → send as `"HH:mm:ss"` string in JSON. DateTime → ISO 8601 string.

### DoctorListItemDto (search/browse results)
```ts
{
  id: number;
  fullName: string;
  specialization: string;
  profileImageUrl: string | null;
  consultationFee: number;
  averageRating: number;
  totalReviews: number;
  isAvailable: boolean;
  clinicName: string | null;
  clinicArea: string | null;
  isFavorited: boolean;
  latitude: number | null;
  longitude: number | null;
  distanceKm: number | null;          // populated when ?userLat&userLng provided
}
```

### NearbyDoctorDto (doctor map results — extends DoctorListItemDto)
```ts
{
  // All DoctorListItemDto fields, plus:
  distanceKm: number;              // always populated (non-null)
  clinicIdForLocation: number | null; // which clinic's coords were used
}
```

### DoctorProfileDto (full profile)
```ts
{
  id, userId, fullName, phoneNumber, email?, profileImageUrl?,
  specialization, licenseNumber?, licenseImageUrl?,
  yearsOfExperience, bio?, consultationFee, averageRating, totalReviews,
  isAvailable, clinicId?, clinicName?,
  clinicLatitude?, clinicLongitude?,        // active clinic location for maps
  degree?, university?, subSpecialty?, graduationYear?, boardCertification?,
  languages: string[], associatedClinics: string[], qrCodeKey?,
  totalPatients: number                     // distinct registered patients (excludes walk-ins)
}
```

### AppointmentDto
```ts
{
  id: number;
  patientId: number | null;
  patientName: string;
  familyMemberId: number | null;
  familyMemberName: string | null;
  doctorId: number;
  doctorName: string;
  specialization: string;
  appointmentDate: string;      // ISO 8601 date
  startTime: string;            // "HH:mm:ss"
  endTime: string | null;
  status: number;               // AppointmentStatus enum
  statusText: string;           // Arabic display
  queueNumber: number | null;
  queueStatus: number | null;   // QueueStatus enum
  refundStatusText: string;
  notes: string | null;
  cancellationReason: string | null;
  doctorProfileImageUrl: string | null;
  clinicId: number | null;
  clinicName: string | null;
  clinicAddress: string | null;
  currentServingNumber: number | null;
  isEmergency: boolean;
  chiefComplaint: string | null;
  isPaid: boolean;
  paymentMethodText: string;    // "Cash" or "Online"
  createdAt: string;            // ISO 8601
}
```

### ClinicDto
```ts
{
  id, name, facilityId?, description?, government?, area?, address?,
  linkMap?, phoneNumber?, email?, logoUrl?, licenseImageUrl?,
  latitude?, longitude?, openingTime?, closingTime?, isActive, doctorsCount
}
```

`openingTime` / `closingTime` — `TimeSpan?`, serialized as `"HH:mm:ss"` or `null`.
Available on **all** Clinic endpoints (`GET /api/clinic`, `GET /api/clinic/{id}`, `POST /api/clinic`, etc.)
and settable via `CreateClinicDto` / `UpdateClinicDto`.

### NearbyClinicDto (clinic map results — extends ClinicDto)
```ts
{
  // All ClinicDto fields, plus:
  distanceKm: number;               // Haversine in km (always populated)
  matchingDoctorsCount: number;     // doctors matching the requested specialization filter
}
```

### PatientProfileDto
```ts
{
  id, userId, fullName, phoneNumber, email?, gender?, age?, dateOfBirth?,
  profileImageUrl?, address?, bloodType?, allergies?, chronicDiseases?,
  emergencyContactName?, emergencyContactPhone?
}
```

### LiveQueueTrackerDto
```ts
{
  appointmentId, myQueueNumber, currentServingNumber,
  patientsAheadOfMe, estimatedWaitTimeMinutes,
  myQueueStatus: number | null, doctorName
}
```

### DoctorDashboardDto
```ts
{
  totalAppointments, newPatientsCount, followUpsCount,
  walkInsCount, onlineCount, todayEarnings,
  waitingCount, withDoctorCount, completedCount
}
```

### MedicalRecordDto (SOAP format)
```ts
{
  id, patientId, patientName, doctorId, doctorName, doctorSpecialization,
  doctorProfileImageUrl, appointmentId?,
  diagnosis, prescription?, treatmentPlan?, notes?, symptoms?,
  subjective?, objective?, assessment?, plan?,
  bloodPressure?, heartRate?, weight?,
  medications?: { name, category, dosage, duration }[],
  observations?, recommendedCare?: string[],
  visitDate, createdAt
}
```

### CommunityPostDto
```ts
{
  id, userId, authorName, authorProfileImageUrl?, authorRoleText,
  authorSpecialization?, content, specialization?, createdAt,
  commentsCount, comments: CommunityCommentDto[]
}
```

---

## 5. ENDPOINT SUMMARY BY ROLE

### Public (no auth required)
| Endpoint | Description |
|---|---|
| `POST /api/auth/login` | Login |
| `POST /api/auth/register/patient` | Patient signup |
| `POST /api/auth/register/clinic` | Clinic signup |
| `POST /api/auth/register/doctor` | Doctor signup |
| `POST /api/auth/refresh-token` | Refresh JWT |
| `POST /api/auth/forgot-password` | Request OTP |
| `POST /api/auth/verify-otp` | Verify OTP |
| `POST /api/auth/reset-password` | Reset password |
| `POST /api/auth/social-login` | NOT IMPLEMENTED (returns 501) |
| `GET /api/auth/telegram-register` | Link Telegram |
| `GET /api/doctor` | Browse/search doctors (supports `?userLat=&userLng=` for distance sort) |
| `GET /api/doctor/nearby` | Geospatial doctor search (returns `NearbyDoctorDto[]`) |
| `GET /api/doctor/specializations` | List specializations |
| `GET /api/doctor/popular` | Popular doctors (supports `?userLat=&userLng=` for distance annotations) |
| `GET /api/doctor/{id}` | Doctor profile (now includes `clinicLatitude`, `clinicLongitude`, `totalPatients`) |
| `GET /api/doctor/{id}/schedules` | Doctor weekly schedule |
| `GET /api/doctor/{id}/available-slots?date=` | Available time slots |
| `GET /api/clinic` | Browse clinics |
| `GET /api/clinic/nearby` | Geospatial clinic search (returns `NearbyClinicDto[]`) |
| `GET /api/clinic/{id}` | Clinic detail |
| `GET /api/review/doctor/{doctorId}` | Reviews for doctor |
| `POST /api/upload/license` | Upload license file |
| `POST /api/upload/profile-image` | Upload profile image |

### Patient (authenticated)
| Endpoint | Description |
|---|---|
| `POST /api/auth/logout` | Logout |
| `GET /api/patient/profile` | My profile |
| `PUT /api/patient/profile` | Update profile |
| `POST /api/patient/favorite/{doctorId}` | Toggle favorite doctor |
| `GET /api/patient/favorites` | My favorited doctors (returns `DoctorListItemDto[]`) |
| `GET /api/patient/family-members` | My family members |
| `POST /api/patient/family-members` | Add family member |
| `DELETE /api/patient/family-members/{id}` | Remove family member |
| `POST /api/appointment` | Book appointment |
| `GET /api/appointment/patient?filter=upcoming\|completed\|cancelled` | My appointments |
| `GET /api/appointment/{id}` | Appointment detail |
| `PUT /api/appointment/{id}/cancel` | Cancel appointment |
| `PUT /api/appointment/{id}/reschedule` | Reschedule appointment |
| `GET /api/appointment/queue/tracker/{id}` | Live queue position |
| `POST /api/review` | Submit review |
| `GET /api/community/posts` | Community feed |
| `POST /api/community/posts` | Create post |
| `POST /api/community/posts/{id}/comments` | Comment on post |
| `GET /api/community/posts/{id}/comments` | Post comments |
| `DELETE /api/community/posts/{id}` | Delete my post |
| `DELETE /api/community/comments/{id}` | Delete my comment |
| `GET /api/notification` | My notifications |
| `GET /api/notification/unread-count` | Unread count |
| `PUT /api/notification/{id}/read` | Mark as read |
| `DELETE /api/notification/{id}` | Delete notification (owner only) |

### Doctor (authenticated)
| Endpoint | Description |
|---|---|
| `POST /api/auth/logout` | Logout |
| `GET /api/doctor/profile` | My profile |
| `PUT /api/doctor/profile` | Update profile |
| `GET /api/doctor/dashboard` | Dashboard stats |
| `GET /api/doctor/live-queue?status=` | Today's patient queue |
| `GET /api/doctor/qr-code` | Get QR code key |
| `GET /api/doctor/patients/{patientId}/history` | Patient history |
| `POST /api/doctor/session/{appointmentId}` | Submit consultation (SOAP) |
| `GET /api/appointment/doctor?date=&status=` | My appointments |
| `GET /api/appointment/queue/today` | Today's queue |
| `PUT /api/appointment/{id}/status` | Change appointment status |
| `POST /api/appointment/queue/call-next` | Call next patient |
| `POST /api/appointment/clinic-booking` | Create walk-in booking |
| `GET /api/patient/search?query=` | Search patients |
| `GET /api/medicalrecord/patient/{patientId}` | Patient records |
| `GET /api/medicalrecord/{id}` | Record detail |
| `POST /api/medicalrecord` | Create record |
| `GET /api/community/posts` | Community feed |
| `POST /api/community/posts` | Create post |
| `DELETE /api/community/posts/{id}` | Delete my post |
| `DELETE /api/community/comments/{id}` | Delete my comment |
| `GET /api/notification` | Notifications |
| `GET /api/notification/unread-count` | Unread count |
| `PUT /api/notification/{id}/read` | Mark as read |
| `DELETE /api/notification/{id}` | Delete notification (owner only) |

### ClinicAdmin (authenticated)
| Endpoint | Description |
|---|---|
| `POST /api/auth/logout` | Logout |
| `GET /api/clinic/profile` | My clinic profile |
| `PUT /api/clinic/profile` | Update clinic |
| `POST /api/clinic` | Create clinic |
| `PUT /api/clinic/{id}` | Update clinic |
| `GET /api/clinic/doctors` | My clinic's doctors |
| `GET /api/clinic/doctors/scan/{qrCodeKey}` | Scan doctor QR |
| `POST /api/clinic/doctors/register` | Register doctor to clinic |
| `GET /api/clinic/doctors/{doctorId}` | Doctor details |
| `PUT /api/clinic/doctors/{doctorId}` | Update doctor at clinic |
| `DELETE /api/clinic/doctors/{doctorId}` | Remove doctor from clinic |
| `POST /api/doctor/{doctorId}/schedules` | Add schedule for doctor |
| `POST /api/appointment/clinic-booking` | Create walk-in booking |
| `GET /api/appointment/clinic/dashboard?doctorId=` | Reception dashboard |
| `GET /api/appointment/clinic/queue?doctorId=` | Today's queue |
| `POST /api/appointment/{id}/start-checkup` | Start patient checkup |
| `GET /api/appointment/clinic/payments-dashboard?doctorId=&timeframe=today\|week\|month` | Payments dashboard |
| `GET /api/patient/search?query=` | Search patients |
| `GET /api/notification` | Notifications |
| `GET /api/notification/unread-count` | Unread count |
| `PUT /api/notification/{id}/read` | Mark as read |
| `DELETE /api/notification/{id}` | Delete notification (owner only) |

---

## 6. CLINIC WORKFLOW (Daily Operation)

```
1. Receptionist logs in as ClinicAdmin
2. GET /clinic/queue?doctorId= → today's patient list
3. Patient arrives → POST /appointment/{id}/start-checkup → patient now InConsultation
4. Doctor sees patient → POST /doctor/session/{appointmentId} → submit diagnosis/prescription
5. Doctor → PUT /appointment/{id}/status { status: Completed } OR
   Doctor → POST /appointment/queue/call-next → auto-completes current + calls next
6. Reception → GET /appointment/clinic/payments-dashboard → daily revenue
```

---

## 7. FILE UPLOADS

```
POST /api/upload/license        → multipart form, field "file", max 5MB (.jpg/.jpeg/.png/.pdf)
POST /api/upload/profile-image  → multipart form, field "file", max 3MB (.jpg/.jpeg/.png)
```

Response: `{ url: "/uploads/filename.jpg" }`. Pass the returned `url` string into subsequent DTOs (e.g., `licenseFileUrl`, `profileImageUrl`).

---

## 8. RATE LIMITS

| Policy | Limit | Window | Applies To |
|---|---|---|---|
| `AuthRateLimit` | 5 req | 1 minute | `/api/auth/*` |
| `GeneralRateLimit` | 100 req | 1 minute | Everything else |

Exceeding returns HTTP 429.

---

## 9. CORS

Fully open — `AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`. No CORS issues expected.

---

## 10. HTTPS

The server has `UseHttpsRedirection()` enabled. Plain HTTP requests to the root redirect to HTTPS with a 301. When running locally without HTTPS, the redirect fails silently. For development, hit specific endpoints directly or configure `ASPNETCORE_URLS` with HTTPS.

---

## 11. QUICK START FOR FLUTTER

```dart
// 1. Login
final response = await http.post(
  Uri.parse('$baseUrl/api/auth/login'),
  headers: {'Content-Type': 'application/json'},
  body: jsonEncode({'phone': phone, 'password': password}),
);
final auth = AuthResponseDto.fromJson(jsonDecode(response.body)['data']);

// 2. Store tokens
await secureStorage.write('token', auth.token);
await secureStorage.write('refreshToken', auth.refreshToken);
await secureStorage.write('role', auth.role.toString());

// 3. Authenticated request
final response = await http.get(
  Uri.parse('$baseUrl/api/patient/profile'),
  headers: {'Authorization': 'Bearer ${await secureStorage.read('token')}'},
);

// 4. Refresh interceptor
if (response.statusCode == 401) {
  final refreshResp = await http.post(
    Uri.parse('$baseUrl/api/auth/refresh-token'),
    body: jsonEncode({'refreshToken': await secureStorage.read('refreshToken')}),
  );
  if (refreshResp.statusCode == 200) {
    // Store new tokens, retry original request
  } else {
    // Logout, navigate to login
  }
}
```
