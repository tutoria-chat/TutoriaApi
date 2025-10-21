# Elastic Beanstalk Deployment Setup Guide

This guide explains how to set up AWS Elastic Beanstalk deployment for the Tutoria API.

## Current Architecture (Single Deployment)

For cost optimization, we're currently deploying **both Management and Auth APIs in a single Elastic Beanstalk environment**. This means:
- Only ONE Elastic Beanstalk instance to pay for
- Both APIs run in the same .NET application
- Different base paths can be used to distinguish them (e.g., `/api/...` and `/auth/...`)

**TODO**: Split into separate deployments when budget allows (see TODO.md)

---

## Prerequisites

### 1. AWS Account Setup
- [ ] AWS account with Elastic Beanstalk access
- [ ] IAM user with permissions for:
  - Elastic Beanstalk (full access)
  - S3 (read/write to deployment bucket)
  - EC2 (for EB instances)
  - CloudWatch (for logs)
  - RDS (if using AWS database)

### 2. AWS Resources to Create

#### A. Elastic Beanstalk Application
```bash
# Using AWS CLI
aws elasticbeanstalk create-application \
  --application-name tutoria-api-dev \
  --description "Tutoria API - Development Environment"
```

Or create via AWS Console:
1. Go to Elastic Beanstalk console
2. Click "Create Application"
3. Name: `tutoria-api-dev`
4. Platform: `.NET Core on Linux`
5. Platform version: `.NET 8 running on 64bit Amazon Linux 2023`

#### B. Elastic Beanstalk Environment (Development)
```bash
aws elasticbeanstalk create-environment \
  --application-name tutoria-api-dev \
  --environment-name tutoria-api-dev-env \
  --solution-stack-name "64bit Amazon Linux 2023 v3.1.3 running .NET 8" \
  --option-settings \
    Namespace=aws:autoscaling:launchconfiguration,OptionName=InstanceType,Value=t3.micro \
    Namespace=aws:elasticbeanstalk:environment,OptionName=EnvironmentType,Value=SingleInstance
```

**Important Settings (Development)**:
- Instance Type: `t3.micro` (1GB RAM, ~$7.50/month or FREE with AWS Free Tier)
- Environment Type: `SingleInstance` (cheaper, suitable for dev)
- Health Check URL: `/health`

**For Production** (use when creating prod environment):
- Instance Type: `t3.small` or larger (2GB RAM minimum recommended)
- Environment Type: `LoadBalanced` with auto-scaling (higher availability)

#### C. S3 Bucket for Deployments
```bash
aws s3 mb s3://tutoria-api-deployments --region us-east-2
```

Or create via AWS Console:
1. S3 > Create bucket
2. Name: `tutoria-api-deployments` (or your preferred name)
3. Region: `us-east-2` (same as EB)
4. Keep defaults (private bucket)

---

## GitHub Secrets Configuration

Go to your GitHub repository settings > Secrets and variables > Actions, and add these secrets:

### Required Secrets for Development

| Secret Name | Description | Example Value | Where to Get |
|-------------|-------------|---------------|--------------|
| **AWS Credentials** ||||
| `DEV_AWS_ACCESS_KEY_ID` | AWS IAM access key | `AKIAIOSFODNN7EXAMPLE` | AWS IAM Console > Users > Security credentials |
| `DEV_AWS_SECRET_ACCESS_KEY` | AWS IAM secret key | `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY` | AWS IAM Console (shown once on creation) |
| **Elastic Beanstalk** ||||
| `DEV_EB_S3_BUCKET` | S3 bucket for deployments | `tutoria-api-deployments` | The bucket you created above |
| **Database** ||||
| `DEV_DB_CONNECTION_STRING` | SQL Server connection string | `Server=YOUR_SERVER.database.windows.net,1433;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASSWORD;...` | Azure Portal > SQL Database > Connection strings |
| **Azure Storage** ||||
| `DEV_AZURE_STORAGE_CONNECTION_STRING` | Azure Blob Storage connection | `DefaultEndpointsProtocol=https;AccountName=tutoria;AccountKey=...` | Azure Portal > Storage Account > Access keys |
| `DEV_AZURE_STORAGE_CONTAINER` | Blob container name | `nonprod` | Your container name |
| **JWT Configuration** ||||
| `DEV_JWT_SECRET_KEY` | JWT signing key (min 32 chars) | `YourSuperSecretKeyThatIsAtLeast32CharactersLong!` | Generate a strong random string |
| `DEV_JWT_ISSUER` | JWT issuer | `TutoriaAuthApi` | Your chosen issuer name |
| `DEV_JWT_AUDIENCE` | JWT audience | `TutoriaApi` | Your chosen audience name |
| **OpenAI (Optional)** ||||
| `DEV_OPENAI_API_KEY` | OpenAI API key | `sk-proj-...` | OpenAI Platform > API keys |
| **AWS Services (Optional)** ||||
| `DEV_AWS_SES_ACCESS_KEY_ID` | AWS SES access key (for emails) | `AKIAIOSFODNN7EXAMPLE` | AWS IAM Console (if using SES) |
| `DEV_AWS_SES_SECRET_ACCESS_KEY` | AWS SES secret key | `wJalrXUtnFEMI/...` | AWS IAM Console (if using SES) |
| `DEV_AWS_SES_REGION` | AWS SES region | `us-east-2` | Your AWS SES region |
| **Email Configuration** ||||
| `DEV_EMAIL_FROM_ADDRESS` | Email sender address | `noreply@yourdomain.com` | Your verified SES email |
| `DEV_EMAIL_FROM_NAME` | Email sender name | `Tutoria` | Your app name |
| `DEV_EMAIL_FRONTEND_URL` | Frontend URL for email links | `https://tutoria-dev.com` | Your dev frontend domain |

### Required Secrets for Production

| Secret Name | Description | Example Value | Where to Get |
|-------------|-------------|---------------|--------------|
| **AWS Credentials** ||||
| `PROD_AWS_ACCESS_KEY_ID` | AWS IAM access key | `AKIAIOSFODNN7EXAMPLE` | AWS IAM Console > Users > Security credentials |
| `PROD_AWS_SECRET_ACCESS_KEY` | AWS IAM secret key | `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY` | AWS IAM Console (shown once on creation) |
| **Elastic Beanstalk** ||||
| `PROD_EB_S3_BUCKET` | S3 bucket for deployments | `tutoria-api-deployments-prod` | The bucket you created above |
| **Database** ||||
| `PROD_DB_CONNECTION_STRING` | SQL Server connection string | `Server=YOUR_SERVER.database.windows.net,1433;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASSWORD;...` | Azure Portal > SQL Database > Connection strings |
| **Azure Storage** ||||
| `PROD_AZURE_STORAGE_CONNECTION_STRING` | Azure Blob Storage connection | `DefaultEndpointsProtocol=https;AccountName=tutoria;AccountKey=...` | Azure Portal > Storage Account > Access keys |
| `PROD_AZURE_STORAGE_CONTAINER` | Blob container name | `prod` | Your container name |
| **JWT Configuration** ||||
| `PROD_JWT_SECRET_KEY` | JWT signing key (min 32 chars) | `YourSuperSecretKeyThatIsAtLeast32CharactersLong!` | Generate a strong random string |
| `PROD_JWT_ISSUER` | JWT issuer | `TutoriaAuthApi` | Your chosen issuer name |
| `PROD_JWT_AUDIENCE` | JWT audience | `TutoriaApi` | Your chosen audience name |
| **OpenAI (Optional)** ||||
| `PROD_OPENAI_API_KEY` | OpenAI API key | `sk-proj-...` | OpenAI Platform > API keys |
| **AWS Services (Optional)** ||||
| `PROD_AWS_SES_ACCESS_KEY_ID` | AWS SES access key (for emails) | `AKIAIOSFODNN7EXAMPLE` | AWS IAM Console (if using SES) |
| `PROD_AWS_SES_SECRET_ACCESS_KEY` | AWS SES secret key | `wJalrXUtnFEMI/...` | AWS IAM Console (if using SES) |
| `PROD_AWS_SES_REGION` | AWS SES region | `us-east-2` | Your AWS SES region |
| **Email Configuration** ||||
| `PROD_EMAIL_FROM_ADDRESS` | Email sender address | `noreply@yourdomain.com` | Your verified SES email |
| `PROD_EMAIL_FROM_NAME` | Email sender name | `Tutoria` | Your app name |
| `PROD_EMAIL_FRONTEND_URL` | Frontend URL for email links | `https://tutoria.com` | Your production frontend domain |

### How to Add Secrets in GitHub

1. Go to your repository on GitHub
2. Click **Settings** > **Secrets and variables** > **Actions**
3. Click **New repository secret**
4. Enter the name and value
5. Click **Add secret**
6. Repeat for all secrets above

---

## Elastic Beanstalk Environment Variables

The deployment will set these environment variables automatically from GitHub secrets.

You can also set them manually in AWS Console:

1. Go to Elastic Beanstalk > Environments > Your environment
2. Click **Configuration** > **Software** > **Edit**
3. Add environment variables in the "Environment properties" section:

```bash
ConnectionStrings__DefaultConnection = <DB_CONNECTION_STRING>
AzureStorage__ConnectionString = <AZURE_STORAGE_CONNECTION_STRING>
AzureStorage__ContainerName = <AZURE_STORAGE_CONTAINER>
Jwt__SecretKey = <JWT_SECRET_KEY>
Jwt__Issuer = <JWT_ISSUER>
Jwt__Audience = <JWT_AUDIENCE>
OpenAI__ApiKey = <OPENAI_API_KEY>
AWS__AccessKeyId = <AWS_SES_ACCESS_KEY_ID>
AWS__SecretAccessKey = <AWS_SES_SECRET_ACCESS_KEY>
AWS__Region = us-east-2
Email__FromAddress = <EMAIL_FROM_ADDRESS>
Email__FromName = <EMAIL_FROM_NAME>
Email__FrontendUrl = <EMAIL_FRONTEND_URL>
Email__Enabled = True
ASPNETCORE_ENVIRONMENT = Production
```

**Note**: Environment variable names use `__` (double underscore) to represent `:` in appsettings.json hierarchy.

---

## Deploy Configuration Files

### web.config (for IIS/Kestrel)

Create `src/TutoriaApi.Web.Management/web.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet"
                arguments=".\TutoriaApi.Web.Management.dll"
                stdoutLogEnabled="true"
                stdoutLogFile=".\logs\stdout"
                hostingModel="InProcess">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
      </environmentVariables>
    </aspNetCore>
  </system.webServer>
</configuration>
```

### .ebextensions/01_dotnet.config (Elastic Beanstalk Configuration)

Create `.ebextensions/01_dotnet.config` in the project root:

```yaml
option_settings:
  aws:elasticbeanstalk:application:environment:
    ASPNETCORE_ENVIRONMENT: Production
  aws:elasticbeanstalk:environment:proxy:
    ProxyServer: nginx
  aws:autoscaling:launchconfiguration:
    InstanceType: t3.small
  aws:elasticbeanstalk:healthreporting:system:
    SystemType: enhanced
  aws:elasticbeanstalk:environment:process:default:
    HealthCheckPath: /health
    HealthCheckInterval: 30
    HealthCheckTimeout: 5
    UnhealthyThresholdCount: 3
    HealthyThresholdCount: 3

files:
  "/opt/elasticbeanstalk/tasks/taillogs.d/dotnet-logs.conf":
    mode: "000644"
    owner: root
    group: root
    content: |
      /var/app/current/logs/*.log
      /var/app/current/logs/*.txt
```

---

## Testing the Deployment

### 1. Manual Test Deployment

Before setting up CI/CD, test manually:

```bash
# Build and publish
cd TutoriaApi
dotnet publish src/TutoriaApi.Web.Management/TutoriaApi.Web.Management.csproj -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../deployment-package.zip .
cd ..

# Upload to Elastic Beanstalk (via AWS Console or CLI)
aws elasticbeanstalk create-application-version \
  --application-name tutoria-api-dev \
  --version-label manual-test-v1 \
  --source-bundle S3Bucket="tutoria-api-deployments",S3Key="manual-test.zip"

# Deploy
aws elasticbeanstalk update-environment \
  --application-name tutoria-api-dev \
  --environment-name tutoria-api-dev-env \
  --version-label manual-test-v1
```

### 2. Verify Deployment

```bash
# Get environment URL
aws elasticbeanstalk describe-environments \
  --application-name tutoria-api-dev \
  --environment-names tutoria-api-dev-env \
  --query "Environments[0].CNAME"

# Test endpoints
curl https://<your-eb-url>/health
curl https://<your-eb-url>/health/ready
curl https://<your-eb-url>/swagger
```

---

## Triggering Deployments

### Automatic Deployment (on push to develop)

```bash
git checkout develop
git add .
git commit -m "Deploy to dev"
git push origin develop
```

GitHub Actions will automatically:
1. Build the solution
2. Run tests
3. Publish the Management API
4. Create deployment package
5. Upload to S3
6. Deploy to Elastic Beanstalk

### Manual Deployment (GitHub Actions)

1. Go to GitHub > Actions tab
2. Select "Deploy to Elastic Beanstalk (Development)"
3. Click "Run workflow"
4. Select branch (usually `develop`)
5. Click "Run workflow"

---

## Monitoring & Logs

### View Logs in AWS Console

1. Elastic Beanstalk > Environments > Your environment
2. Click **Logs** > **Request Logs** > **Last 100 Lines** or **Full Logs**

### View Logs via AWS CLI

```bash
aws elasticbeanstalk retrieve-environment-info \
  --application-name tutoria-api-dev \
  --environment-name tutoria-api-dev-env \
  --info-type tail
```

### Application Logs

- Logs are written to `logs/tutoria-management-.log` in the application directory
- Elastic Beanstalk captures these and uploads to CloudWatch

### CloudWatch Logs

- Log Group: `/aws/elasticbeanstalk/tutoria-api-dev-env/...`
- Access via AWS Console > CloudWatch > Log groups

---

## Cost Estimation

### Development Environment (Single Instance - t3.micro)

| Resource | Type | Monthly Cost |
|----------|------|--------------|
| EC2 Instance | t3.micro (2 vCPU, 1GB RAM) | ~$7.50 (or **FREE** with AWS Free Tier) |
| EBS Volume | 10GB gp3 | ~$0.80 |
| Data Transfer | First 100GB free | $0 |
| S3 Storage | Deployments | ~$0.50 |
| CloudWatch Logs | Standard logging | ~$0.50 |
| **Total** || **~$9/month** (or **$1-2/month with Free Tier**) |

**💡 AWS Free Tier**: If your AWS account is less than 12 months old, t3.micro is FREE (750 hours/month)!

### Production Environment (Recommended)

| Resource | Type | Monthly Cost |
|----------|------|--------------|
| EC2 Instance | t3.small (2 vCPU, 2GB RAM) | ~$15 |
| Load Balancer | Application Load Balancer | ~$16 |
| EBS Volume | 10GB gp3 | ~$1 |
| Data Transfer | First 100GB free | $0 |
| **Total** || **~$32-40/month** |

**Note**: Production should use `LoadBalanced` environment type with auto-scaling for high availability.

---

## Troubleshooting

### Deployment Fails

**Check GitHub Actions logs**:
- Go to Actions tab
- Click on failed workflow
- Review build/deploy logs

**Check Elastic Beanstalk events**:
```bash
aws elasticbeanstalk describe-events \
  --application-name tutoria-api-dev \
  --environment-name tutoria-api-dev-env \
  --max-records 50
```

### Environment is "Degraded"

**Check health**:
```bash
aws elasticbeanstalk describe-environment-health \
  --environment-name tutoria-api-dev-env \
  --attribute-names All
```

**Common issues**:
- Health check failing → Verify `/health` endpoint works
- Database connection failing → Check connection string and firewall rules
- Insufficient memory → Upgrade instance type to t3.medium

### Application Errors

**View detailed logs**:
```bash
# Request full logs
aws elasticbeanstalk retrieve-environment-info \
  --application-name tutoria-api-dev \
  --environment-name tutoria-api-dev-env \
  --info-type bundle

# Get log URLs
aws elasticbeanstalk describe-environment-info \
  --application-name tutoria-api-dev \
  --environment-name tutoria-api-dev-env \
  --info-type bundle
```

---

## Next Steps

- [ ] Complete AWS setup (EB application, environment, S3 bucket)
- [ ] Add all GitHub secrets
- [ ] Test manual deployment
- [ ] Enable GitHub Actions workflow
- [ ] Monitor first automatic deployment
- [ ] Set up CloudWatch alarms for production
- [ ] **TODO**: Split into separate Management and Auth API deployments when budget allows

---

## Security Best Practices

1. **Never commit secrets** to git
2. **Use IAM roles** for EC2 instances (instead of access keys) in production
3. **Enable HTTPS** using AWS Certificate Manager (free SSL certificates)
4. **Restrict security groups** to allow only necessary traffic
5. **Enable CloudWatch alarms** for monitoring
6. **Rotate credentials** regularly (especially database passwords)
7. **Use AWS Secrets Manager** for production secrets (optional upgrade)

---

## References

- [AWS Elastic Beanstalk .NET Documentation](https://docs.aws.amazon.com/elasticbeanstalk/latest/dg/dotnet-core-tutorial.html)
- [Deploying .NET Core Apps to Elastic Beanstalk](https://docs.aws.amazon.com/elasticbeanstalk/latest/dg/create_deploy_NET.html)
- [GitHub Actions AWS Credentials](https://github.com/aws-actions/configure-aws-credentials)
