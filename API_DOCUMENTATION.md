# Tutoria API Documentation

Complete documentation for the Tutoria educational platform APIs - Authentication and Management services.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Authentication & Authorization](#authentication--authorization)
- [API Endpoints](#api-endpoints)
  - [Auth API](#auth-api)
  - [Management API](#management-api)
- [Data Models](#data-models)
- [Error Handling](#error-handling)
- [Rate Limiting](#rate-limiting)
- [Examples](#examples)

---

## Overview

Tutoria provides two complementary APIs:

1. **Auth API** (Port 5001): User authentication, registration, and profile management
2. **Management API** (Port 5002): University, course, module, and content management

Both APIs use JWT Bearer tokens for authentication and follow RESTful principles.

### Base URLs

- **Auth API**: `https://localhost:5001` (Development)
- **Management API**: `https://localhost:5002` (Development)

### Technology Stack

- **.NET 8**: Modern C# web framework
- **Entity Framework Core**: ORM for SQL Server
- **JWT Bearer**: Stateless authentication
- **Swagger/OpenAPI**: Interactive API documentation
- **Azure Blob Storage**: File storage for module content
- **OpenAI API**: AI tutoring functionality
- **Serilog**: Structured logging
- **AspNetCoreRateLimit**: Rate limiting middleware

---

## Architecture

### Onion Architecture (Domain-Driven Design)

```
TutoriaApi/
├── TutoriaApi.Core/            # Domain entities, interfaces
├── TutoriaApi.Infrastructure/  # Data access, external services
├── TutoriaApi.Web.Auth/        # Authentication API
└── TutoriaApi.Web.Management/  # Management API
```

### Design Patterns

- **Repository Pattern**: Abstraction over data access
- **Service Pattern**: Business logic encapsulation
- **Dependency Injection**: .NET Core DI container
- **Middleware Pipeline**: Request/response logging, rate limiting, auth

### Database Schema

Unified `Users` table with discriminator column `UserType`:
- `super_admin`: Full system access
- `professor`: Course and module management (can be admin professors)
- `student`: Course enrollment and learning

---

## Authentication & Authorization

### Authentication Flows

#### 1. User Authentication (Username/Password)

**Endpoint**: `POST /api/auth/login`

**Request**:
```json
{
  "username": "professor1",
  "password": "SecurePass123!"
}
```

**Response**:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresIn": 28800,
  "user": {
    "userId": 1,
    "username": "professor1",
    "email": "prof@university.edu",
    "firstName": "John",
    "lastName": "Doe",
    "userType": "professor",
    "isAdmin": true
  }
}
```

**Token Lifetimes**:
- Access Token: 8 hours
- Refresh Token: 30 days

#### 2. API Client Authentication (OAuth2 Client Credentials)

**Endpoint**: `POST /api/auth/token`

**Request** (application/x-www-form-urlencoded):
```
grant_type=client_credentials
client_id=tutoria-management-api
client_secret=super-secret-key
```

**Response**:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "scope": "api.read api.write api.admin"
}
```

**Token Lifetime**: 1 hour

#### 3. Token Refresh

**Endpoint**: `POST /api/auth/refresh`

**Request**:
```json
{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Response**: Same as login response with new tokens.

### Authorization Policies

Management API enforces role-based policies:

| Policy | Allowed Roles | Use Case |
|--------|---------------|----------|
| `SuperAdminOnly` | super_admin | System-wide operations (universities) |
| `AdminOrAbove` | super_admin, admin professor | Course and professor management |
| `ProfessorOrAbove` | super_admin, admin professor, professor | Module and content management |

### Using Bearer Tokens

Include access token in Authorization header:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

Example with curl:
```bash
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  https://localhost:5002/api/courses
```

---

## API Endpoints

### Auth API

Base URL: `https://localhost:5001`

#### Authentication Endpoints

##### POST /api/auth/login
Authenticate user with username and password.

**Request Body**:
```json
{
  "username": "student1",
  "password": "Password123!"
}
```

**Success Response** (200 OK): See [User Authentication](#1-user-authentication-usernamepassword) above.

**Error Responses**:
- `400 Bad Request`: Invalid request format
- `401 Unauthorized`: Invalid credentials or inactive account

---

##### POST /api/auth/token
OAuth2 client credentials flow for API-to-API authentication.

**Request** (application/x-www-form-urlencoded):
```
grant_type=client_credentials
client_id=your-client-id
client_secret=your-client-secret
```

**Success Response** (200 OK): See [API Client Authentication](#2-api-client-authentication-oauth2-client-credentials) above.

**Error Responses**:
- `400 Bad Request`: Missing grant_type or unsupported grant type
- `401 Unauthorized`: Invalid client credentials

---

##### POST /api/auth/register/student
Register a new student account.

**Request Body**:
```json
{
  "username": "newstudent",
  "email": "student@university.edu",
  "password": "SecurePass123!",
  "firstName": "Jane",
  "lastName": "Smith",
  "courseId": 1
}
```

**Success Response** (201 Created): Same as login response (auto-login).

**Error Responses**:
- `400 Bad Request`: Validation failed, username/email taken
- `404 Not Found`: Course not found

**Password Requirements**:
- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one number
- At least one special character

---

##### POST /api/auth/refresh
Refresh access token using refresh token.

**Request Body**:
```json
{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Success Response** (200 OK): New access and refresh tokens.

**Error Responses**:
- `400 Bad Request`: Invalid request format
- `401 Unauthorized`: Invalid or expired refresh token, user not found/inactive

---

##### POST /api/auth/password-reset-request
Request password reset token via email.

**Request Body**:
```json
{
  "email": "user@university.edu"
}
```

**Success Response** (200 OK):
```json
{
  "message": "If the email exists, a password reset link has been sent"
}
```

**Security Features**:
- Always returns success to prevent email enumeration
- Token expires in 1 hour
- TODO: Email service integration (currently logs token)

---

##### POST /api/auth/password-reset
Reset password using reset token.

**Request Body**:
```json
{
  "token": "secure-reset-token-from-email",
  "newPassword": "NewSecurePass123!"
}
```

**Success Response** (200 OK):
```json
{
  "message": "Password has been reset successfully"
}
```

**Error Responses**:
- `400 Bad Request`: Invalid/expired token, validation failed

---

#### Profile Management Endpoints

##### GET /api/auth/me
Get current user's profile.

**Authorization**: Required (Bearer token)

**Success Response** (200 OK):
```json
{
  "userId": 1,
  "username": "professor1",
  "email": "prof@university.edu",
  "firstName": "John",
  "lastName": "Doe",
  "userType": "professor",
  "isActive": true,
  "universityId": 1,
  "universityName": "MIT",
  "isAdmin": true,
  "courseId": null,
  "courseName": null,
  "themePreference": "dark",
  "languagePreference": "en",
  "lastLoginAt": "2025-10-14T12:00:00Z",
  "createdAt": "2025-01-01T00:00:00Z"
}
```

**Error Responses**:
- `401 Unauthorized`: Invalid token or inactive account
- `404 Not Found`: User not found

---

##### PUT /api/auth/me
Update current user's profile.

**Authorization**: Required (Bearer token)

**Request Body** (all fields optional):
```json
{
  "firstName": "John",
  "lastName": "Doe",
  "email": "newemail@university.edu",
  "themePreference": "dark",
  "languagePreference": "pt-br"
}
```

**Success Response** (200 OK): Updated user profile (same as GET /api/auth/me).

**Error Responses**:
- `400 Bad Request`: Validation failed, email already in use
- `401 Unauthorized`: Invalid token or inactive account
- `404 Not Found`: User not found

---

##### PUT /api/auth/me/password
Change current user's password.

**Authorization**: Required (Bearer token)

**Request Body**:
```json
{
  "currentPassword": "OldPassword123!",
  "newPassword": "NewSecurePass123!"
}
```

**Success Response** (200 OK):
```json
{
  "message": "Password changed successfully"
}
```

**Error Responses**:
- `400 Bad Request`: Current password incorrect, validation failed
- `401 Unauthorized`: Invalid token or inactive account
- `404 Not Found`: User not found

**Security**: Current password verification required.

---

### Management API

Base URL: `https://localhost:5002`

All Management API endpoints require authentication (Bearer token in Authorization header).

#### University Endpoints

##### GET /api/universities
Get paginated list of universities.

**Authorization**: Authenticated users

**Query Parameters**:
- `page` (default: 1): Page number
- `size` (default: 10, max: 100): Page size
- `search` (optional): Search by name or code

**Success Response** (200 OK):
```json
{
  "items": [
    {
      "id": 1,
      "name": "Massachusetts Institute of Technology",
      "code": "MIT",
      "description": "A private research university...",
      "coursesCount": 45,
      "professorsCount": 120,
      "studentsCount": 3400,
      "createdAt": "2025-01-01T00:00:00Z",
      "updatedAt": "2025-01-01T00:00:00Z"
    }
  ],
  "total": 1,
  "page": 1,
  "size": 10,
  "pages": 1
}
```

---

##### GET /api/universities/{id}
Get university by ID with full details.

**Authorization**: Authenticated users

**Success Response** (200 OK):
```json
{
  "id": 1,
  "name": "MIT",
  "code": "MIT",
  "description": "...",
  "courses": [
    {
      "id": 1,
      "name": "Computer Science",
      "code": "CS",
      "description": "..."
    }
  ],
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-01-01T00:00:00Z"
}
```

**Error Responses**:
- `404 Not Found`: University not found

---

##### POST /api/universities
Create a new university.

**Authorization**: SuperAdminOnly

**Request Body**:
```json
{
  "name": "Harvard University",
  "code": "HARVARD",
  "description": "Private Ivy League research university"
}
```

**Success Response** (201 Created): Created university object.

**Error Responses**:
- `400 Bad Request`: Validation failed, name/code already exists
- `403 Forbidden`: User lacks SuperAdminOnly permission

---

##### PUT /api/universities/{id}
Update university.

**Authorization**: SuperAdminOnly

**Request Body** (all fields optional):
```json
{
  "name": "Harvard University",
  "code": "HARVARD",
  "description": "Updated description"
}
```

**Success Response** (200 OK): Updated university object.

**Error Responses**:
- `400 Bad Request`: Validation failed, name/code conflict
- `403 Forbidden`: User lacks SuperAdminOnly permission
- `404 Not Found`: University not found

---

##### DELETE /api/universities/{id}
Delete university (cascades to courses, modules).

**Authorization**: SuperAdminOnly

**Success Response** (200 OK):
```json
{
  "message": "University deleted successfully"
}
```

**Error Responses**:
- `403 Forbidden`: User lacks SuperAdminOnly permission
- `404 Not Found`: University not found

---

#### Course Endpoints

##### GET /api/courses
Get paginated list of courses.

**Authorization**: Authenticated users

**Query Parameters**:
- `page`, `size`: Pagination
- `universityId` (optional): Filter by university
- `search` (optional): Search by name or code

**Success Response** (200 OK): Paginated course list with counts.

---

##### GET /api/courses/{id}
Get course by ID with details (modules, students).

**Authorization**: Authenticated users

**Success Response** (200 OK): Course with university, modules, students.

---

##### POST /api/courses
Create a new course.

**Authorization**: AdminOrAbove

**Request Body**:
```json
{
  "name": "Computer Science",
  "code": "CS",
  "description": "Undergraduate CS program",
  "universityId": 1
}
```

**Success Response** (201 Created): Created course object.

**Error Responses**:
- `400 Bad Request`: Validation failed, code exists in university
- `403 Forbidden`: User lacks AdminOrAbove permission
- `404 Not Found`: University not found

---

##### PUT /api/courses/{id}
Update course.

**Authorization**: AdminOrAbove

**Request Body** (all fields optional):
```json
{
  "name": "Computer Science",
  "code": "CS-NEW",
  "description": "Updated description"
}
```

**Success Response** (200 OK): Updated course object.

---

##### DELETE /api/courses/{id}
Delete course (cascades to modules).

**Authorization**: AdminOrAbove

**Success Response** (200 OK): Success message.

---

##### POST /api/courses/{courseId}/professors/{professorId}
Assign professor to course.

**Authorization**: AdminOrAbove

**Success Response** (200 OK): Success message.

**Error Responses**:
- `400 Bad Request`: Professor already assigned
- `404 Not Found`: Course or professor not found

---

##### DELETE /api/courses/{courseId}/professors/{professorId}
Unassign professor from course.

**Authorization**: AdminOrAbove

**Success Response** (200 OK): Success message.

---

#### Module Endpoints

##### GET /api/modules
Get paginated list of modules.

**Authorization**: Authenticated users

**Query Parameters**:
- `page`, `size`: Pagination
- `courseId` (optional): Filter by course
- `semester` (optional): Filter by semester (1-8)
- `year` (optional): Filter by year (2020-2050)
- `search` (optional): Search by name or code

**Success Response** (200 OK): Paginated module list with file/token counts.

---

##### GET /api/modules/{id}
Get module by ID with full details.

**Authorization**: Authenticated users

**Success Response** (200 OK):
```json
{
  "id": 1,
  "name": "Introduction to Python",
  "code": "CS101",
  "description": "...",
  "systemPrompt": "You are a helpful Python tutor...",
  "semester": 1,
  "year": 2025,
  "courseId": 1,
  "course": { ... },
  "openAIAssistantId": "asst_abc123",
  "openAIVectorStoreId": "vs_abc123",
  "lastPromptImprovedAt": "2025-10-01T00:00:00Z",
  "promptImprovementCount": 5,
  "tutorLanguage": "en",
  "files": [ ... ],
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-10-14T00:00:00Z"
}
```

---

##### POST /api/modules
Create a new module.

**Authorization**: ProfessorOrAbove

**Request Body**:
```json
{
  "name": "Introduction to Python",
  "code": "CS101",
  "description": "Learn Python fundamentals",
  "systemPrompt": "You are a helpful Python tutor. Guide students through Python basics...",
  "semester": 1,
  "year": 2025,
  "courseId": 1,
  "tutorLanguage": "en"
}
```

**Success Response** (201 Created): Created module object.

**Error Responses**:
- `400 Bad Request`: Validation failed, code exists in course
- `403 Forbidden`: User lacks ProfessorOrAbove permission
- `404 Not Found`: Course not found

---

##### PUT /api/modules/{id}
Update module.

**Authorization**: ProfessorOrAbove

**Request Body** (all fields optional):
```json
{
  "name": "Advanced Python",
  "systemPrompt": "Updated system prompt...",
  "tutorLanguage": "pt-br"
}
```

**Success Response** (200 OK): Updated module object.

---

##### DELETE /api/modules/{id}
Delete module (cascades to files, access tokens).

**Authorization**: ProfessorOrAbove

**Success Response** (200 OK): Success message.

---

#### File Endpoints

All file endpoints require `ProfessorOrAbove` policy.

##### GET /api/files
Get paginated list of files.

**Query Parameters**:
- `page`, `size`: Pagination
- `moduleId` (optional): Filter by module
- `search` (optional): Search by filename

**Success Response** (200 OK): Paginated file list with module info.

---

##### GET /api/files/{id}
Get file by ID with full details.

**Success Response** (200 OK): File with module/course/university hierarchy.

---

##### POST /api/files
Upload file to module.

**Request** (multipart/form-data):
- `moduleId` (form field): Module ID
- `file` (form file): File to upload

**Success Response** (201 Created): Created file object with pending status.

**Error Responses**:
- `400 Bad Request`: File required
- `404 Not Found`: Module not found
- `500 Internal Server Error`: Upload failed

**Process**:
1. Upload to Azure Blob Storage
2. Create database record with status "pending"
3. Background process should update status to "completed" after OpenAI processing

---

##### PUT /api/files/{id}/status
Update file processing status.

**Request Body**:
```json
{
  "status": "completed",
  "openAIFileId": "file-abc123",
  "errorMessage": null
}
```

**Success Response** (200 OK): Updated file object.

---

##### DELETE /api/files/{id}
Delete file from storage and database.

**Success Response** (200 OK): Success message.

**Process**:
1. Delete from Azure Blob Storage
2. Delete from database
3. Continues even if blob deletion fails (logs error)

---

##### GET /api/files/{id}/download
Get file download URL.

**Success Response** (302 Redirect): Redirects to Azure Blob Storage URL with SAS token (1-hour expiration).

**Error Responses**:
- `404 Not Found`: File not found
- `500 Internal Server Error`: Failed to generate download URL

---

#### Module Access Token Endpoints

All token endpoints require `ProfessorOrAbove` policy.

##### GET /api/module-access-tokens
Get paginated list of module access tokens.

**Query Parameters**:
- `page`, `size`: Pagination
- `moduleId` (optional): Filter by module
- `isActive` (optional): Filter by active status

**Success Response** (200 OK): Paginated token list.

---

##### GET /api/module-access-tokens/{id}
Get token by ID with full details.

**Success Response** (200 OK): Token with module/course/university info and creator details.

---

##### POST /api/module-access-tokens
Create a new module access token.

**Request Body**:
```json
{
  "name": "Canvas Widget Token",
  "description": "Token for embedding tutor in Canvas LMS",
  "moduleId": 1,
  "expiresInDays": 365,
  "allowChat": true,
  "allowFileAccess": true
}
```

**Success Response** (201 Created): Created token object with generated token string.

**Token Generation**:
- 32 random bytes
- Base64 encoded, URL-safe (no +, /, =)
- Example: `a7B9cD3eF5gH7iJ9kL1mN3oP5qR7sT9uV1wX3yZ5`

**Error Responses**:
- `400 Bad Request`: Validation failed
- `404 Not Found`: Module not found

---

##### PUT /api/module-access-tokens/{id}
Update module access token.

**Request Body** (all fields optional):
```json
{
  "name": "Updated Token Name",
  "description": "Updated description",
  "isActive": false,
  "allowChat": true,
  "allowFileAccess": false
}
```

**Success Response** (200 OK): Updated token object.

**Use Cases**:
- Revoke access: Set `isActive` to false
- Modify permissions: Update `allowChat`, `allowFileAccess`

---

##### DELETE /api/module-access-tokens/{id}
Delete module access token.

**Success Response** (200 OK): Success message.

---

## Data Models

### User

```json
{
  "userId": 1,
  "username": "professor1",
  "email": "prof@university.edu",
  "firstName": "John",
  "lastName": "Doe",
  "userType": "professor",  // "super_admin", "professor", "student"
  "isActive": true,
  "universityId": 1,
  "isAdmin": true,  // Only for professors
  "courseId": null,  // Only for students
  "themePreference": "dark",  // "light", "dark", "system"
  "languagePreference": "en",  // "en", "pt-br", etc.
  "lastLoginAt": "2025-10-14T12:00:00Z",
  "createdAt": "2025-01-01T00:00:00Z"
}
```

### University

```json
{
  "id": 1,
  "name": "MIT",
  "code": "MIT",
  "description": "...",
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-01-01T00:00:00Z"
}
```

### Course

```json
{
  "id": 1,
  "name": "Computer Science",
  "code": "CS",
  "description": "...",
  "universityId": 1,
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-01-01T00:00:00Z"
}
```

### Module

```json
{
  "id": 1,
  "name": "Introduction to Python",
  "code": "CS101",
  "description": "...",
  "systemPrompt": "You are a helpful tutor...",
  "semester": 1,
  "year": 2025,
  "courseId": 1,
  "openAIAssistantId": "asst_abc123",
  "openAIVectorStoreId": "vs_abc123",
  "lastPromptImprovedAt": "2025-10-01T00:00:00Z",
  "promptImprovementCount": 5,
  "tutorLanguage": "en",
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-10-14T00:00:00Z"
}
```

### File

```json
{
  "id": 1,
  "fileName": "syllabus.pdf",
  "blobName": "university-1/course-1/module-1/syllabus.pdf",
  "contentType": "application/pdf",
  "size": 524288,
  "moduleId": 1,
  "openAIFileId": "file-abc123",
  "status": "completed",  // "pending", "processing", "completed", "failed"
  "errorMessage": null,
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-01-01T00:00:00Z"
}
```

### ModuleAccessToken

```json
{
  "id": 1,
  "token": "a7B9cD3eF5gH7iJ9kL1mN3oP5qR7sT9uV1wX3yZ5",
  "name": "Canvas Widget Token",
  "description": "...",
  "moduleId": 1,
  "createdByProfessorId": 1,
  "isActive": true,
  "expiresAt": "2026-10-14T00:00:00Z",
  "allowChat": true,
  "allowFileAccess": true,
  "usageCount": 42,
  "lastUsedAt": "2025-10-14T10:30:00Z",
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-10-14T00:00:00Z"
}
```

---

## Error Handling

### Standard Error Response

```json
{
  "message": "Error description",
  "errors": {
    "fieldName": ["Validation error message"]
  }
}
```

### HTTP Status Codes

| Code | Meaning | Common Causes |
|------|---------|---------------|
| 200 | OK | Successful GET, PUT, DELETE |
| 201 | Created | Successful POST |
| 400 | Bad Request | Validation failed, malformed request |
| 401 | Unauthorized | Missing/invalid token, inactive account |
| 403 | Forbidden | Insufficient permissions (policy check failed) |
| 404 | Not Found | Resource doesn't exist |
| 500 | Internal Server Error | Unexpected server error |

### Common Error Scenarios

#### Invalid Token
```
Status: 401 Unauthorized
{
  "message": "Invalid or expired token"
}
```

#### Insufficient Permissions
```
Status: 403 Forbidden
{
  "message": "User lacks required permissions"
}
```

#### Validation Errors
```
Status: 400 Bad Request
{
  "errors": {
    "email": ["The Email field is not a valid e-mail address."],
    "password": ["Password must be at least 8 characters"]
  }
}
```

#### Resource Not Found
```
Status: 404 Not Found
{
  "message": "Course not found"
}
```

---

## Rate Limiting

Both APIs implement IP-based rate limiting using AspNetCoreRateLimit.

### Default Limits (configurable in appsettings.json)

- **General endpoints**: 100 requests per minute
- **Auth endpoints**: 20 requests per minute (stricter to prevent brute force)

### Rate Limit Headers

Response headers indicate current rate limit status:

```
X-Rate-Limit-Limit: 100
X-Rate-Limit-Remaining: 95
X-Rate-Limit-Reset: 2025-10-14T12:01:00Z
```

### Rate Limit Exceeded Response

```
Status: 429 Too Many Requests
{
  "message": "Rate limit exceeded. Please try again later."
}
```

---

## Examples

### Example 1: Complete Student Registration Flow

```bash
# Step 1: Register new student
curl -X POST https://localhost:5001/api/auth/register/student \
  -H "Content-Type: application/json" \
  -d '{
    "username": "newstudent",
    "email": "student@mit.edu",
    "password": "SecurePass123!",
    "firstName": "Jane",
    "lastName": "Smith",
    "courseId": 1
  }'

# Response includes access token and user details
# Save the accessToken for subsequent requests
```

### Example 2: Professor Creates Module

```bash
# Step 1: Login as professor
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "professor1",
    "password": "ProfPass123!"
  }'

# Response includes accessToken
# Export token for convenience
export TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

# Step 2: Create new module
curl -X POST https://localhost:5002/api/modules \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Python Fundamentals",
    "code": "PY101",
    "description": "Introduction to Python programming",
    "systemPrompt": "You are a helpful Python tutor. Guide students...",
    "semester": 1,
    "year": 2025,
    "courseId": 1,
    "tutorLanguage": "en"
  }'

# Step 3: Upload file to module
curl -X POST https://localhost:5002/api/files \
  -H "Authorization: Bearer $TOKEN" \
  -F "moduleId=1" \
  -F "file=@/path/to/syllabus.pdf"

# Step 4: Create access token for widget
curl -X POST https://localhost:5002/api/module-access-tokens \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Canvas Widget",
    "description": "Token for Canvas LMS integration",
    "moduleId": 1,
    "expiresInDays": 365,
    "allowChat": true,
    "allowFileAccess": true
  }'

# Response includes generated token for widget use
```

### Example 3: Token Refresh Flow

```bash
# When access token expires (after 8 hours), refresh it
curl -X POST https://localhost:5001/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "YOUR_REFRESH_TOKEN"
  }'

# Response includes new access token and refresh token
```

### Example 4: Password Reset Flow

```bash
# Step 1: Request password reset
curl -X POST https://localhost:5001/api/auth/password-reset-request \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@university.edu"
  }'

# Step 2: User receives token via email (check logs in dev)
# Step 3: Reset password with token
curl -X POST https://localhost:5001/api/auth/password-reset \
  -H "Content-Type: application/json" \
  -d '{
    "token": "reset-token-from-email",
    "newPassword": "NewSecurePass123!"
  }'
```

### Example 5: Profile Management

```bash
# Get current user profile
curl -X GET https://localhost:5001/api/auth/me \
  -H "Authorization: Bearer $TOKEN"

# Update profile
curl -X PUT https://localhost:5001/api/auth/me \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "UpdatedName",
    "themePreference": "dark",
    "languagePreference": "pt-br"
  }'

# Change password
curl -X PUT https://localhost:5001/api/auth/me/password \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "currentPassword": "OldPassword123!",
    "newPassword": "NewSecurePass123!"
  }'
```

---

## Interactive Documentation

Both APIs provide interactive Swagger UI documentation in development mode:

- **Auth API Swagger**: https://localhost:5001/swagger
- **Management API Swagger**: https://localhost:5002/swagger

Swagger UI features:
- Try API endpoints directly from browser
- View request/response schemas
- See authentication requirements
- Test with your Bearer tokens

### Using Swagger UI

1. Navigate to the Swagger URL
2. Click "Authorize" button (Auth API: Bearer token, Management API: OAuth2)
3. Enter your access token
4. Try any endpoint by clicking "Try it out"

---

## Support and Resources

- **GitHub Repository**: [tutoria-chat/TutoriaApi](https://github.com/tutoria-chat/TutoriaApi)
- **Issue Tracker**: Report bugs and request features on GitHub Issues
- **API Version**: 1.0 (no versioning in routes - treating as new API)

---

## Changelog

### Version 1.0 (2025-10-14)
- Initial release
- Complete auth and management functionality
- Swagger documentation with XML comments
- JWT authentication with refresh tokens
- Module access tokens for widget integration
- Azure Blob Storage integration
- Rate limiting and logging
