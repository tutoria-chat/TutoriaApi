# AWS Elastic Beanstalk Deployment Guide

**TL;DR**: Elastic Beanstalk is the simplest, most cost-effective way to deploy your .NET 8 API to AWS with auto-scaling and zero infrastructure management. No Kubernetes complexity needed.

---

## Why Elastic Beanstalk?

### Comparison: Elastic Beanstalk vs ECS Fargate vs EC2

| Feature | Elastic Beanstalk | ECS Fargate | Raw EC2 |
|---------|-------------------|-------------|---------|
| **Complexity** | ⭐ Simple | ⭐⭐⭐ Complex | ⭐⭐⭐⭐ Very Complex |
| **Cost (Small)** | $8-10/month | $15-20/month | $8-10/month |
| **Auto-Scaling** | ✅ Built-in | ✅ Built-in | ❌ Manual setup |
| **Load Balancer** | ✅ Included | ✅ Included | ❌ Extra cost |
| **SSL/HTTPS** | ✅ Easy setup | ✅ Easy setup | ❌ Manual |
| **.NET 8 Support** | ✅ Native | ✅ Docker only | ✅ Manual install |
| **Zero Downtime Deploy** | ✅ Yes | ✅ Yes | ❌ Manual |
| **Health Monitoring** | ✅ Built-in | ✅ CloudWatch | ❌ Manual |
| **Infrastructure Management** | ✅ Managed | ⚠️ Partial | ❌ DIY |
| **Best For** | 🎯 Your use case | Microservices | High control |

**Recommendation**: Elastic Beanstalk is perfect for your needs:
- Simple deployment ("set it and forget it")
- Cost-effective with AWS credits
- Scales automatically when needed
- No K8s complexity
- .NET 8 natively supported

---

## Architecture Overview

### Recommended Setup: Dual-API on Single Environment

```
┌─────────────────────────────────────────────────────────┐
│  AWS Elastic Beanstalk Environment                      │
│  "tutoria-api-production"                               │
│                                                          │
│  ┌────────────────────────────────────────────────┐    │
│  │  Application Load Balancer (ALB)               │    │
│  │  - Handles HTTPS/SSL                           │    │
│  │  - Routes to healthy instances                 │    │
│  └─────────┬──────────────────────────────────────┘    │
│            │                                            │
│  ┌─────────┴──────────────────────────────────────┐    │
│  │  Auto Scaling Group                            │    │
│  │  (1-4 EC2 instances, scales based on CPU/RAM)  │    │
│  │                                                 │    │
│  │  ┌─────────────────────────────────────────┐   │    │
│  │  │  EC2 Instance (t3.small)                │   │    │
│  │  │                                         │   │    │
│  │  │  ┌──────────────────────────────────┐  │   │    │
│  │  │  │  TutoriaApi.Web.Management       │  │   │    │
│  │  │  │  (Port 5000)                     │  │   │    │
│  │  │  │  /api/universities, /api/courses │  │   │    │
│  │  │  └──────────────────────────────────┘  │   │    │
│  │  │                                         │   │    │
│  │  │  ┌──────────────────────────────────┐  │   │    │
│  │  │  │  TutoriaApi.Web.Auth             │  │   │    │
│  │  │  │  (Port 5001)                     │  │   │    │
│  │  │  │  /api/auth/*                     │  │   │    │
│  │  │  └──────────────────────────────────┘  │   │    │
│  │  └─────────────────────────────────────┘   │    │
│  └────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│  AWS RDS (Database)                                     │
│  - SQL Server Express (Free Tier eligible)              │
│  - Or SQL Server Web Edition ($30-50/month)             │
│  - Automatic backups, Multi-AZ for HA                   │
└─────────────────────────────────────────────────────────┘
```

### Routing Strategy

**Option A: Path-based routing (Recommended)**
- Single load balancer, single domain
- `https://api.tutoria.com/api/auth/*` → Auth API
- `https://api.tutoria.com/api/*` → Management API
- Simpler, cheaper (one SSL cert, one load balancer)

**Option B: Subdomain routing**
- `https://auth.tutoria.com` → Auth API
- `https://api.tutoria.com` → Management API
- Requires two SSL certs or wildcard cert

---

## Deployment Options

### Option 1: Single Environment, Both APIs (Recommended)

**How it works:**
- Bundle both `TutoriaApi.Web.Management` and `TutoriaApi.Web.Auth` into single deployment
- Use Kestrel to run both apps on different ports
- Use process manager (systemd/Windows Service) or supervisor
- ALB routes traffic based on path

**Pros:**
- ✅ Simplest deployment (one environment)
- ✅ Lowest cost ($8-10/month)
- ✅ Shared infrastructure
- ✅ Easy to manage

**Cons:**
- ❌ Both APIs scale together (not independent)
- ❌ Deployment affects both APIs

**When to use:** You're starting out, want simplicity, don't need independent scaling

---

### Option 2: Separate Environments (Future Growth)

**How it works:**
- Two Elastic Beanstalk environments
- `tutoria-auth-api` (Auth API only)
- `tutoria-management-api` (Management API only)
- Separate auto-scaling, separate deployments

**Pros:**
- ✅ Independent scaling (auth scales separately)
- ✅ Independent deployments (no downtime for other API)
- ✅ Better isolation
- ✅ Easier to monitor separately

**Cons:**
- ❌ Higher cost (~$16-20/month)
- ❌ More complexity

**When to use:** After you have significant traffic and need independent scaling

---

## Step-by-Step Deployment

### Prerequisites

1. **AWS Account** with credits activated
2. **AWS CLI** installed: https://aws.amazon.com/cli/
3. **EB CLI** installed: `pip install awsebcli`
4. **.NET 8 SDK** installed
5. **SQL Server database** (RDS or external)

---

### Step 1: Prepare Your Application

#### 1.1 Install AWS Elastic Beanstalk Tools

```bash
# Install EB CLI
pip install awsebcli --upgrade

# Verify installation
eb --version
```

#### 1.2 Update appsettings.json for Production

Create `appsettings.Production.json` in both API projects:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "#{TUTORIA_DB_CONNECTION}#"
  },
  "Jwt": {
    "Secret": "#{JWT_SECRET}#",
    "Issuer": "#{JWT_ISSUER}#",
    "Audience": "#{JWT_AUDIENCE}#"
  },
  "AzureStorage": {
    "ConnectionString": "#{AZURE_STORAGE_CONNECTION}#",
    "ContainerName": "tutoria-files"
  }
}
```

**Note**: `#{VAR_NAME}#` will be replaced by environment variables in EB.

#### 1.3 Build and Publish

```bash
# Navigate to your solution directory
cd D:\Users\Steve\Code\TutoriaApi

# Publish Management API
dotnet publish src/TutoriaApi.Web.Management/TutoriaApi.Web.Management.csproj `
  -c Release `
  -o ./publish/management `
  --self-contained false `
  --runtime linux-x64

# Publish Auth API
dotnet publish src/TutoriaApi.Web.Auth/TutoriaApi.Web.Auth.csproj `
  -c Release `
  -o ./publish/auth `
  --self-contained false `
  --runtime linux-x64
```

---

### Step 2: Initialize Elastic Beanstalk

#### 2.1 Initialize EB in Management API Directory

```bash
cd src/TutoriaApi.Web.Management
eb init
```

**Prompts:**
- **Select a region**: Choose closest to your users (e.g., `us-east-1`)
- **Application name**: `tutoria-api`
- **Platform**: `.NET Core on Linux`
- **Platform branch**: `.NET 8 running on 64bit Amazon Linux 2023`
- **SSH**: Yes (for debugging)
- **Keypair**: Create new or use existing

This creates `.elasticbeanstalk/config.yml`.

#### 2.2 Configure for Multi-API Deployment

Update `.elasticbeanstalk/config.yml`:

```yaml
branch-defaults:
  main:
    environment: tutoria-api-production
    group_suffix: null
global:
  application_name: tutoria-api
  branch: null
  default_ec2_keyname: your-keypair-name
  default_platform: .NET 8 running on 64bit Amazon Linux 2023
  default_region: us-east-1
  include_git_submodules: true
  instance_profile: null
  platform_name: null
  platform_version: null
  profile: eb-cli
  repository: null
  sc: git
  workspace_type: Application
```

---

### Step 3: Create RDS Database (SQL Server)

#### Option A: RDS SQL Server Express (Free Tier Eligible)

```bash
aws rds create-db-instance \
  --db-instance-identifier tutoria-db \
  --db-instance-class db.t3.micro \
  --engine sqlserver-ex \
  --master-username admin \
  --master-user-password YOUR_STRONG_PASSWORD \
  --allocated-storage 20 \
  --backup-retention-period 7 \
  --no-publicly-accessible \
  --vpc-security-group-ids sg-xxxxxx \
  --availability-zone us-east-1a
```

**Cost**: Free for 12 months (750 hours/month), then ~$15-20/month

#### Option B: RDS SQL Server Web Edition (Production)

```bash
aws rds create-db-instance \
  --db-instance-identifier tutoria-db-prod \
  --db-instance-class db.t3.small \
  --engine sqlserver-web \
  --license-model license-included \
  --master-username admin \
  --master-user-password YOUR_STRONG_PASSWORD \
  --allocated-storage 50 \
  --backup-retention-period 7 \
  --multi-az \
  --storage-encrypted \
  --vpc-security-group-ids sg-xxxxxx
```

**Cost**: ~$30-50/month

#### Get Connection String

Once created, get the endpoint:

```bash
aws rds describe-db-instances --db-instance-identifier tutoria-db
```

Connection string format:
```
Server={endpoint};Database=TutoriaDB;User Id=admin;Password={password};Encrypt=True;TrustServerCertificate=True;
```

---

### Step 4: Create Elastic Beanstalk Environment

#### 4.1 Create Environment

```bash
cd src/TutoriaApi.Web.Management

eb create tutoria-api-production \
  --instance-type t3.small \
  --platform ".NET 8 running on 64bit Amazon Linux 2023" \
  --region us-east-1 \
  --elb-type application \
  --enable-spot \
  --min-instances 1 \
  --max-instances 4 \
  --scale 2
```

**What this does:**
- Creates an Elastic Beanstalk environment named `tutoria-api-production`
- Uses `t3.small` instances (2 vCPU, 2GB RAM) - good starting point
- Enables spot instances (60-70% cost savings)
- Auto-scales from 1 to 4 instances based on load
- Creates Application Load Balancer
- Enables CloudWatch monitoring

#### 4.2 Set Environment Variables

```bash
eb setenv \
  ASPNETCORE_ENVIRONMENT=Production \
  TUTORIA_DB_CONNECTION="Server=tutoria-db.xxxxx.rds.amazonaws.com;Database=TutoriaDB;User Id=admin;Password=YourPassword;Encrypt=True;TrustServerCertificate=True;" \
  JWT_SECRET="YourSuperSecureJwtSecretKeyWithAtLeast32Characters" \
  JWT_ISSUER="https://api.tutoria.com" \
  JWT_AUDIENCE="https://api.tutoria.com" \
  AZURE_STORAGE_CONNECTION="DefaultEndpointsProtocol=https;AccountName=xxx;AccountKey=xxx;" \
  AWS_REGION="us-east-1"
```

**Security Note**: For production, use AWS Secrets Manager instead of environment variables:

```bash
# Store secrets in AWS Secrets Manager
aws secretsmanager create-secret \
  --name tutoria/db-connection \
  --secret-string "Server=...;Database=TutoriaDB;..."

# Reference in code
// Use AWS.Extensions.NETCore.Setup to load from Secrets Manager
```

---

### Step 5: Deploy Your Application

#### 5.1 Deploy Management API

```bash
cd src/TutoriaApi.Web.Management
dotnet publish -c Release

# Deploy to EB
eb deploy tutoria-api-production
```

This will:
1. Build your application
2. Create deployment package (zip)
3. Upload to S3
4. Deploy to EB environment
5. Run health checks
6. Route traffic to new version

#### 5.2 Monitor Deployment

```bash
# Watch deployment logs
eb logs --stream

# Check environment health
eb health

# Open in browser
eb open
```

---

### Step 6: Configure HTTPS/SSL

#### 6.1 Request SSL Certificate (AWS Certificate Manager)

```bash
# Request certificate for your domain
aws acm request-certificate \
  --domain-name api.tutoria.com \
  --subject-alternative-names "*.tutoria.com" \
  --validation-method DNS \
  --region us-east-1
```

**Cost**: FREE (AWS ACM certificates are free)

#### 6.2 Validate Domain Ownership

AWS will provide DNS records to add to your domain registrar.

```bash
# Check validation status
aws acm describe-certificate --certificate-arn arn:aws:acm:...
```

#### 6.3 Attach Certificate to Load Balancer

Via EB Console or CLI:

```bash
# Update load balancer listener
eb config

# Add under aws:elbv2:listener:443:
# Certificate: arn:aws:acm:us-east-1:123456789:certificate/xxx
# Protocol: HTTPS
```

Or use `.ebextensions/https.config`:

```yaml
option_settings:
  aws:elbv2:listener:443:
    Protocol: HTTPS
    SSLCertificateArns: arn:aws:acm:us-east-1:123456789:certificate/xxx
    ListenerEnabled: true
  aws:elbv2:listener:80:
    Protocol: HTTP
    ListenerEnabled: true
    Rules: redirect-to-https
```

---

### Step 7: Deploy Auth API (Option A: Same Environment)

If using single environment approach:

#### 7.1 Configure Process Manager

Create `.ebextensions/processes.config`:

```yaml
files:
  "/opt/elasticbeanstalk/tasks/taillogs.d/supervisor.conf":
    mode: "000644"
    owner: root
    group: root
    content: |
      /var/log/supervisor/*.log

  "/etc/supervisor/conf.d/tutoria-apps.conf":
    mode: "000644"
    owner: root
    group: root
    content: |
      [program:management-api]
      command=/usr/bin/dotnet /var/app/current/management/TutoriaApi.Web.Management.dll
      directory=/var/app/current/management
      autostart=true
      autorestart=true
      stdout_logfile=/var/log/supervisor/management-api.log
      stderr_logfile=/var/log/supervisor/management-api-error.log
      environment=ASPNETCORE_ENVIRONMENT=Production,ASPNETCORE_URLS="http://localhost:5000"

      [program:auth-api]
      command=/usr/bin/dotnet /var/app/current/auth/TutoriaApi.Web.Auth.dll
      directory=/var/app/current/auth
      autostart=true
      autorestart=true
      stdout_logfile=/var/log/supervisor/auth-api.log
      stderr_logfile=/var/log/supervisor/auth-api-error.log
      environment=ASPNETCORE_ENVIRONMENT=Production,ASPNETCORE_URLS="http://localhost:5001"

commands:
  01_install_supervisor:
    command: "yum install -y supervisor"
  02_start_supervisor:
    command: "systemctl start supervisord"
  03_enable_supervisor:
    command: "systemctl enable supervisord"
```

#### 7.2 Configure NGINX Reverse Proxy

Create `.ebextensions/nginx.config`:

```yaml
files:
  "/etc/nginx/conf.d/tutoria-proxy.conf":
    mode: "000644"
    owner: root
    group: root
    content: |
      upstream management-api {
          server 127.0.0.1:5000;
      }

      upstream auth-api {
          server 127.0.0.1:5001;
      }

      server {
          listen 80;

          location /api/auth {
              proxy_pass http://auth-api;
              proxy_http_version 1.1;
              proxy_set_header Upgrade $http_upgrade;
              proxy_set_header Connection keep-alive;
              proxy_set_header Host $host;
              proxy_cache_bypass $http_upgrade;
              proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
              proxy_set_header X-Forwarded-Proto $scheme;
          }

          location /api {
              proxy_pass http://management-api;
              proxy_http_version 1.1;
              proxy_set_header Upgrade $http_upgrade;
              proxy_set_header Connection keep-alive;
              proxy_set_header Host $host;
              proxy_cache_bypass $http_upgrade;
              proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
              proxy_set_header X-Forwarded-Proto $scheme;
          }

          location /health {
              access_log off;
              return 200 "healthy\n";
              add_header Content-Type text/plain;
          }
      }

commands:
  01_restart_nginx:
    command: "systemctl restart nginx"
```

#### 7.3 Update Deployment Package

```bash
# Create deployment directory structure
mkdir -p deploy-package/management
mkdir -p deploy-package/auth

# Copy published outputs
cp -r publish/management/* deploy-package/management/
cp -r publish/auth/* deploy-package/auth/

# Create zip
cd deploy-package
zip -r ../tutoria-api-deploy.zip .

# Deploy
eb deploy tutoria-api-production --staged
```

---

### Step 8: Configure Auto-Scaling

#### 8.1 Create Scaling Configuration

Via EB Console or `.ebextensions/autoscaling.config`:

```yaml
option_settings:
  aws:autoscaling:asg:
    MinSize: 1
    MaxSize: 4
    Cooldown: 300

  aws:autoscaling:trigger:
    MeasureName: CPUUtilization
    Statistic: Average
    Unit: Percent
    UpperThreshold: 70
    UpperBreachScaleIncrement: 1
    LowerThreshold: 30
    LowerBreachScaleIncrement: -1

  aws:elasticbeanstalk:environment:
    LoadBalancerType: application

  aws:elasticbeanstalk:healthreporting:system:
    SystemType: enhanced
```

**What this does:**
- Starts with 1 instance (cheap)
- Scales up when CPU > 70%
- Scales down when CPU < 30%
- Max 4 instances (prevents runaway costs)
- 5-minute cooldown between scaling actions

---

### Step 9: Set Up Database Migrations

#### 9.1 Create Migration Deployment Script

Create `.ebextensions/migrations.config`:

```yaml
container_commands:
  01_migrate_database:
    command: "dotnet ef database update --project /var/app/staging/management/TutoriaApi.Web.Management.dll"
    leader_only: true
```

**Note**: `leader_only: true` ensures migrations run only once (on one instance), not on all instances.

#### 9.2 Alternative: Manual Migrations

```bash
# SSH into EB instance
eb ssh tutoria-api-production

# Run migrations manually
cd /var/app/current/management
dotnet ef database update
```

---

### Step 10: Configure CloudWatch Alarms

#### 10.1 Create Alarms for Monitoring

```bash
# High CPU alarm
aws cloudwatch put-metric-alarm \
  --alarm-name tutoria-high-cpu \
  --alarm-description "Alert when CPU exceeds 80%" \
  --metric-name CPUUtilization \
  --namespace AWS/ElasticBeanstalk \
  --statistic Average \
  --period 300 \
  --threshold 80 \
  --comparison-operator GreaterThanThreshold \
  --evaluation-periods 2 \
  --alarm-actions arn:aws:sns:us-east-1:123456789:your-sns-topic

# High error rate alarm
aws cloudwatch put-metric-alarm \
  --alarm-name tutoria-high-errors \
  --alarm-description "Alert when 5xx errors exceed 10/minute" \
  --metric-name ApplicationRequests5xx \
  --namespace AWS/ElasticBeanstalk \
  --statistic Sum \
  --period 60 \
  --threshold 10 \
  --comparison-operator GreaterThanThreshold \
  --evaluation-periods 1
```

---

## CI/CD with GitHub Actions

### GitHub Actions Workflow

Create `.github/workflows/deploy-production.yml`:

```yaml
name: Deploy to AWS Elastic Beanstalk

on:
  push:
    branches:
      - main

env:
  AWS_REGION: us-east-1
  EB_APPLICATION_NAME: tutoria-api
  EB_ENVIRONMENT_NAME: tutoria-api-production

jobs:
  deploy:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v3

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Test
        run: dotnet test --no-restore --verbosity normal

      - name: Publish Management API
        run: |
          dotnet publish src/TutoriaApi.Web.Management/TutoriaApi.Web.Management.csproj \
            -c Release \
            -o ./publish/management \
            --no-restore

      - name: Publish Auth API
        run: |
          dotnet publish src/TutoriaApi.Web.Auth/TutoriaApi.Web.Auth.csproj \
            -c Release \
            -o ./publish/auth \
            --no-restore

      - name: Create deployment package
        run: |
          mkdir -p deploy-package
          cp -r publish/management deploy-package/
          cp -r publish/auth deploy-package/
          cp -r .ebextensions deploy-package/
          cd deploy-package
          zip -r ../tutoria-api-${{ github.sha }}.zip .

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v2
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ env.AWS_REGION }}

      - name: Upload package to S3
        run: |
          aws s3 cp tutoria-api-${{ github.sha }}.zip \
            s3://elasticbeanstalk-${{ env.AWS_REGION }}-${{ secrets.AWS_ACCOUNT_ID }}/tutoria-api-${{ github.sha }}.zip

      - name: Create EB application version
        run: |
          aws elasticbeanstalk create-application-version \
            --application-name ${{ env.EB_APPLICATION_NAME }} \
            --version-label ${{ github.sha }} \
            --source-bundle S3Bucket="elasticbeanstalk-${{ env.AWS_REGION }}-${{ secrets.AWS_ACCOUNT_ID }}",S3Key="tutoria-api-${{ github.sha }}.zip"

      - name: Deploy to Elastic Beanstalk
        run: |
          aws elasticbeanstalk update-environment \
            --application-name ${{ env.EB_APPLICATION_NAME }} \
            --environment-name ${{ env.EB_ENVIRONMENT_NAME }} \
            --version-label ${{ github.sha }}

      - name: Wait for deployment
        run: |
          aws elasticbeanstalk wait environment-updated \
            --application-name ${{ env.EB_APPLICATION_NAME }} \
            --environment-name ${{ env.EB_ENVIRONMENT_NAME }}

      - name: Verify deployment
        run: |
          HEALTH=$(aws elasticbeanstalk describe-environment-health \
            --environment-name ${{ env.EB_ENVIRONMENT_NAME }} \
            --attribute-names HealthStatus \
            --query 'HealthStatus' \
            --output text)

          if [ "$HEALTH" != "Ok" ]; then
            echo "Deployment health check failed: $HEALTH"
            exit 1
          fi

      - name: Notify deployment success
        if: success()
        run: echo "✅ Deployment to ${{ env.EB_ENVIRONMENT_NAME }} successful!"

      - name: Notify deployment failure
        if: failure()
        run: echo "❌ Deployment to ${{ env.EB_ENVIRONMENT_NAME }} failed!"
```

### Required GitHub Secrets

Add these to your repository secrets (Settings → Secrets → Actions):

- `AWS_ACCESS_KEY_ID`: Your AWS access key
- `AWS_SECRET_ACCESS_KEY`: Your AWS secret key
- `AWS_ACCOUNT_ID`: Your AWS account ID (12-digit number)

---

## Cost Breakdown

### Monthly Cost Estimates

#### Minimal Setup (Starting Out)
- **Elastic Beanstalk**: Free (only pay for resources)
- **EC2 t3.small (1 instance)**: $15.33/month (730 hours)
- **EC2 t3.small (Spot, 70% off)**: ~$4.60/month
- **Application Load Balancer**: $16.20/month + $0.008/LCU-hour
- **RDS SQL Server Express (t3.micro)**: FREE (12 months), then ~$15/month
- **Data Transfer**: $0.09/GB (first GB free)
- **S3 Storage (deployments)**: ~$0.50/month

**Total (with spot instances)**: ~$21-25/month
**Total (after free tier expires)**: ~$36-40/month

#### Medium Scale (100-500 users)
- **EC2 t3.small (2-3 instances, spot)**: ~$9-14/month
- **Application Load Balancer**: ~$20/month
- **RDS SQL Server Web (t3.small)**: ~$35/month
- **RDS Storage (50GB)**: ~$5/month
- **Data Transfer (100GB)**: ~$9/month

**Total**: ~$78-83/month

#### Large Scale (1,000-5,000 users)
- **EC2 t3.medium (2-4 instances)**: ~$60/month
- **Application Load Balancer**: ~$25/month
- **RDS SQL Server Web (t3.medium, Multi-AZ)**: ~$140/month
- **RDS Storage (200GB)**: ~$20/month
- **Data Transfer (500GB)**: ~$45/month

**Total**: ~$290/month

### Cost Optimization Tips

1. **Use Spot Instances**: 60-70% savings on EC2 costs
2. **Reserved Instances**: Save up to 40% for 1-year commitment
3. **Right-size Instances**: Start small (t3.small), scale as needed
4. **CloudWatch Cost Alarms**: Alert when costs exceed budget
5. **S3 Lifecycle Policies**: Delete old deployment packages
6. **RDS Backups**: 7-day retention sufficient for most cases

---

## Monitoring and Troubleshooting

### View Logs

```bash
# Stream all logs
eb logs --stream

# Download logs
eb logs --all

# View specific log file
eb ssh
tail -f /var/log/eb-engine.log
tail -f /var/log/supervisor/management-api.log
```

### Check Application Health

```bash
# Environment health
eb health

# Detailed health
eb health --view detailed

# CloudWatch metrics
aws cloudwatch get-metric-statistics \
  --namespace AWS/ElasticBeanstalk \
  --metric-name EnvironmentHealth \
  --dimensions Name=EnvironmentName,Value=tutoria-api-production \
  --start-time 2025-01-01T00:00:00Z \
  --end-time 2025-01-01T23:59:59Z \
  --period 3600 \
  --statistics Average
```

### Common Issues

#### 1. Database Connection Timeout
**Problem**: API can't connect to RDS
**Solution**: Update RDS security group to allow inbound from EB instances

```bash
# Get EB security group ID
aws elasticbeanstalk describe-environment-resources \
  --environment-name tutoria-api-production \
  --query "EnvironmentResources.Instances[0].Id"

# Allow traffic from EB to RDS
aws ec2 authorize-security-group-ingress \
  --group-id sg-rds-xxx \
  --protocol tcp \
  --port 1433 \
  --source-group sg-eb-xxx
```

#### 2. 502 Bad Gateway
**Problem**: Load balancer can't reach application
**Solution**: Check health check path matches your app

```bash
# Update health check to /health endpoint
eb config
# Set: application_healthcheck_url: /health
```

#### 3. Deployment Rollback
**Problem**: New deployment is broken
**Solution**: Rollback to previous version

```bash
# List versions
eb appversion lifecycle --list

# Rollback
eb deploy --version previous-version-label
```

---

## Security Best Practices

### 1. Use AWS Secrets Manager

```bash
# Store database password
aws secretsmanager create-secret \
  --name tutoria/db-password \
  --secret-string "YourStrongPassword"

# Load in application startup
using Amazon.SecretsManager;

var secret = await secretsManagerClient.GetSecretValueAsync(
    new GetSecretValueRequest { SecretId = "tutoria/db-password" });
```

### 2. Enable Enhanced Health Reporting

```bash
eb config
# Set: SystemType: enhanced
```

### 3. Configure HTTPS-Only

```yaml
# .ebextensions/https-redirect.config
option_settings:
  aws:elbv2:listener:80:
    Rules: https-redirect
```

### 4. Enable CloudTrail Logging

```bash
aws cloudtrail create-trail \
  --name tutoria-audit-trail \
  --s3-bucket-name tutoria-cloudtrail-logs
```

### 5. Regular Security Updates

```yaml
# .ebextensions/updates.config
commands:
  01_security_updates:
    command: "yum update -y --security"
```

---

## Summary: Quick Start Checklist

- [ ] Install AWS CLI and EB CLI
- [ ] Create RDS SQL Server database
- [ ] Initialize Elastic Beanstalk (`eb init`)
- [ ] Create environment (`eb create`)
- [ ] Set environment variables (`eb setenv`)
- [ ] Deploy application (`eb deploy`)
- [ ] Request SSL certificate (ACM)
- [ ] Configure HTTPS on load balancer
- [ ] Set up auto-scaling rules
- [ ] Configure CloudWatch alarms
- [ ] Set up GitHub Actions CI/CD
- [ ] Test deployment with Swagger
- [ ] Monitor costs and performance

---

## Next Steps

1. **Deploy to staging first**: Create `tutoria-api-staging` environment for testing
2. **Set up monitoring**: CloudWatch dashboards, SNS alerts
3. **Configure backups**: RDS automated backups, manual snapshots
4. **Performance testing**: Load test with locust/k6
5. **Cost optimization**: Review AWS Cost Explorer monthly

**You're now ready to deploy! 🚀**

For questions or issues, check:
- AWS Elastic Beanstalk docs: https://docs.aws.amazon.com/elasticbeanstalk/
- AWS .NET Developer Guide: https://docs.aws.amazon.com/sdk-for-net/
- Your AWS credits usage: AWS Billing Dashboard
