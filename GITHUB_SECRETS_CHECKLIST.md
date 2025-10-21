# GitHub Secrets Checklist for Elastic Beanstalk Deployment

## Quick Setup Guide

1. Go to GitHub repository: `https://github.com/YOUR_USERNAME/TutoriaApi`
2. Navigate to: **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret** for each secret below

---

## 📋 Overview

You need **TWO sets of secrets**: one for **Development** (DEV_*) and one for **Production** (PROD_*).

- **DEV_*** secrets are used by the `main` branch deployment
- **PROD_*** secrets are used by the `prod` branch deployment

**Total Secrets Needed**: ~30 (15 for dev + 15 for prod)

---

## 🔧 Development Secrets (DEV_*)

### ✅ AWS Credentials (Elastic Beanstalk Deployment)

```
Name: DEV_AWS_ACCESS_KEY_ID
Value: <Get from AWS IAM Console>
```

```
Name: DEV_AWS_SECRET_ACCESS_KEY
Value: <Get from AWS IAM Console>
```

```
Name: DEV_EB_S3_BUCKET
Value: tutoria-api-deployments
```

**Note**: Get from AWS IAM Console → Users → Security credentials

---

### ✅ Database Connection (SQL Server)

```
Name: DEV_DB_CONNECTION_STRING
Value: Server=YOUR_SERVER.database.windows.net,1433;Database=YOUR_DEV_DATABASE;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

**Note**: Get from Azure Portal → SQL Database → Connection strings

---

### ✅ Azure Storage (File Uploads)

```
Name: DEV_AZURE_STORAGE_CONNECTION_STRING
Value: DefaultEndpointsProtocol=https;AccountName=YOUR_ACCOUNT;AccountKey=YOUR_ACCOUNT_KEY_HERE;EndpointSuffix=core.windows.net
```

```
Name: DEV_AZURE_STORAGE_CONTAINER
Value: nonprod
```

**Note**: Get from Azure Portal → Storage Account → Access keys

---

### ✅ JWT Configuration (Authentication)

```
Name: DEV_JWT_SECRET_KEY
Value: YourSuperSecretKeyThatIsAtLeast32CharactersLongForDevelopment!
```

**💡 Tip**: Generate a secure JWT key:

```bash
# Linux/Mac/WSL
openssl rand -base64 32

# Windows PowerShell
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})
```

```
Name: DEV_JWT_ISSUER
Value: TutoriaAuthApi
```

```
Name: DEV_JWT_AUDIENCE
Value: TutoriaApi
```

---

### ✅ AWS SES (Email Service)

```
Name: DEV_AWS_SES_ACCESS_KEY_ID
Value: YOUR_AWS_ACCESS_KEY_ID
```

```
Name: DEV_AWS_SES_SECRET_ACCESS_KEY
Value: YOUR_AWS_SECRET_ACCESS_KEY
```

```
Name: DEV_AWS_SES_REGION
Value: us-east-2
```

**Note**: Get from AWS IAM Console → Users → Security credentials

---

### ✅ Email Configuration

```
Name: DEV_EMAIL_FROM_ADDRESS
Value: noreply@yourdomain.com
```

**Note**: Must be verified in AWS SES

```
Name: DEV_EMAIL_FROM_NAME
Value: Tutoria Dev
```

```
Name: DEV_EMAIL_FRONTEND_URL
Value: http://localhost:3000
```

---

### ❓ Optional Secrets (Development)

```
Name: DEV_OPENAI_API_KEY
Value: sk-proj-YOUR_KEY_HERE
```

---

## 🚀 Production Secrets (PROD_*)

### ✅ AWS Credentials (Elastic Beanstalk Deployment)

```
Name: PROD_AWS_ACCESS_KEY_ID
Value: <Get from AWS IAM Console>
```

```
Name: PROD_AWS_SECRET_ACCESS_KEY
Value: <Get from AWS IAM Console>
```

```
Name: PROD_EB_S3_BUCKET
Value: tutoria-api-deployments-prod
```

**⚠️ IMPORTANT**: Use separate AWS credentials for production!

---

### ✅ Database Connection (SQL Server)

```
Name: PROD_DB_CONNECTION_STRING
Value: Server=YOUR_PROD_SERVER.database.windows.net,1433;Database=YOUR_PROD_DATABASE;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

**⚠️ IMPORTANT**: Use production database, NOT dev database!

---

### ✅ Azure Storage (File Uploads)

```
Name: PROD_AZURE_STORAGE_CONNECTION_STRING
Value: DefaultEndpointsProtocol=https;AccountName=YOUR_PROD_ACCOUNT;AccountKey=YOUR_PROD_ACCOUNT_KEY_HERE;EndpointSuffix=core.windows.net
```

```
Name: PROD_AZURE_STORAGE_CONTAINER
Value: prod
```

**⚠️ IMPORTANT**: Use production storage account!

---

### ✅ JWT Configuration (Authentication)

```
Name: PROD_JWT_SECRET_KEY
Value: <Generate a STRONG random key - DIFFERENT from dev!>
```

**⚠️ CRITICAL**: Must be different from dev and VERY strong!

```
Name: PROD_JWT_ISSUER
Value: TutoriaAuthApi
```

```
Name: PROD_JWT_AUDIENCE
Value: TutoriaApi
```

---

### ✅ AWS SES (Email Service)

```
Name: PROD_AWS_SES_ACCESS_KEY_ID
Value: YOUR_PROD_AWS_ACCESS_KEY_ID
```

```
Name: PROD_AWS_SES_SECRET_ACCESS_KEY
Value: YOUR_PROD_AWS_SECRET_ACCESS_KEY
```

```
Name: PROD_AWS_SES_REGION
Value: us-east-2
```

---

### ✅ Email Configuration

```
Name: PROD_EMAIL_FROM_ADDRESS
Value: noreply@yourdomain.com
```

```
Name: PROD_EMAIL_FROM_NAME
Value: Tutoria
```

```
Name: PROD_EMAIL_FRONTEND_URL
Value: https://tutoria.com
```

---

### ❓ Optional Secrets (Production)

```
Name: PROD_OPENAI_API_KEY
Value: sk-proj-YOUR_PROD_KEY_HERE
```

---

## ✅ Verification Checklist

After adding all secrets, verify:

### Development (15 secrets)
- [ ] `DEV_AWS_ACCESS_KEY_ID`
- [ ] `DEV_AWS_SECRET_ACCESS_KEY`
- [ ] `DEV_EB_S3_BUCKET`
- [ ] `DEV_DB_CONNECTION_STRING`
- [ ] `DEV_AZURE_STORAGE_CONNECTION_STRING`
- [ ] `DEV_AZURE_STORAGE_CONTAINER`
- [ ] `DEV_JWT_SECRET_KEY` (32+ characters)
- [ ] `DEV_JWT_ISSUER`
- [ ] `DEV_JWT_AUDIENCE`
- [ ] `DEV_AWS_SES_ACCESS_KEY_ID`
- [ ] `DEV_AWS_SES_SECRET_ACCESS_KEY`
- [ ] `DEV_AWS_SES_REGION`
- [ ] `DEV_EMAIL_FROM_ADDRESS`
- [ ] `DEV_EMAIL_FROM_NAME`
- [ ] `DEV_EMAIL_FRONTEND_URL`

### Production (15 secrets)
- [ ] `PROD_AWS_ACCESS_KEY_ID`
- [ ] `PROD_AWS_SECRET_ACCESS_KEY`
- [ ] `PROD_EB_S3_BUCKET`
- [ ] `PROD_DB_CONNECTION_STRING`
- [ ] `PROD_AZURE_STORAGE_CONNECTION_STRING`
- [ ] `PROD_AZURE_STORAGE_CONTAINER`
- [ ] `PROD_JWT_SECRET_KEY` (32+ characters, DIFFERENT from dev)
- [ ] `PROD_JWT_ISSUER`
- [ ] `PROD_JWT_AUDIENCE`
- [ ] `PROD_AWS_SES_ACCESS_KEY_ID`
- [ ] `PROD_AWS_SES_SECRET_ACCESS_KEY`
- [ ] `PROD_AWS_SES_REGION`
- [ ] `PROD_EMAIL_FROM_ADDRESS`
- [ ] `PROD_EMAIL_FROM_NAME`
- [ ] `PROD_EMAIL_FRONTEND_URL`

### General Checks
- [ ] No typos in secret names (case-sensitive!)
- [ ] Connection strings include passwords
- [ ] AWS credentials have correct permissions
- [ ] Email addresses are verified in AWS SES
- [ ] Production secrets are DIFFERENT from development

---

## 🧪 Testing Secrets Configuration

### Development Deployment

```bash
# Push to main branch
git checkout main
git commit --allow-empty -m "Test dev deployment"
git push origin main
```

### Production Deployment

```bash
# Push to prod branch
git checkout prod
git commit --allow-empty -m "Test prod deployment"
git push origin prod
```

### Monitor Deployments

1. Go to **Actions** tab in GitHub
2. Watch deployment workflow
3. Check for errors in build/deploy steps

---

## 🔍 Common Errors

**Error**: `AWS credentials not found`
- **Fix**: Check `DEV_AWS_ACCESS_KEY_ID` / `PROD_AWS_ACCESS_KEY_ID` and secret access keys are set correctly

**Error**: `Database connection failed`
- **Fix**: Verify connection string is correct and firewall allows GitHub Actions IP

**Error**: `Blob storage authentication failed`
- **Fix**: Check Azure Storage connection string is valid

**Error**: `S3 bucket not found`
- **Fix**: Verify bucket exists and AWS credentials have S3 access

---

## 🔐 Security Best Practices

### ✅ DO

- ✅ Use **different secrets** for dev and prod
- ✅ Rotate credentials regularly (every 90 days)
- ✅ Use strong passwords and keys (32+ characters)
- ✅ Limit AWS IAM permissions to minimum required
- ✅ Enable AWS CloudTrail for audit logging
- ✅ Use separate AWS accounts for dev and prod (ideal)

### ❌ DON'T

- ❌ Never commit secrets to git (even in private repos)
- ❌ Never share secrets in chat/email (use password managers)
- ❌ Never use production credentials in development
- ❌ Never reuse the same password/key across environments
- ❌ Never grant more AWS permissions than needed

---

## 📍 Where to Find Secret Values

| Secret | Where to Find |
|--------|---------------|
| AWS Access Keys | AWS Console → IAM → Users → Security credentials → Create access key |
| Database Connection | Azure Portal → SQL Database → Connection strings → ADO.NET |
| Azure Storage | Azure Portal → Storage Account → Access keys → Connection string |
| JWT Secret | Generate random 32+ character string (see commands above) |
| AWS SES Keys | AWS Console → IAM → Users → Security credentials |
| Email Settings | AWS SES verified emails / Your email configuration |

---

## 🔑 IAM Permissions Required

Create an IAM user with these managed policies:
- `ElasticBeanstalkFullAccess`
- `AmazonS3FullAccess`

Or create custom policy:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "elasticbeanstalk:*",
        "s3:*",
        "ec2:DescribeInstances",
        "ec2:DescribeSecurityGroups",
        "elasticloadbalancing:DescribeLoadBalancers",
        "autoscaling:DescribeAutoScalingGroups",
        "cloudformation:DescribeStacks",
        "cloudformation:DescribeStackResources",
        "logs:CreateLogGroup",
        "logs:CreateLogStream",
        "logs:PutLogEvents"
      ],
      "Resource": "*"
    }
  ]
}
```

---

## 📚 Need Help?

- **Elastic Beanstalk Setup**: See `ELASTIC_BEANSTALK_SETUP.md`
- **Deployment Issues**: Check GitHub Actions logs and Elastic Beanstalk events
- **AWS Errors**: Review CloudWatch logs for the EB environment
- **Database Errors**: Verify firewall rules allow EB instance IP addresses

---

## 🎯 Next Steps

After adding all secrets:

1. ✅ Verify all secrets are added (use checklist above)
2. ✅ Push to `main` branch to test dev deployment
3. ✅ Monitor GitHub Actions for errors
4. ✅ Verify EB environment is healthy
5. ✅ Test API endpoints (`/health`, `/swagger`)
6. ✅ Set up production branch and deploy to prod
