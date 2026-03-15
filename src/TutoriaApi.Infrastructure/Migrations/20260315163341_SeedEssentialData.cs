using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <summary>
    /// Seeds essential data: AI Models, Plans, Permissions, RolePermissions, and API Clients.
    /// Converted from TutoriaDb post-deployment scripts (05, 09, 10, 11, 12).
    /// Uses INSERT ... ON CONFLICT DO NOTHING for idempotency.
    /// </summary>
    public partial class SeedEssentialData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =====================================================================
            // AI Models (from 05_AIModels.sql + 10_BedrockModels.sql)
            // =====================================================================
            migrationBuilder.Sql(@"
                INSERT INTO ""AIModels"" (""ModelName"", ""DisplayName"", ""Provider"", ""MaxTokens"", ""SupportsVision"", ""SupportsFunctionCalling"", ""InputCostPer1M"", ""OutputCostPer1M"", ""Description"", ""RecommendedFor"", ""IsActive"", ""IsDeprecated"", ""RequiredTier"", ""CreatedAt"", ""UpdatedAt"")
                VALUES
                    ('gpt-4-turbo', 'GPT-4 Turbo', 'openai', 128000, true, true, 10.00, 30.00, 'Latest GPT-4 Turbo with vision capabilities', 'Complex analysis, vision tasks', true, true, 3, NOW(), NOW()),
                    ('gpt-4o', 'GPT-4o', 'openai', 128000, true, true, 2.50, 10.00, 'GPT-4o - Optimized for speed and cost', 'General tutoring, balanced performance', true, false, 2, NOW(), NOW()),
                    ('gpt-4', 'GPT-4', 'openai', 8192, false, true, 30.00, 60.00, 'Original GPT-4 model', 'Legacy support', true, true, 3, NOW(), NOW()),
                    ('gpt-3.5-turbo', 'GPT-3.5 Turbo', 'openai', 16384, false, true, 0.50, 1.50, 'Fast and cost-effective model', 'Simple questions, high volume', true, true, 1, NOW(), NOW()),
                    ('gpt-4.1-turbo', 'GPT-4.1 Turbo', 'openai', 1048576, true, true, 2.00, 8.00, 'Next-gen GPT-4.1 Turbo with 1M context', 'Long documents, complex analysis', true, false, 2, NOW(), NOW()),
                    ('gpt-5', 'GPT-5', 'openai', 1048576, true, true, 2.00, 8.00, 'OpenAI GPT-5 flagship model', 'Premium tutoring, research-level tasks', true, false, 3, NOW(), NOW()),
                    ('claude-sonnet-4-5', 'Claude Sonnet 4.5', 'anthropic', 200000, true, true, 3.00, 15.00, 'Anthropic Claude Sonnet 4.5 - balanced', 'General tutoring, document analysis', true, false, 2, NOW(), NOW()),
                    ('claude-haiku-4-5', 'Claude Haiku 4.5', 'anthropic', 200000, true, true, 0.80, 4.00, 'Anthropic Claude Haiku 4.5 - fast and affordable', 'Quick answers, high throughput', true, false, 1, NOW(), NOW()),
                    ('claude-opus-4-1', 'Claude Opus 4.1', 'anthropic', 200000, true, true, 15.00, 75.00, 'Anthropic Claude Opus 4.1 - most powerful', 'Complex reasoning, research', true, false, 3, NOW(), NOW()),
                    ('claude-3-7-sonnet-20250219', 'Claude 3.7 Sonnet', 'anthropic', 200000, true, true, 3.00, 15.00, 'Anthropic Claude 3.7 Sonnet', 'Complex analysis, extended reasoning', true, false, 2, NOW(), NOW()),
                    ('claude-3-5-haiku-20241022', 'Claude 3.5 Haiku', 'anthropic', 200000, true, true, 0.80, 4.00, 'Anthropic Claude 3.5 Haiku - fast', 'Quick tutoring, simple questions', true, false, 1, NOW(), NOW()),
                    ('claude-3-haiku-20240307', 'Claude 3 Haiku', 'anthropic', 200000, false, false, 0.25, 1.25, 'Anthropic Claude 3 Haiku - lightweight', 'Basic tutoring, budget option', true, true, 1, NOW(), NOW()),
                    ('deepseek-chat', 'DeepSeek V3', 'deepseek', 65536, false, true, 0.27, 1.10, 'DeepSeek V3 chat model - cost-effective', 'Budget tutoring, coding assistance', true, false, 1, NOW(), NOW()),
                    ('deepseek-reasoner', 'DeepSeek R1', 'deepseek', 65536, false, true, 0.55, 2.19, 'DeepSeek R1 reasoning model', 'Math problems, logical reasoning', true, false, 2, NOW(), NOW()),
                    ('gemini-2.0-flash', 'Gemini 2.0 Flash', 'gemini', 1048576, true, true, 0.10, 0.40, 'Google Gemini 2.0 Flash - fast', 'Quick responses, multimedia', true, false, 1, NOW(), NOW()),
                    ('gemini-2.5-pro', 'Gemini 2.5 Pro', 'gemini', 1048576, true, true, 1.25, 10.00, 'Google Gemini 2.5 Pro - advanced', 'Complex analysis, long documents', true, false, 2, NOW(), NOW()),
                    ('llama-3.3-70b-versatile', 'Llama 3.3 70B', 'groq', 32768, false, true, 0.59, 0.79, 'Meta Llama 3.3 70B via Groq', 'General tutoring, open-source', true, false, 1, NOW(), NOW()),
                    ('mixtral-8x7b-32768', 'Mixtral 8x7B', 'groq', 32768, false, true, 0.24, 0.24, 'Mistral Mixtral 8x7B via Groq', 'Budget option, fast responses', true, false, 1, NOW(), NOW()),
                    ('anthropic.claude-3-5-sonnet-20241022-v2:0', 'Claude 3.5 Sonnet (Bedrock)', 'bedrock', 200000, true, true, 3.00, 15.00, 'Claude 3.5 Sonnet via AWS Bedrock', 'AWS-native tutoring, enterprise', true, false, 2, NOW(), NOW()),
                    ('anthropic.claude-3-haiku-20240307-v1:0', 'Claude 3 Haiku (Bedrock)', 'bedrock', 200000, false, false, 0.25, 1.25, 'Claude 3 Haiku via AWS Bedrock', 'AWS budget option', true, false, 1, NOW(), NOW()),
                    ('anthropic.claude-3-opus-20240229-v1:0', 'Claude 3 Opus (Bedrock)', 'bedrock', 200000, true, true, 15.00, 75.00, 'Claude 3 Opus via AWS Bedrock', 'AWS premium, complex reasoning', true, false, 3, NOW(), NOW())
                ON CONFLICT (""ModelName"") DO NOTHING;
            ");

            // =====================================================================
            // Plans (from 11_Plans.sql)
            // =====================================================================
            migrationBuilder.Sql(@"
                INSERT INTO ""Plans"" (""Name"", ""Slug"", ""Description"", ""MonthlyPriceBRL"", ""MaxCourses"", ""MaxModules"", ""MaxStudents"", ""HasAIQuizzes"", ""HasWhatsApp"", ""HasPrioritySupport"", ""HasCustomModelConfig"", ""TrialDays"", ""DisplayOrder"", ""IsActive"", ""IsCustom"", ""CreatedAt"", ""UpdatedAt"")
                VALUES
                    ('Starter', 'starter', 'Ideal para professores individuais ou pequenas disciplinas.', 2625.00, 3, 12, 1050, false, false, false, false, 14, 1, true, false, NOW(), NOW()),
                    ('Professional', 'professional', 'Para departamentos ou coordenacoes com multiplas disciplinas.', 6650.00, 8, 32, 2800, true, false, false, false, 14, 2, true, false, NOW(), NOW()),
                    ('Business', 'business', 'Para universidades com grande volume.', 15750.00, 20, 80, 7000, true, true, true, true, 14, 3, true, false, NOW(), NOW()),
                    ('Enterprise', 'enterprise', 'Solucao personalizada para grandes instituicoes.', 0.00, 999, 9999, NULL, true, true, true, true, 30, 4, true, true, NOW(), NOW())
                ON CONFLICT (""Slug"") DO NOTHING;
            ");

            // =====================================================================
            // Permissions (from 12_Permissions.sql)
            // =====================================================================
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Resource"", ""Action"", ""Scope"", ""Category"", ""Description"", ""DisplayOrder"")
                VALUES
                    ('universities:create', 'universities', 'create', 'global', 'University Management', 'Create new universities', 1),
                    ('universities:read', 'universities', 'read', 'global', 'University Management', 'View all universities', 2),
                    ('universities:update', 'universities', 'update', 'global', 'University Management', 'Edit university details', 3),
                    ('universities:delete', 'universities', 'delete', 'global', 'University Management', 'Remove universities', 4),
                    ('courses:create', 'courses', 'create', 'university', 'Course Management', 'Create courses', 5),
                    ('courses:read', 'courses', 'read', 'university', 'Course Management', 'View courses', 6),
                    ('courses:update', 'courses', 'update', 'university', 'Course Management', 'Edit courses', 7),
                    ('courses:delete', 'courses', 'delete', 'university', 'Course Management', 'Remove courses', 8),
                    ('modules:create', 'modules', 'create', 'university', 'Module Management', 'Create modules', 9),
                    ('modules:read', 'modules', 'read', 'university', 'Module Management', 'View modules', 10),
                    ('modules:update', 'modules', 'update', 'university', 'Module Management', 'Edit modules', 11),
                    ('modules:delete', 'modules', 'delete', 'university', 'Module Management', 'Remove modules', 12),
                    ('staff:create', 'staff', 'create', 'university', 'Staff Management', 'Create staff members', 13),
                    ('staff:read', 'staff', 'read', 'university', 'Staff Management', 'View staff', 14),
                    ('staff:update', 'staff', 'update', 'university', 'Staff Management', 'Edit staff', 15),
                    ('staff:delete', 'staff', 'delete', 'university', 'Staff Management', 'Remove staff', 16),
                    ('students:create', 'students', 'create', 'university', 'Student Management', 'Create student records', 17),
                    ('students:read', 'students', 'read', 'university', 'Student Management', 'View students', 18),
                    ('students:update', 'students', 'update', 'university', 'Student Management', 'Edit student records', 19),
                    ('students:delete', 'students', 'delete', 'university', 'Student Management', 'Remove students', 20),
                    ('files:create', 'files', 'create', 'university', 'File Management', 'Upload files', 21),
                    ('files:read', 'files', 'read', 'university', 'File Management', 'View and download files', 22),
                    ('files:update', 'files', 'update', 'university', 'File Management', 'Edit file metadata', 23),
                    ('files:delete', 'files', 'delete', 'university', 'File Management', 'Remove files', 24),
                    ('tokens:create', 'tokens', 'create', 'university', 'Token Management', 'Create access tokens', 25),
                    ('tokens:read', 'tokens', 'read', 'university', 'Token Management', 'View tokens', 26),
                    ('tokens:update', 'tokens', 'update', 'university', 'Token Management', 'Edit tokens', 27),
                    ('tokens:delete', 'tokens', 'delete', 'university', 'Token Management', 'Revoke tokens', 28),
                    ('analytics:read', 'analytics', 'read', 'university', 'Analytics', 'View analytics and reports', 29),
                    ('subscription:manage', 'subscription', 'manage', 'university', 'Subscription', 'Manage subscription and billing', 30)
                ON CONFLICT (""Code"") DO NOTHING;
            ");

            // =====================================================================
            // RolePermissions (from 12_Permissions.sql)
            // =====================================================================
            migrationBuilder.Sql(@"
                -- super_admin: all permissions
                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'super_admin', ""Id"" FROM ""Permissions""
                WHERE ""Id"" BETWEEN 1 AND 30
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;

                -- manager: all except university CRUD (5-30)
                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'manager', ""Id"" FROM ""Permissions""
                WHERE ""Id"" BETWEEN 5 AND 30
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;

                -- tutor: courses, modules, students:read, files, tokens
                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'tutor', ""Id"" FROM ""Permissions""
                WHERE ""Id"" IN (5,6,7,8,9,10,11,12,18,21,22,23,24,25,26,27,28)
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;

                -- platform_coordinator: read courses/modules/students, read files, tokens CRUD
                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'platform_coordinator', ""Id"" FROM ""Permissions""
                WHERE ""Id"" IN (6,10,18,22,25,26,27,28)
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;

                -- professor: courses:read, modules CRUD, students:read, files CRUD, tokens CRUD
                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'professor', ""Id"" FROM ""Permissions""
                WHERE ""Id"" IN (6,9,10,11,12,18,21,22,23,24,25,26,27,28)
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;

                -- student: courses:read, modules:read, files:read
                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'student', ""Id"" FROM ""Permissions""
                WHERE ""Id"" IN (6,10,22)
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;
            ");

            // =====================================================================
            // API Clients (from 09_ApiClients.sql)
            // =====================================================================
            migrationBuilder.Sql(@"
                INSERT INTO ""ApiClients"" (""ClientId"", ""HashedSecret"", ""Name"", ""Description"", ""IsActive"", ""Scopes"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (
                    'tutoria-ui-backend',
                    '$2a$12$8nOy9Rv97dZBklSJRLpC1e1FBxdmOR2NDfm36F7OKvx7v/5fFxaiG',
                    'Tutoria UI Backend',
                    'Next.js frontend backend API route authentication',
                    true,
                    '[""api.read"", ""api.write"", ""api.admin""]',
                    NOW(),
                    NOW()
                )
                ON CONFLICT (""ClientId"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"";");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"";");
            migrationBuilder.Sql(@"DELETE FROM ""Plans"";");
            migrationBuilder.Sql(@"DELETE FROM ""ApiClients"" WHERE ""ClientId"" = 'tutoria-ui-backend';");
            migrationBuilder.Sql(@"DELETE FROM ""AIModels"";");
        }
    }
}
