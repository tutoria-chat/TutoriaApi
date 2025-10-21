# GitHub Secrets Checklist for Elastic Beanstalk Deployment

## Quick Setup Guide

1. Go to GitHub repository: `https://github.com/YOUR_USERNAME/TutoriaApi`
2. Navigate to: **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret** for each secret below

---

## Required Secrets (Copy-Paste Ready)

### ✅ AWS Credentials (Elastic Beanstalk Deployment)

```
Name: AWS_ACCESS_KEY_ID
Value: <Get from AWS IAM Console>
```

```
Name: AWS_SECRET_ACCESS_KEY
Value: <Get from AWS IAM Console>
```

```
Name: EB_S3_BUCKET
Value: tutoria-api-deployments
```

---

### ✅ Database Connection (SQL Server)

```
Name: DB_CONNECTION_STRING
Value: Server=YOUR_SERVER.database.windows.net,1433;Database=YOUR_DATABASE;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

**Note**: Get actual value from Azure Portal → SQL Database → Connection strings

---

### ✅ Azure Storage (File Uploads)

```
Name: AZURE_STORAGE_CONNECTION_STRING
Value: DefaultEndpointsProtocol=https;AccountName=YOUR_ACCOUNT;AccountKey=YOUR_ACCOUNT_KEY_HERE;EndpointSuffix=core.windows.net
```

**Note**: Get from Azure Portal → Storage Account → Access keys

```
Name: AZURE_STORAGE_CONTAINER
Value: your-container-name
```

**Note**: Use `nonprod` for dev, `prod` for production

---

### ✅ JWT Configuration (Authentication)

```
Name: JWT_SECRET_KEY
Value: YourSuperSecretKeyThatIsAtLeast32CharactersLongForDevelopment!
```

**⚠️ IMPORTANT**: Generate a strong random key for production!

```bash
# Generate a secure JWT key (Linux/Mac/WSL)
openssl rand -base64 32

# Or use PowerShell (Windows)
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})
```

```
Name: JWT_ISSUER
Value: TutoriaAuthApi
```

```
Name: JWT_AUDIENCE
Value: TutoriaApi
```

---

### ✅ AWS SES (Email Service)

```
Name: AWS_SES_ACCESS_KEY_ID
Value: YOUR_AWS_ACCESS_KEY_ID
```

**Note**: Get from AWS IAM Console → Users → Security credentials

```
Name: AWS_SES_SECRET_ACCESS_KEY
Value: YOUR_AWS_SECRET_ACCESS_KEY
```

**Note**: Shown only once when creating access key - save securely!

```
Name: AWS_SES_REGION
Value: us-east-2
```

**Note**: Use the AWS region where you configured SES

---

### ✅ Email Configuration

```
Name: EMAIL_FROM_ADDRESS
Value: noreply@yourdomain.com
```

**Note**: Must be verified in AWS SES

```
Name: EMAIL_FROM_NAME
Value: Your App Name
```

```
Name: EMAIL_FRONTEND_URL
Value: https://your-frontend-url.com
```

**Note**: Use `http://localhost:3000` for dev, production URL for prod

---

### ❓ Optional Secrets

#### OpenAI API (if using AI features)

```
Name: OPENAI_API_KEY
Value: sk-proj-YOUR_KEY_HERE
```

---

## Verification Checklist

After adding all secrets, verify:

- [ ] All secrets added (check count - should be 14+ secrets)
- [ ] No typos in secret names (case-sensitive!)
- [ ] Connection strings include passwords
- [ ] JWT_SECRET_KEY is strong (32+ characters)
- [ ] AWS credentials have correct permissions
- [ ] Email addresses are verified in AWS SES

---

## Testing Secrets Configuration

### 1. Trigger Deployment Workflow

```bash
# Push to develop branch
git checkout develop
git commit --allow-empty -m "Test deployment configuration"
git push origin develop
```

### 2. Monitor GitHub Actions

1. Go to **Actions** tab in GitHub
2. Watch "Deploy to Elastic Beanstalk (Development)" workflow
3. Check for errors in build/deploy steps

### 3. Common Errors

**Error**: `AWS credentials not found`
- **Fix**: Check `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` are set correctly

**Error**: `Database connection failed`
- **Fix**: Verify `DB_CONNECTION_STRING` is correct and firewall allows GitHub Actions IP

**Error**: `Blob storage authentication failed`
- **Fix**: Check `AZURE_STORAGE_CONNECTION_STRING` is valid

---

## Security Best Practices

### ✅ DO

- ✅ Use different secrets for dev/staging/prod
- ✅ Rotate credentials regularly
- ✅ Use strong passwords and keys
- ✅ Limit AWS IAM permissions to minimum required
- ✅ Enable AWS CloudTrail for audit logging

### ❌ DON'T

- ❌ Never commit secrets to git (even in private repos)
- ❌ Never share secrets in chat/email (use secure password managers)
- ❌ Never use production credentials in development
- ❌ Never reuse the same password across services
- ❌ Never grant more AWS permissions than needed

---

## Where to Find Secret Values

| Secret | Where to Find |
|--------|---------------|
| AWS Access Keys | AWS Console → IAM → Users → Security credentials → Create access key |
| Database Connection | Azure Portal → SQL Database → Connection strings → ADO.NET |
| Azure Storage | Azure Portal → Storage Account → Access keys → Connection string |
| JWT Secret | Generate random 32+ character string (see generation commands above) |
| AWS SES Keys | AWS Console → IAM → Users → Security credentials (same as AWS access keys or create separate) |
| Email Settings | Your email configuration / AWS SES verified emails |

---

## IAM Permissions Required for AWS User

Create an IAM user with these policies:

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

Or use AWS managed policies:
- `ElasticBeanstalkFullAccess`
- `AmazonS3FullAccess`

---

## Need Help?

- **Elastic Beanstalk Setup**: See `ELASTIC_BEANSTALK_SETUP.md`
- **Deployment Issues**: Check GitHub Actions logs and Elastic Beanstalk events
- **AWS Errors**: Review CloudWatch logs for the EB environment
- **Database Errors**: Verify firewall rules allow EB instance IP addresses

---

## Next Steps

After adding all secrets:

1. ✅ Commit and push to `develop` branch
2. ✅ Monitor GitHub Actions deployment
3. ✅ Verify EB environment is healthy
4. ✅ Test API endpoints (`/health`, `/swagger`)
5. ✅ Set up production environment and secrets
