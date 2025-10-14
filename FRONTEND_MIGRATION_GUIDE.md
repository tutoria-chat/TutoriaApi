# Frontend Migration Guide - .NET API

## Overview

This guide documents the new .NET API architecture and all changes the frontend team needs to know when migrating from the Python API to the .NET API.

## 🏗️ Architecture Changes

### API Split
The API has been split into two separate applications:

1. **Management API** (`https://localhost:5001`) - Business logic and data management
   - Universities, Courses, Modules
   - Professors, Students (CRUD operations)
   - Files, Module Access Tokens

2. **Auth API** (`https://localhost:5002`) - Authentication and user profile
   - Login, Registration
   - Password Reset
   - User Profile Management (`/me` endpoints)

### Key Architectural Decisions
- **Users Table**: Professors, Students, and Super Admins now use a unified `Users` table with a `UserType` field
- **No API Versioning**: Clean slate - no `/v1/` or `/v2/` in routes
- **Improved Prompt**: This endpoint stays in the Python API (AI-related functionality)

## 🔐 Authentication

### Login Flow

**Endpoint**: `POST /api/auth/login`
**URL**: `https://localhost:5002/api/auth/login`

**Request**:
```json
{
  "username": "john.doe",
  "password": "password123"
}
```

**Response**:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresIn": 28800,
  "user": {
    "userId": 1,
    "username": "john.doe",
    "email": "john@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "userType": "professor",
    "isActive": true,
    "universityId": 1,
    "universityName": "Example University",
    "isAdmin": true,
    "courseId": null,
    "courseName": null,
    "lastLoginAt": "2025-01-15T10:30:00Z",
    "createdAt": "2024-01-01T00:00:00Z",
    "themePreference": "system",
    "languagePreference": "pt-br"
  }
}
```

### Student Registration

**Endpoint**: `POST /api/auth/register/student`
**URL**: `https://localhost:5002/api/auth/register/student`

**Request**:
```json
{
  "username": "jane.student",
  "email": "jane@example.com",
  "firstName": "Jane",
  "lastName": "Student",
  "password": "password123",
  "courseId": 5
}
```

**Response**: Same as login response (automatically logs in the user)

### Password Reset Flow

#### Step 1: Request Password Reset

**Endpoint**: `POST /api/auth/password-reset-request`
**URL**: `https://localhost:5002/api/auth/password-reset-request`

**Request**:
```json
{
  "email": "john@example.com"
}
```

**Response**:
```json
{
  "message": "If the email exists, a password reset link has been sent"
}
```

**Note**: The response is always the same to prevent email enumeration attacks.

#### Step 2: Reset Password with Token

**Endpoint**: `POST /api/auth/password-reset`
**URL**: `https://localhost:5002/api/auth/password-reset`

**Request**:
```json
{
  "token": "abc123xyz789...",
  "newPassword": "newpassword123"
}
```

**Response**:
```json
{
  "message": "Password has been reset successfully"
}
```

## 👤 User Profile Management

All profile endpoints require authentication (include JWT token in `Authorization: Bearer {token}` header).

### Get Current User Profile

**Endpoint**: `GET /api/auth/me`
**URL**: `https://localhost:5002/api/auth/me`

**Response**:
```json
{
  "userId": 1,
  "username": "john.doe",
  "email": "john@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "userType": "professor",
  "isActive": true,
  "universityId": 1,
  "universityName": "Example University",
  "isAdmin": true,
  "courseId": null,
  "courseName": null,
  "lastLoginAt": "2025-01-15T10:30:00Z",
  "createdAt": "2024-01-01T00:00:00Z",
  "themePreference": "system",
  "languagePreference": "pt-br"
}
```

### Update Current User Profile

**Endpoint**: `PUT /api/auth/me`
**URL**: `https://localhost:5002/api/auth/me`

**Request** (all fields optional):
```json
{
  "firstName": "John",
  "lastName": "Doe",
  "email": "newemail@example.com",
  "themePreference": "dark",
  "languagePreference": "en"
}
```

**Response**: Updated user object (same structure as GET /me)

### Change Password (Authenticated User)

**Endpoint**: `PUT /api/auth/me/password`
**URL**: `https://localhost:5002/api/auth/me/password`

**Request**:
```json
{
  "currentPassword": "oldpassword123",
  "newPassword": "newpassword123"
}
```

**Response**:
```json
{
  "message": "Password changed successfully"
}
```

## 🏫 Management API Endpoints

All Management API endpoints require authentication.

### Universities

**Base URL**: `https://localhost:5001/api/universities`

- `GET /api/universities` - List universities (paginated)
  - Query params: `page`, `size`, `search`
- `GET /api/universities/{id}` - Get single university
- `POST /api/universities` - Create university
- `PUT /api/universities/{id}` - Update university
- `DELETE /api/universities/{id}` - Delete university

### Courses

**Base URL**: `https://localhost:5001/api/courses`

- `GET /api/courses` - List courses (paginated)
  - Query params: `page`, `size`, `universityId`, `search`
- `GET /api/courses/{id}` - Get single course
- `POST /api/courses` - Create course
- `PUT /api/courses/{id}` - Update course
- `DELETE /api/courses/{id}` - Delete course
- `POST /api/courses/{courseId}/professors/{professorId}` - Assign professor to course
- `DELETE /api/courses/{courseId}/professors/{professorId}` - Unassign professor from course

### Modules

**Base URL**: `https://localhost:5001/api/modules`

- `GET /api/modules` - List modules (paginated)
  - Query params: `page`, `size`, `courseId`, `semester`, `year`, `search`
- `GET /api/modules/{id}` - Get single module
- `POST /api/modules` - Create module
- `PUT /api/modules/{id}` - Update module
- `DELETE /api/modules/{id}` - Delete module

**⚠️ IMPORTANT**: The `/api/modules/{id}/improve-prompt` endpoint has been **REMOVED** from the .NET API. This functionality remains in the Python API as it's AI-related.

### Professors

**Base URL**: `https://localhost:5001/api/professors`

- `GET /api/professors` - List professors (paginated)
  - Query params: `page`, `size`, `universityId`, `isAdmin`, `search`
- `GET /api/professors/{id}` - Get single professor
- `POST /api/professors` - Create professor
- `PUT /api/professors/{id}` - Update professor
- `DELETE /api/professors/{id}` - Delete professor
- `PUT /api/professors/{id}/password` - Change professor password (admin only)

**Changes from Python API**:
- Now uses `Users` table with `userType = "professor"`
- `id` is now `userId` in the database
- `universityId` is nullable

### Students

**Base URL**: `https://localhost:5001/api/students`

- `GET /api/students` - List students (paginated)
  - Query params: `page`, `size`, `courseId`, `search`
- `GET /api/students/{id}` - Get single student
- `POST /api/students` - Create student
- `PUT /api/students/{id}` - Update student
- `DELETE /api/students/{id}` - Delete student
- `PUT /api/students/{id}/password` - Change student password (admin/professor only)

**Changes from Python API**:
- Now uses `Users` table with `userType = "student"`
- `id` is now `userId` in the database

### Files

**Base URL**: `https://localhost:5001/api/files`

- `GET /api/files` - List files (paginated)
  - Query params: `page`, `size`, `moduleId`, `search`
- `GET /api/files/{id}` - Get single file
- `POST /api/files` - Upload file (multipart/form-data)
- `GET /api/files/{id}/download` - Download file (redirects to SAS URL)
- `DELETE /api/files/{id}` - Delete file
- `PUT /api/files/{id}/status` - Update file status

**File Upload Request** (multipart/form-data):
```
POST /api/files
Content-Type: multipart/form-data

moduleId: 5
file: [binary file data]
```

**File Upload Response**:
```json
{
  "id": 123,
  "fileName": "lecture-notes.pdf",
  "blobName": "universities/1/courses/3/modules/5/abc123-def456.pdf",
  "contentType": "application/pdf",
  "size": 2048576,
  "moduleId": 5,
  "moduleName": "Introduction to Computer Science",
  "courseId": 3,
  "courseName": "CS 101",
  "universityId": 1,
  "universityName": "Example University",
  "openAIFileId": null,
  "status": "pending",
  "errorMessage": null,
  "createdAt": "2025-01-15T12:00:00Z",
  "updatedAt": "2025-01-15T12:00:00Z"
}
```

**File Download**:
- `GET /api/files/{id}/download` returns a 302 redirect to a SAS-secured Azure Blob Storage URL
- The SAS token is valid for 1 hour by default
- Frontend can either follow the redirect or extract the URL from the Location header

**Status Update Request**:
```json
{
  "status": "completed",
  "errorMessage": null,
  "openAIFileId": "file-abc123"
}
```

Valid status values: `pending`, `processing`, `completed`, `failed`

### Module Access Tokens

**Base URL**: `https://localhost:5001/api/module-access-tokens`

- `GET /api/module-access-tokens` - List tokens (paginated)
  - Query params: `page`, `size`, `moduleId`, `isActive`
- `GET /api/module-access-tokens/{id}` - Get single token
- `POST /api/module-access-tokens` - Create token
- `PUT /api/module-access-tokens/{id}` - Update token
- `DELETE /api/module-access-tokens/{id}` - Delete token

**Create Token Request**:
```json
{
  "name": "Student Access Token",
  "description": "Token for student access to module materials",
  "moduleId": 5,
  "allowChat": true,
  "allowFileAccess": true,
  "expiresInDays": 90
}
```

**Update Token Request** (all fields optional):
```json
{
  "name": "Updated Token Name",
  "description": "Updated description",
  "isActive": false,
  "allowChat": false,
  "allowFileAccess": true
}
```

## 📊 Pagination

All list endpoints support pagination with consistent query parameters:

- `page` (default: 1, min: 1)
- `size` (default: 10, min: 1, max: 100)

**Response Structure**:
```json
{
  "items": [ /* array of items */ ],
  "total": 150,
  "page": 1,
  "size": 10,
  "pages": 15
}
```

## 🔑 JWT Token Structure

The JWT token includes the following claims:
- `sub` (subject): User ID
- `type`: User type (`professor`, `student`, `super_admin`)
- `scopes`: Array of permission scopes

**Token Expiration**: 8 hours (28800 seconds)

## 🚫 Error Handling

### Standard Error Responses

**400 Bad Request**:
```json
{
  "message": "Validation error message",
  "errors": {
    "fieldName": ["Error message 1", "Error message 2"]
  }
}
```

**401 Unauthorized**:
```json
{
  "message": "Invalid username or password"
}
```

**404 Not Found**:
```json
{
  "message": "Resource not found"
}
```

**500 Internal Server Error**:
```json
{
  "message": "An unexpected error occurred"
}
```

## 🔄 Migration Checklist for Frontend

- [ ] Update API base URLs (split into Auth and Management)
- [ ] Update login endpoint to use new response structure
- [ ] Implement student registration flow
- [ ] Update password reset flow (2-step process)
- [ ] Add `/me` endpoints for profile management
- [ ] Remove references to `/improve-prompt` endpoint (use Python API instead)
- [ ] Update all user-related endpoints to use `userId` instead of `id` in some contexts
- [ ] Update file upload endpoints to use new .NET API with Azure Blob Storage
- [ ] Test pagination with new response structure
- [ ] Update error handling to match new error response formats
- [ ] Add support for theme and language preferences

## 📝 Property Naming

### C# to JSON Conversion

All C# properties use **PascalCase** in code but are automatically converted to **camelCase** in JSON responses:

- C#: `FirstName` → JSON: `firstName`
- C#: `UserId` → JSON: `userId`
- C#: `IsActive` → JSON: `isActive`

### Important Field Name Changes

| Old (Python API) | New (.NET API) | Notes |
|-----------------|----------------|-------|
| `id` (Professors/Students) | `userId` | Now uses Users table |
| `professor_id` | `professorId` | Converted to camelCase |
| `module_id` | `moduleId` | Converted to camelCase |
| `university_id` | `universityId` | Converted to camelCase |
| `is_active` | `isActive` | Converted to camelCase |
| `created_at` | `createdAt` | Converted to camelCase |
| `updated_at` | `updatedAt` | Converted to camelCase |

## 🔮 Future Enhancements

The following features are planned but not yet implemented:

1. **Professor Application System**:
   - Public endpoint for professor applications
   - SuperAdmin approval workflow
   - Email notifications
3. **JWT Refresh Tokens**: Token refresh endpoint
4. **Email Service**: Actual email sending for password resets and notifications
5. **Authorization Policies**: Role-based access control with policies

## 🆘 Support

For questions or issues with the API migration, please:
1. Check this guide first
2. Review the Swagger documentation at:
   - Management API: `https://localhost:5001/swagger`
   - Auth API: `https://localhost:5002/swagger`
3. Contact the backend team

---

**Last Updated**: 2025-01-15
**API Version**: 1.0
**Migration Status**: ✅ Core functionality complete, some features pending
