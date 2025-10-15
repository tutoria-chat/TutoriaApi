# CI/CD Workflows

This directory contains GitHub Actions workflows for continuous integration and deployment.

## Workflows

### 1. CI - Build and Test (`ci.yml`)

**Triggers:**
- Push to `main`, `newapi`, or `develop` branches
- Changes to source code (`src/**`, `tests/**`, `*.sln`)

**Jobs:**
- **build-and-test**: Builds the solution and runs unit tests
  - Uses .NET 8
  - Restores dependencies
  - Builds in Release configuration
  - Runs tests with `dotnet test`
  - Uploads test results as artifacts (30-day retention)
  - Generates test summary

- **code-quality**: Performs code quality checks
  - Checks code formatting with `dotnet format`
  - Builds with warnings as errors
  - Runs on every push

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

- ✅ All tests must pass before merging
- ✅ Build warnings are treated as errors in code quality checks
- ✅ Security scans run on every PR
- ✅ Test results are uploaded for review
- ✅ NuGet packages are cached for faster builds

## Future Enhancements

- [ ] Add integration tests
- [ ] Add end-to-end tests
- [ ] Add code coverage reporting
- [ ] Add deployment workflows (staging, production)
- [ ] Add Docker image building
- [ ] Add performance benchmarks
