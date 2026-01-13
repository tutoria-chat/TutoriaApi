# AWS SES Email Configuration Checklist

This document tracks the configuration needed for AWS SES email functionality in the Tutoria API.

---

## 1. appsettings.json - Add Email Section

**File:** `src/TutoriaApi.Web.API/appsettings.json`

Add this section:

```json
"Email": {
  "FromAddress": "noreply@yourdomain.com",
  "FromName": "Tutoria",
  "FrontendUrl": "http://localhost:3000",
  "LogoUrl": "https://your-cdn.com/tutoria-logo.png",
  "Enabled": true
}
```

| Setting | Description | Example |
|---------|-------------|---------|
| `FromAddress` | Verified sender email in AWS SES | `noreply@tutoria.com` |
| `FromName` | Display name in emails | `Tutoria Platform` |
| `FrontendUrl` | Base URL for password reset links | `https://app.tutoria.com` |
| `LogoUrl` | Logo image URL for email templates | `https://cdn.tutoria.com/logo.png` |
| `Enabled` | Enable/disable email sending | `true` or `false` |

---

## 2. AWS Credentials

**File:** `src/TutoriaApi.Web.API/appsettings.json`

Already exists, just ensure values are set:

```json
"AWS": {
  "Region": "sa-east-1",
  "AccessKeyId": "YOUR_ACCESS_KEY_HERE",
  "SecretAccessKey": "YOUR_SECRET_KEY_HERE"
}
```

---

## 3. AWS Console Setup

### 3.1 Verify Sender Identity

- [ ] Go to AWS SES Console → Region: `sa-east-1`
- [ ] Navigate to: **Verified Identities**
- [ ] Choose ONE:
  - [ ] **Verify Email**: Add `noreply@yourdomain.com` and click verification link
  - [ ] **Verify Domain**: Add domain and configure DNS records (recommended for production)

### 3.2 Request Production Access (if in Sandbox)

- [ ] Check if account is in Sandbox mode (SES Dashboard shows this)
- [ ] If in Sandbox: **Account Dashboard** → **Request Production Access**
- [ ] Fill out the request form (takes 24-48 hours for approval)

> **Sandbox Limitation:** Can only send emails to verified email addresses. Production access allows sending to anyone.

### 3.3 Create IAM User with SES Permissions

- [ ] Go to IAM Console → Users → Create User
- [ ] Attach this inline policy:

```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Action": "ses:SendEmail",
    "Resource": "*"
  }]
}
```

- [ ] Create Access Key → Save `AccessKeyId` and `SecretAccessKey`

---

## 4. GitHub Secrets for CI/CD

Add these secrets in: **GitHub Repo** → **Settings** → **Secrets and variables** → **Actions**

### Development Environment

| Secret | Value |
|--------|-------|
| `DEV_EMAIL_FROM_ADDRESS` | `noreply@dev.tutoria.com` |
| `DEV_EMAIL_FROM_NAME` | `Tutoria Dev` |
| `DEV_EMAIL_FRONTEND_URL` | `https://dev.tutoria.com` |
| `DEV_EMAIL_LOGO_URL` | `https://...` |
| `DEV_AWS_SES_REGION` | `sa-east-1` |
| `DEV_AWS_SES_ACCESS_KEY_ID` | `AKIA...` |
| `DEV_AWS_SES_SECRET_ACCESS_KEY` | `...` |

### Production Environment

| Secret | Value |
|--------|-------|
| `PROD_EMAIL_FROM_ADDRESS` | `noreply@tutoria.com` |
| `PROD_EMAIL_FROM_NAME` | `Tutoria` |
| `PROD_EMAIL_FRONTEND_URL` | `https://app.tutoria.com` |
| `PROD_EMAIL_LOGO_URL` | `https://...` |
| `PROD_AWS_SES_REGION` | `sa-east-1` |
| `PROD_AWS_SES_ACCESS_KEY_ID` | `AKIA...` |
| `PROD_AWS_SES_SECRET_ACCESS_KEY` | `...` |

---

## 5. Verification Checklist

After configuration, test these endpoints:

- [ ] `POST /api/auth/password-reset-request` - Should send password reset email
- [ ] `POST /api/users` - Should send welcome email to new user
- [ ] `POST /api/auth/password-reset` - Should send password changed confirmation
- [ ] `PUT /api/me/password` - Should send password changed confirmation

### Testing in Development (Email Disabled)

When `Email:Enabled` is `false` or AWS credentials are missing:
- Emails are **logged** instead of sent
- Check application logs to see email content
- User flows still work (non-blocking)

---

## Email Templates Supported

The following email types are already implemented with templates in EN, PT-BR, and ES:

| Email Type | Trigger | Link Expiry |
|------------|---------|-------------|
| Password Reset | `/api/auth/password-reset-request` | 1 hour |
| Welcome (New User) | `/api/users` | 24 hours |
| Password Changed | Password update endpoints | N/A |
| 2FA Code | (Future) | Configurable |
| Security Alert | (Future) | N/A |

---

## Files Reference

| File | Purpose |
|------|---------|
| `Core/Interfaces/IEmailService.cs` | Email service interface |
| `Infrastructure/Services/AwsSesEmailService.cs` | AWS SES implementation |
| `Infrastructure/DependencyInjection.cs` | AWS SES client registration |
| `Web.API/Controllers/AuthController.cs` | Password reset endpoints |
| `Web.API/Controllers/UsersController.cs` | User creation endpoint |

---

## Troubleshooting

**Emails not sending?**
1. Check `Email:Enabled` is `true`
2. Check AWS credentials are valid
3. Check sender email is verified in SES
4. Check not in sandbox mode (or recipient is verified)
5. Check application logs for errors

**"Email address not verified" error?**
- Verify the `FromAddress` in AWS SES Console

**"Access Denied" error?**
- IAM user needs `ses:SendEmail` permission

**Links in emails don't work?**
- Check `FrontendUrl` is correct
- Ensure frontend has `/reset-password` route
