# Tutoria API - TODO List

## 🚨 Blocking - tutoria-worker follow-up (course-scoped activities)
**Status**: Pending | **Priority**: HIGH

Assignments and the question bank moved from module scope to course scope
(migration `MoveActivitiesToCourseScope`). `tutoria-worker` is a separate repo and
still speaks the old contract — it must ship with this change:

- [ ] Quiz-upload SQS payload is now `{ job_id, course_id }` (was `{ job_id, module_id }`)
  - Producer: `SqsMessagingService.SendQuizUploadJobAsync`
- [ ] `QuizUploadJobs.ModuleId` no longer exists — read `CourseId`
- [ ] `QuizExtractorService` now takes a `Course` (was `Module`) and writes
      `Quiz.course_id` with `module_id = NULL` (tutoria-api `app/services/quiz_extractor.py`)
- [ ] Quiz *generation* stays module-triggered; `quiz_generator.py` already fills
      `course_id` from the module's course, so no worker change is expected there —
      verify the worker doesn't set `module_id`/numbering itself


## 🔐 High Priority - Security & Compliance

### Secrets Management Migration
**Status**: Pending | **Priority**: HIGH

- [ ] Migrate from GitHub Secrets to AWS Secrets Manager
  - Currently using GitHub Secrets (DEV_* and PROD_* prefixes)
  - Target: AWS Secrets Manager (leverage AWS sponsor credits)
  - Benefits: Auto-rotation, audit logging, better security
  - Estimated: 2-3 days
  - See CLAUDE.md "Configuration & Secrets Management" for current setup

### Multi-Factor Authentication (TOTP)
**Status**: Pending | **Priority**: HIGH

- [ ] Implement TOTP-based MFA with Google/Microsoft Authenticator
  - Install `Otp.NET` and `QRCoder` NuGet packages
  - Add MFA fields to User entity (MfaEnabled, MfaSecretKey, etc.)
  - Create MfaBackupCode and MfaAuditLog entities
  - Implement MFA setup, enable, disable, verify endpoints
  - Update login flow to handle MFA verification
  - **Cost**: $0 (TOTP is free)
  - **Timeline**: 2 weeks

### Auth API Security Improvements
**Status**: Pending | **Priority**: MEDIUM

- [ ] Move reset token from query params to request body
  - Current: `POST /auth/reset-password?username={}&resetToken={}`
  - Target: `POST /auth/reset-password` with body `{ username, resetToken, newPassword }`
  - Rationale: Tokens in URLs can leak via logs/browser history
  - **Breaking change**: Frontend must be updated after deployment

## 📧 Email & Communication

### AWS SES Email Integration
**Status**: ✅ DONE with Resend | **Priority**: LOW

Email integration is complete using Resend. AWS SES migration is optional.

- [x] Email service implemented with Resend
- [ ] Optional: Migrate to AWS SES for cost optimization (if needed)

## 🔐 Authentication Features

### Professor Application System
**Status**: Pending | **Priority**: MEDIUM

- [ ] Create ProfessorApplication entity (application tracking)
- [ ] Implement `/auth/apply/professor` endpoint (public)
- [ ] Implement `/api/professor-applications` endpoints (SuperAdmin only)
  - GET (list applications), POST approve/reject
- [ ] Email templates (received, approved, rejected)
- [ ] Frontend integration document

## 📊 Analytics & Monitoring

### DynamoDB Chat Analytics
**Status**: ✅ DONE (handled in Python API) | **Priority**: N/A

Chat storage is implemented in the Python API using DynamoDB.

### Dashboard Performance
**Status**: Partial | **Priority**: MEDIUM

- [x] Unified dashboard endpoint (Phase 1)
- [x] Parallel query execution fixes
- [ ] Add DynamoDB GSI for university/course-level queries (future optimization)
- [ ] Pre-aggregated analytics table (future optimization)

## 🏗️ Infrastructure & Performance

### RabbitMQ Async Message Processing
**Status**: Pending | **Priority**: LOW

- [ ] Set up CloudAMQP (free tier)
- [ ] Implement async DynamoDB write queue
- [ ] Create .NET consumer app
- **Benefits**: 10-20% faster chat response times
- **Cost**: $0-30/month

### AWS CloudWatch Audit Logging
**Status**: Pending | **Priority**: MEDIUM

- [ ] Install AWSSDK.CloudWatchLogs package
- [ ] Create audit middleware (capture management actions only)
- [ ] Set up S3 archival with lifecycle policies
- [ ] Create Athena table for analytics queries
- **Cost**: $0-3/month (AWS free tier)

## 🎨 Features & Enhancements

### AI Model Tier Management
**Status**: Pending | **Priority**: MEDIUM

- [ ] Create endpoint: `GET /api/universities/{id}/models`
- [ ] Filter AI models by university subscription tier
- [ ] Move tier logic from frontend to backend (security + maintainability)

### FluentValidation
**Status**: Pending | **Priority**: LOW

- [ ] Add FluentValidation for complex validation scenarios
- [ ] Replace data annotations where needed

## 🚀 Deployment & CI/CD

### Current Status
- [x] GitHub Actions CI/CD pipeline
- [x] Unified deployment (Management + Auth APIs)
- [x] AWS Elastic Beanstalk deployment
- [ ] Split API deployments (future, when budget allows ~$34/month)

### Fix Duplicate GitHub Actions Runs
**Status**: Pending | **Priority**: LOW

- [ ] Remove duplicate triggers on PR workflows
- [ ] Currently runs on both `push` and `pull_request` events

## 🧪 Testing

### Unit Tests
**Status**: Partial | **Priority**: HIGH

- [x] Test project structure created
- [x] Placeholder tests passing
- [ ] **Add real unit tests for all new features (MANDATORY)**
  - Repository tests (mock DbContext)
  - Service tests (mock repositories)
  - Controller tests (mock services)
- [ ] Add integration tests
- [ ] Configure code coverage enforcement

## 📚 Documentation

### API Documentation
**Status**: Partial | **Priority**: MEDIUM

- [ ] Add XML documentation comments to all controllers
- [ ] Add ProducesResponseType attributes
- [ ] Enable XML file generation in .csproj
- [ ] Clean up inline comments
- [ ] Enhance Swagger UI with examples

### Code Quality
**Status**: Pending | **Priority**: LOW

- [ ] Standardize controller routes to use `[controller]` tag
- [ ] Replace hardcoded strings with constants
  - User types, claim types, scopes, policies, error messages
- [ ] Create constants files in TutoriaApi.Core/Constants/

## 🧹 Nice to Have

- [ ] Health check endpoints (partial - basic done, DB checks needed)
- [ ] Rate limiting (✅ DONE - AspNetCoreRateLimit)
- [ ] Request/response logging (✅ DONE - middleware)
- [ ] Global exception handling (✅ DONE)
- [ ] Serilog structured logging (✅ DONE)
- [ ] Password complexity validation (✅ DONE)
- [ ] Caching layer (Redis/In-Memory)
- [ ] API versioning
- [ ] Postman collection export

---

## 💡 Future Ideas - High Impact Features

These are strategic features for future consideration when core platform is stable:

### 1. Student Learning Analytics Dashboard ⭐⭐⭐⭐⭐
**Impact**: Massive | **Effort**: Medium | **Timeline**: Q2 2026

- Personal analytics with study time heatmaps
- Performance trends with grade predictions
- AI-detected weak topics
- Goal setting and streak tracking
- **Why**: 40% increase in engagement, 25% improvement in completion rates

### 2. Smart Flashcards + Spaced Repetition ⭐⭐⭐⭐⭐
**Impact**: Massive | **Effort**: Medium-High | **Timeline**: Q2 2026

- Auto-generate from PDFs/slides/chat history
- FSRS algorithm (better than Anki's SM-2)
- Mobile-optimized swipe interface
- Shared deck marketplace
- **Why**: 60% retention improvement, 50% increase in DAU, proven demand (Quizlet = $1B valuation)

### 3. Real-Time Collaboration & Study Rooms ⭐⭐⭐⭐
**Impact**: High | **Effort**: High | **Timeline**: Q4 2026

- Video/audio via WebRTC (Twilio/Agora)
- Collaborative whiteboard with LaTeX
- AI tutor available in study rooms
- Screen sharing + recording
- **Why**: 70% increase in collaboration, 35% completion boost, network effects

### 4. AI Content Generation for Professors ⭐⭐⭐⭐
**Impact**: High | **Effort**: Medium | **Timeline**: Q3 2026

- Quiz generation from PDFs
- Assignment prompts with rubrics
- Practice problems with solutions
- Export to Moodle/Canvas/PDF
- **Why**: 80% time savings, professor retention tool, $19.99/month premium feature

### 5. Mobile App (iOS & Android) ⭐⭐⭐⭐
**Impact**: High | **Effort**: Very High | **Timeline**: Q2-Q3 2026

- React Native or Flutter
- Offline mode (download content)
- Push notifications
- Camera integration (scan notes)
- **Why**: 60% of student time is mobile, 90% increase in mobile engagement

### 6. Gamification & Achievement System ⭐⭐⭐⭐
**Impact**: Medium-High | **Effort**: Medium | **Timeline**: Q2 2026

- XP system for all activities
- Levels 1-100 with rewards
- 100+ badges for milestones
- Daily/weekly streaks (Duolingo-style)
- Leaderboards
- **Why**: 45% increase in DAU, 60% increase in time spent, low cost/high impact

### 7. Assignment & Auto-Grading System ⭐⭐⭐⭐
**Impact**: Medium-High | **Effort**: Very High | **Timeline**: Q3 2026

- Multiple question types (MCQ, essay, code, file upload)
- Auto-grading + AI-assisted grading
- Plagiarism detection
- Peer review option
- **Why**: 70% grading time reduction, faster feedback, $29.99/month professor feature

### 8. Multi-Modal AI Tutor (Vision, Code, Math) ⭐⭐⭐⭐
**Impact**: Medium-High | **Effort**: High | **Timeline**: Q3 2026

- GPT-4V for image understanding
- Code execution sandbox (Judge0)
- LaTeX math rendering
- Wolfram Alpha integration
- Voice input
- **Why**: 80% increase in AI usefulness (especially STEM), premium justification

### 9. LMS Integration (Canvas, Moodle, Blackboard) ⭐⭐⭐⭐⭐
**Impact**: Very High | **Effort**: Very High | **Timeline**: Q4 2026-2027

- LTI 1.3 deep integration
- SSO with LMS credentials
- Grade sync to LMS gradebook
- Course roster sync
- **Why**: 90% adoption friction reduction, 10x university acquisition, path to 500K students

### 10. Advanced Teaching Analytics ⭐⭐⭐⭐
**Impact**: Medium-High | **Effort**: Medium | **Timeline**: Q3 2026

- Engagement metrics (watch time, quiz attempts)
- Topic difficulty heatmaps
- At-risk student prediction (ML model)
- Question hotspots from chat
- Peer benchmarking
- **Why**: 35% teaching effectiveness improvement, university license upsell

---

## 📊 Success Metrics

### Product Metrics
- Course pass rate: Target 85%
- Course completion rate: Target 70%
- DAU/MAU stickiness: Target 40%
- NPS: Target 50+

### Business Metrics
- ARR: Target $500K by Dec 2025
- Free-to-paid conversion: Target 8-12%
- Monthly churn: Target <5%
- LTV:CAC ratio: Target 5:1

### Technical Metrics
- API response time (p95): <500ms
- Uptime: 99.9% SLA
- AI response accuracy: >4.5/5

---

## 🚫 Ideas NOT Recommended

Say NO to these (focus on core value):
- ❌ Live video lectures (capital intensive, low margins)
- ❌ Content creation (not our core competency)
- ❌ Blockchain credentials (gimmick)
- ❌ VR/AR learning (too early, low adoption)
- ❌ Social network features (focus on learning)

---

**Last Updated**: January 2026
**Status**: Living document - update as priorities change
