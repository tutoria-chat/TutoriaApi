# CI/CD Workflows

This directory contains GitHub Actions workflows for continuous integration and deployment.

## Workflows

### 1. Unified Pipeline (`pipeline.yml`) ⭐

**Main workflow for build, test, and deployment**

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main`
- Manual workflow dispatch with environment selection (dev/prod)

**Jobs:**
1. **build-and-test**: Builds solution, runs tests, and publishes artifacts
   - Uses .NET 8
   - Restores dependencies with caching
   - Builds in Release configuration
   - Runs tests with `dotnet test`
   - Publishes unified API (Management + Auth combined)
   - Uploads build artifacts for deployment

2. **deploy-dev**: Deploys to development environment
   - **Triggers**: Automatic on `main` push OR manual with environment=dev
   - Deploys to AWS Elastic Beanstalk (us-east-2)
   - Creates deployment package with production secrets
   - Uploads to S3 and deploys via EB
   - Cleans up old versions (keeps last 10)
   - **Environment**: `development` with protection rules

3. **deploy-prod**: Deploys to production environment
   - **Triggers**: Manual workflow dispatch ONLY (environment=prod)
   - Requires manual approval via GitHub Environments
   - Deploys to production Elastic Beanstalk instance
   - Cleans up old versions (keeps last 20)
   - **Environment**: `production` with protection rules

**Key Features:**
- ✅ Single unified workflow for entire CI/CD pipeline
- ✅ Job dependencies (deploy only runs if build succeeds)
- ✅ Shared artifacts between jobs (build once, deploy multiple times)
- ✅ Environment-specific secrets (DEV_* and PROD_* prefixes)
- ✅ Manual approval for production deployments
- ✅ Visual pipeline tracking in GitHub Actions UI

### 2. PR Checks (`pr-checks.yml`)

**Triggers:**
- Pull requests to `main` or `newapi`
- PR opened, synchronized, or reopened

**Jobs:**
- **pr-validation**: Full build and test validation
  - Builds in both Debug and Release configurations
  - Runs tests with code coverage
  - Caches NuGet packages for faster builds
  - Posts test summary comment on PR
  - Uploads test results as artifacts (14-day retention)

- **security-scan**: Security vulnerability scanning
  - Scans for vulnerable packages
  - Checks transitive dependencies
  - Uploads security scan results

### 3. Claude Code Review (`claude-code-review.yml`)

**Triggers:**
- Pull requests to any branch
- Automated code review by Claude AI

**Jobs:**
- Analyzes code changes
- Provides suggestions and feedback
- Comments on pull requests

### 4. Claude Integration (`claude.yml`)

**Triggers:**
- Workflow dispatch (manual)
- Integration with Claude AI tools

## Deployment Environments

### Development
- **Branch**: `main`
- **Auto-deploy**: ✅ Yes (on push to main)
- **Instance**: t3.micro (~$9/month or FREE with AWS Free Tier)
- **Region**: us-east-2
- **URL**: `https://tutoria-api-dev-env.eba-*.us-east-2.elasticbeanstalk.com`
- **Protection**: Basic (no approval required)

### Production
- **Branch**: N/A (manual deploy only)
- **Auto-deploy**: ❌ No (manual workflow dispatch)
- **Instance**: t3.micro (can upgrade later)
- **Region**: us-east-2
- **URL**: `https://tutoria-api-prod-env.eba-*.us-east-2.elasticbeanstalk.com`
- **Protection**: ⚠️ Requires manual approval via GitHub Environments

## Manual Deployment

To manually deploy to dev or prod:

1. Go to **Actions** → **Pipeline - Build, Test & Deploy**
2. Click **Run workflow**
3. Select environment:
   - `dev` - Deploy to development
   - `prod` - Deploy to production (requires approval)
4. Click **Run workflow**

## Required GitHub Secrets

### Development Secrets (16 total)
```
DEV_AWS_ACCESS_KEY_ID
DEV_AWS_SECRET_ACCESS_KEY
DEV_EB_S3_BUCKET
DEV_DB_CONNECTION_STRING
DEV_AZURE_STORAGE_CONNECTION_STRING
DEV_AZURE_STORAGE_CONTAINER
DEV_JWT_SECRET_KEY
DEV_JWT_ISSUER
DEV_JWT_AUDIENCE
DEV_OPENAI_API_KEY
DEV_AWS_SES_ACCESS_KEY_ID
DEV_AWS_SES_SECRET_ACCESS_KEY
DEV_AWS_SES_REGION
DEV_EMAIL_FROM_ADDRESS
DEV_EMAIL_FROM_NAME
DEV_EMAIL_FRONTEND_URL
DEV_EMAIL_LOGO_URL
```

### Production Secrets (16 total)
Same as above, but with `PROD_` prefix instead of `DEV_`

See `GITHUB_SECRETS_CHECKLIST.md` for detailed setup instructions.

## Test Structure

```
tests/
└── TutoriaApi.Tests.Unit/
    ├── PlaceholderTests.cs  # Placeholder tests (to be replaced)
    └── TutoriaApi.Tests.Unit.csproj
```

## Adding Tests

1. Create test classes in `tests/TutoriaApi.Tests.Unit/`
2. Use xUnit framework (`[Fact]` and `[Theory]` attributes)
3. Reference the projects you want to test:
   ```bash
   dotnet add tests/TutoriaApi.Tests.Unit reference src/TutoriaApi.Core
   dotnet add tests/TutoriaApi.Tests.Unit reference src/TutoriaApi.Infrastructure
   ```

## Running Tests Locally

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run in Release mode
dotnet test --configuration Release
```

## CI/CD Best Practices

- ✅ All tests must pass before deploying
- ✅ Build warnings are treated as errors in code quality checks
- ✅ Security scans run on every PR
- ✅ Test results are uploaded for review
- ✅ NuGet packages are cached for faster builds
- ✅ Production deployments require manual approval
- ✅ Separate secrets for dev and prod environments
- ✅ Automatic cleanup of old deployment versions

## Architecture Notes

### Unified API Deployment
The pipeline deploys `TutoriaApi.Web.API` which combines:
- **Management API**: `/api/universities`, `/api/courses`, `/api/modules`, etc.
- **Auth API**: `/api/auth/login`, `/api/auth/register`, `/api/auth/me`, etc.

Both APIs are deployed to a single Elastic Beanstalk instance for cost optimization.

**Future**: Split into separate deployments when traffic justifies the cost (~$17/month → ~$34/month)

## Future Enhancements

- [ ] Add integration tests
- [ ] Add end-to-end tests
- [ ] Add code coverage reporting with threshold enforcement
- [ ] Add Docker image building
- [ ] Add performance benchmarks
- [ ] Split API deployments (when budget allows)
- [ ] Add database migration automation
- [ ] Add automated rollback on deployment failure
