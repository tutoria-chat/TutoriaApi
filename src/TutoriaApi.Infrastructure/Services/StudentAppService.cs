using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.DTOs;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Enums;
using TutoriaApi.Core.Exceptions;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Helpers;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// TutorIA Estudantes (B2C app). Independent students are Users of the consumer
/// University: enrolled in its ENEM course (one module per area) and, on the
/// Universitário plan, in a personal Course whose Modules are their disciplines.
/// Everything AI (chat, ENEM, flashcards, essays) is served by the Python API on
/// top of those same courses/modules.
/// </summary>
public class StudentAppService : IStudentAppService
{
    private static readonly string[] UploadKinds = { "pdf", "pptx", "docx", "txt", "md", "jpg", "jpeg", "png", "webp", "heic" };

    private readonly IStudentAppRepository _repo;
    private readonly IUserRepository _users;
    private readonly IUserUniversityRepository _memberships;
    private readonly IStudentCourseRepository _enrollments;
    private readonly IConsentRepository _consents;
    private readonly ICourseRepository _courses;
    private readonly IModuleRepository _modules;
    private readonly IFileRepository _files;
    private readonly IBlobStorageService _blobs;
    private readonly ISqsMessagingService _sqs;
    private readonly IEmailService _email;
    private readonly StudentAppOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<StudentAppService> _logger;

    public StudentAppService(
        IStudentAppRepository repo,
        IUserRepository users,
        IUserUniversityRepository memberships,
        IStudentCourseRepository enrollments,
        IConsentRepository consents,
        ICourseRepository courses,
        IModuleRepository modules,
        IFileRepository files,
        IBlobStorageService blobs,
        ISqsMessagingService sqs,
        IEmailService email,
        IOptions<StudentAppOptions> options,
        TimeProvider clock,
        ILogger<StudentAppService> logger)
    {
        _repo = repo;
        _users = users;
        _memberships = memberships;
        _enrollments = enrollments;
        _consents = consents;
        _courses = courses;
        _modules = modules;
        _files = files;
        _blobs = blobs;
        _sqs = sqs;
        _email = email;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => StudentApp.TodayBrazil(UtcNow);

    public static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private async Task<University> ConsumerUniversityAsync()
        => await _repo.GetConsumerUniversityAsync()
           ?? throw new InvalidOperationException("Consumer university is not seeded");

    // ─── Accounts ────────────────────────────────────────────────────────────

    public async Task<User> RegisterAsync(StudentRegisterInput input)
    {
        if (!input.AcceptTerms)
            throw new StudentAppException(400, "terms_required", "Você precisa aceitar os Termos de Uso e a Política de Privacidade.");
        var name = string.Join(' ', (input.Name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length < 2)
            throw new StudentAppException(400, "invalid_name", "Informe seu nome.");
        if ((input.Password ?? string.Empty).Length < 8)
            throw new StudentAppException(400, "weak_password", "A senha precisa ter pelo menos 8 caracteres.");
        if (input.BirthDate >= Today || input.BirthDate.Year < 1900)
            throw new StudentAppException(400, "invalid_birth_date", "Data de nascimento inválida.");
        var birth = input.BirthDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (StudentApp.AgeOn(birth, Today) < StudentApp.MinAge)
            throw new StudentAppException(400, "too_young", $"O TutorIA Estudantes é para estudantes a partir de {StudentApp.MinAge} anos.");

        var email = input.Email.Trim().ToLowerInvariant();
        if (await _users.ExistsByEmailAsync(email) || await _users.ExistsByUsernameAsync(email))
            throw new StudentAppException(409, "email_taken", "Já existe uma conta com este e-mail.");

        var university = await ConsumerUniversityAsync();
        var parts = name.Split(' ', 2);
        var user = await _users.AddAsync(new User
        {
            Username = email,
            Email = email,
            FirstName = parts[0],
            LastName = parts.Length > 1 ? parts[1] : string.Empty,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(input.Password),
            UserType = UserTypes.Student,
            IsActive = true,
            UniversityId = university.Id,
            Birthdate = birth,
            LanguagePreference = "pt-br",
        });

        await _memberships.AddAsync(user.UserId, university.Id);
        var enem = await _repo.GetEnemCourseAsync(university.Id);
        if (enem != null) await _enrollments.EnrollStudentInCourseAsync(user.UserId, enem.Id);

        var now = UtcNow;
        await _repo.AddProfileAsync(new StudentProfile { UserId = user.UserId, TermsAcceptedAt = now });
        // LGPD: record what the student accepted (reuses the platform's Consents table).
        foreach (var type in new[] { "student_app_terms", "lgpd_privacy_policy" })
        {
            await _consents.AddAsync(new Consent { UserId = user.UserId, ConsentType = type, Version = "1.0", AcceptedAt = now });
        }

        _logger.LogInformation("Student app account created: {UserId}", user.UserId);
        return user;
    }

    public async Task<User> AuthenticateAsync(string email, string password)
    {
        var invalid = new StudentAppException(401, "invalid_credentials", "E-mail ou senha incorretos.");
        var user = await _users.GetByEmailAsync(email.Trim().ToLowerInvariant());
        if (user == null || string.IsNullOrEmpty(user.HashedPassword) || !BCrypt.Net.BCrypt.Verify(password, user.HashedPassword))
            throw invalid;
        if (!user.IsActive)
            throw new StudentAppException(403, "inactive", "Esta conta está desativada.");
        var university = await ConsumerUniversityAsync();
        if (user.UserType != UserTypes.Student || user.UniversityId != university.Id)
            // Institutional accounts sign in on the dashboard/widget, not in the consumer app.
            throw new StudentAppException(403, "not_student_app_account", "Esta conta é de uma instituição. Use o acesso da sua faculdade.");
        user.LastLoginAt = UtcNow;
        await _users.SaveChangesAsync();
        return user;
    }

    public async Task<User> RequireConsumerStudentAsync(int userId)
    {
        var user = await _users.GetByIdAsync(userId) ?? throw new StudentAppException(401, "unauthorized", "Sua sessão expirou. Entre novamente.");
        var university = await ConsumerUniversityAsync();
        if (user.UserType != UserTypes.Student || user.UniversityId != university.Id || !user.IsActive)
            throw new StudentAppException(403, "not_student_app_account", "Esta conta não é do TutorIA Estudantes.");
        return user;
    }

    // ─── Profile / entitlements ──────────────────────────────────────────────

    private async Task<StudentProfile> ProfileAsync(int userId)
    {
        var profile = await _repo.GetProfileAsync(userId);
        if (profile != null) return profile;
        profile = new StudentProfile { UserId = userId };
        await _repo.AddProfileAsync(profile);
        return profile;
    }

    public async Task<StudentMeResponse> GetMeAsync(int userId)
    {
        var user = await RequireConsumerStudentAsync(userId);
        var profile = await ProfileAsync(userId);
        var sub = await _repo.GetSubscriptionAsync(userId);
        var plan = StudentApp.ActivePlan(sub, UtcNow);
        var university = await ConsumerUniversityAsync();
        var enem = await _repo.GetEnemCourseAsync(university.Id);
        var areaModules = new Dictionary<string, int>();
        if (enem != null)
        {
            var modules = await _repo.GetModulesByCourseAsync(enem.Id);
            foreach (var (area, code) in StudentApp.AreaModuleCodes)
            {
                var m = modules.FirstOrDefault(x => x.Code == code);
                if (m != null) areaModules[area] = m.Id;
            }
        }

        var needsDisciplines = plan?.Id == "universitario"
            && (profile.PersonalCourseId == null || (await _repo.GetModulesByCourseAsync(profile.PersonalCourseId.Value)).Count == 0);
        var guardianOk = StudentApp.GuardianOk(user.Birthdate, profile, Today);
        var blocked = StudentApp.BelowMinAge(profile);
        var needsAreas = StudentApp.NeedsAreaChoice(plan, profile);
        var usage = await _repo.GetUsageAsync(userId, Today);

        return new StudentMeResponse
        {
            Id = user.UserId,
            Name = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email,
            BirthDate = user.Birthdate == null ? null : DateOnly.FromDateTime(user.Birthdate.Value),
            Age = user.Birthdate == null ? 0 : StudentApp.AgeOn(user.Birthdate.Value, Today),
            Track = profile.Track,
            GoalCourse = profile.GoalCourse,
            EnemYear = profile.EnemYear,
            University = profile.UniversityName,
            Major = profile.Major,
            Semester = profile.Semester,
            Plan = plan == null ? null : ToPlanResponse(plan),
            PlanSource = plan == null ? null : sub!.Source,
            PlanPeriodEnd = plan == null ? null : sub!.PeriodEnd,
            PlanWillRenew = plan == null ? null : sub!.WillRenew,
            PlanBillingIssue = plan != null && sub!.BillingIssue,
            Areas = StudentApp.AllowedAreas(plan, profile),
            SelectedAreas = StudentApp.SelectedAreas(profile),
            AreaModules = areaModules,
            NeedsAreaChoice = needsAreas,
            NeedsDisciplines = needsDisciplines,
            CanChangeAreas = StudentApp.CanChangeAreas(profile, sub),
            Guardian = new StudentGuardianResponse(
                StudentApp.IsMinor(user.Birthdate, profile, Today), profile.GuardianStatus, profile.GuardianEmail, profile.GuardianName),
            Ready = guardianOk && !blocked && plan != null && !needsAreas && !needsDisciplines,
            Blocked = blocked,
            MessagesToday = usage?.Messages ?? 0,
            Xp = await _repo.GetXpAsync(userId),
            NotifyStreak = profile.NotifyStreak,
            NotifyUpdates = profile.NotifyUpdates,
            AgeSignalChecked = profile.AgeSignalAt != null,
        };
    }

    public static StudentPlanResponse ToPlanResponse(StudentApp.PlanInfo p)
        => new(p.Id, p.Name, p.PriceCents, p.AreaCount, p.EssaysPerWeek, p.DailyMessages, p.Available, p.Highlights);

    private static string? Clean(string? value, int max)
    {
        var v = string.Join(' ', (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return v.Length == 0 ? null : v.Length > max ? v[..max] : v;
    }

    public async Task<StudentMeResponse> UpdateProfileAsync(int userId, StudentProfileUpdateInput input)
    {
        var user = await RequireConsumerStudentAsync(userId);
        var profile = await ProfileAsync(userId);

        if (input.Name != null)
        {
            var name = Clean(input.Name, 80) ?? throw new StudentAppException(400, "invalid_name", "Informe seu nome.");
            var parts = name.Split(' ', 2);
            user.FirstName = parts[0];
            user.LastName = parts.Length > 1 ? parts[1] : string.Empty;
            await _users.UpdateAsync(user);
        }
        if (input.Track != null)
        {
            if (input.Track != "enem" && input.Track != "university")
                throw new StudentAppException(400, "invalid_track", "Trilha inválida.");
            profile.Track = input.Track;
        }
        if (input.HasGoalCourse) profile.GoalCourse = Clean(input.GoalCourse, 80);
        if (input.HasEnemYear)
        {
            if (input.EnemYear is int y && (y < 2025 || y > 2035))
                throw new StudentAppException(400, "invalid_year", "Ano do ENEM inválido.");
            profile.EnemYear = input.EnemYear;
        }
        if (input.HasUniversity) profile.UniversityName = Clean(input.University, 120);
        if (input.HasMajor) profile.Major = Clean(input.Major, 80);
        if (input.HasSemester)
        {
            if (input.Semester is int s && (s < 1 || s > 14))
                throw new StudentAppException(400, "invalid_semester", "Semestre inválido.");
            profile.Semester = input.Semester;
        }
        if (input.NotifyStreak is bool ns) profile.NotifyStreak = ns;
        if (input.NotifyUpdates is bool nu) profile.NotifyUpdates = nu;
        await _repo.SaveChangesAsync();
        return await GetMeAsync(userId);
    }

    public async Task<StudentMeResponse> SetAreasAsync(int userId, IReadOnlyCollection<string> areas)
    {
        await RequireConsumerStudentAsync(userId);
        var profile = await ProfileAsync(userId);
        var sub = await _repo.GetSubscriptionAsync(userId);
        var plan = StudentApp.ActivePlan(sub, UtcNow)
                   ?? throw new StudentAppException(402, "plan_required", "Escolha um plano para continuar estudando.");
        if (plan.AreaCount == 0 || plan.AreaCount >= StudentApp.AreaIds.Length)
            throw new StudentAppException(400, "areas_not_selectable", "Seu plano não usa escolha de áreas.");
        var chosen = StudentApp.AreaIds.Where(areas.Contains).ToList();
        if (chosen.Count != plan.AreaCount || areas.Distinct().Count() != plan.AreaCount)
            throw new StudentAppException(400, "invalid_areas", $"Escolha exatamente {plan.AreaCount} áreas.");

        var current = StudentApp.SelectedAreas(profile);
        if (current.OrderBy(a => a).SequenceEqual(chosen.OrderBy(a => a)))
            return await GetMeAsync(userId);
        if (!StudentApp.CanChangeAreas(profile, sub))
            throw new StudentAppException(409, "areas_change_used",
                "Você já trocou de áreas neste ciclo. A próxima troca fica liberada na renovação do plano.");

        var firstChoice = current.Count == 0;
        profile.SelectedAreas = string.Join(',', chosen);
        if (!firstChoice) profile.AreasChangedPeriodStart = sub!.PeriodStart;
        await _repo.SaveChangesAsync();
        await EnsureDefaultAgentsAsync(userId, plan, profile);
        return await GetMeAsync(userId);
    }

    private static readonly Dictionary<string, (string Name, string Avatar)> DefaultAgents = new()
    {
        ["linguagens"] = ("Lia", "book"),
        ["humanas"] = ("Heitor", "globe"),
        ["natureza"] = ("Nina", "atom"),
        ["matematica"] = ("Teo", "calculator"),
    };

    public async Task EnsureDefaultAgentsAsync(int userId)
    {
        var profile = await ProfileAsync(userId);
        var plan = StudentApp.ActivePlan(await _repo.GetSubscriptionAsync(userId), UtcNow);
        await EnsureDefaultAgentsAsync(userId, plan, profile);
    }

    /// <summary>Gives every unlocked ENEM area a ready-made agent, unless the student already has one there.</summary>
    private async Task EnsureDefaultAgentsAsync(int userId, StudentApp.PlanInfo? plan, StudentProfile profile)
    {
        var allowed = StudentApp.AllowedAreas(plan, profile);
        if (allowed.Count == 0) return;
        var university = await ConsumerUniversityAsync();
        var enem = await _repo.GetEnemCourseAsync(university.Id);
        if (enem == null) return;
        var modules = await _repo.GetModulesByCourseAsync(enem.Id);
        var have = await _repo.GetAgentModuleIdsAsync(userId);
        foreach (var area in allowed)
        {
            var module = modules.FirstOrDefault(m => m.Code == StudentApp.AreaModuleCodes[area]);
            if (module == null || have.Contains(module.Id)) continue;
            var (name, avatar) = DefaultAgents[area];
            await _repo.AddAgentAsync(new StudentAgent
            {
                UserId = userId, ModuleId = module.Id, Name = name, Avatar = avatar, Tone = "amigavel", IsDefault = true,
            });
        }
    }

    // ─── Guardian consent ────────────────────────────────────────────────────

    public async Task<StudentMeResponse> RequestGuardianConsentAsync(int userId, string guardianName, string guardianEmail, string consentPageBaseUrl)
    {
        var user = await RequireConsumerStudentAsync(userId);
        var profile = await ProfileAsync(userId);
        if (!StudentApp.IsMinor(user.Birthdate, profile, Today))
            throw new StudentAppException(400, "not_minor", "A autorização do responsável é só para menores de 18 anos.");
        if (profile.GuardianStatus == "approved")
            return await GetMeAsync(userId);
        var name = Clean(guardianName, 80) ?? throw new StudentAppException(400, "invalid_name", "Informe o nome do responsável.");
        var email = guardianEmail.Trim().ToLowerInvariant();
        if (email == user.Email.ToLowerInvariant())
            throw new StudentAppException(400, "guardian_same_email", "Use o e-mail do seu responsável, não o seu.");

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        profile.GuardianName = name;
        profile.GuardianEmail = email;
        profile.GuardianTokenHash = Sha256(token);
        profile.GuardianRequestedAt = UtcNow;
        profile.GuardianStatus = "pending";
        await _repo.SaveChangesAsync();

        var link = $"{consentPageBaseUrl.TrimEnd('/')}/api/student-app/guardian/consent?token={token}";
        await _email.SendGuardianConsentEmailAsync(email, name, $"{user.FirstName} {user.LastName}".Trim(), link);
        return await GetMeAsync(userId);
    }

    private async Task<StudentProfile?> FindGuardianRequestAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var profile = await _repo.GetProfileByGuardianTokenHashAsync(Sha256(token));
        if (profile?.GuardianRequestedAt == null) return null;
        if (profile.GuardianRequestedAt.Value.AddDays(StudentApp.GuardianLinkDays) < UtcNow) return null;
        return profile;
    }

    public async Task<GuardianRequestInfo?> GetGuardianRequestAsync(string token)
    {
        var profile = await FindGuardianRequestAsync(token);
        if (profile == null) return null;
        var user = await _users.GetByIdAsync(profile.UserId);
        return user == null ? null : new GuardianRequestInfo($"{user.FirstName} {user.LastName}".Trim(), profile.GuardianName, _options.DpoEmail);
    }

    public async Task<string?> DecideGuardianConsentAsync(string token, bool approve, string? ipAddress, string? userAgent)
    {
        var profile = await FindGuardianRequestAsync(token);
        if (profile == null) return null;
        var user = await _users.GetByIdAsync(profile.UserId);
        if (user == null) return null;

        profile.GuardianStatus = approve ? "approved" : "denied";
        profile.GuardianDecidedAt = UtcNow;
        profile.GuardianTokenHash = null; // one decision per link
        await _repo.SaveChangesAsync();
        if (approve)
        {
            // Evidence of the guardian's consent (LGPD Art. 14 §1).
            await _consents.AddAsync(new Consent
            {
                UserId = user.UserId, ConsentType = "guardian_consent", Version = "1.0",
                IpAddress = ipAddress?.Length > 64 ? ipAddress[..64] : ipAddress,
                UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent,
                AcceptedAt = UtcNow,
            });
        }
        return $"{user.FirstName} {user.LastName}".Trim();
    }

    public async Task<StudentMeResponse> RecordAgeSignalAsync(int userId, string platform, int? lowerBound, int? upperBound, string? source)
    {
        await RequireConsumerStudentAsync(userId);
        if (platform != "ios" && platform != "android")
            throw new StudentAppException(400, "invalid_platform", "Plataforma inválida.");
        var profile = await ProfileAsync(userId);
        profile.AgeSignalLower = lowerBound;
        profile.AgeSignalUpper = upperBound;
        var src = $"{platform}:{source}";
        profile.AgeSignalSource = src.Length > 30 ? src[..30] : src;
        profile.AgeSignalAt = UtcNow;
        await _repo.SaveChangesAsync();
        return await GetMeAsync(userId);
    }

    // ─── Agents ──────────────────────────────────────────────────────────────

    private async Task<(StudentApp.PlanInfo? Plan, StudentProfile Profile, Dictionary<int, string> AreaByModule, Dictionary<int, Module> Disciplines)> ContextAsync(int userId)
    {
        var profile = await ProfileAsync(userId);
        var plan = StudentApp.ActivePlan(await _repo.GetSubscriptionAsync(userId), UtcNow);
        var university = await ConsumerUniversityAsync();
        var enem = await _repo.GetEnemCourseAsync(university.Id);
        var areaByModule = new Dictionary<int, string>();
        if (enem != null)
        {
            foreach (var m in await _repo.GetModulesByCourseAsync(enem.Id))
            {
                var area = StudentApp.AreaModuleCodes.FirstOrDefault(kv => kv.Value == m.Code).Key;
                if (area != null) areaByModule[m.Id] = area;
            }
        }
        var disciplines = profile.PersonalCourseId == null
            ? new Dictionary<int, Module>()
            : (await _repo.GetModulesByCourseAsync(profile.PersonalCourseId.Value)).ToDictionary(m => m.Id);
        return (plan, profile, areaByModule, disciplines);
    }

    private static StudentAgentResponse ToAgentResponse(StudentAgent a, StudentApp.PlanInfo? plan, StudentProfile profile,
        Dictionary<int, string> areaByModule, Dictionary<int, Module> disciplines)
    {
        var isDiscipline = disciplines.TryGetValue(a.ModuleId, out var discipline);
        var area = isDiscipline ? "disciplina" : areaByModule.GetValueOrDefault(a.ModuleId, "geral");
        var locked = isDiscipline
            ? plan?.Id != "universitario"
            : !StudentApp.AllowedAreas(plan, profile).Contains(area);
        return new StudentAgentResponse
        {
            Id = a.Id, Name = a.Name, Avatar = a.Avatar, Area = area, ModuleId = a.ModuleId,
            DisciplineId = isDiscipline ? a.ModuleId : null, DisciplineName = discipline?.Name,
            Tone = a.Tone, Instructions = a.Instructions, IsDefault = a.IsDefault, Locked = locked, CreatedAt = a.CreatedAt,
        };
    }

    public async Task<List<StudentAgentResponse>> GetAgentsAsync(int userId)
    {
        await RequireConsumerStudentAsync(userId);
        var (plan, profile, areas, disciplines) = await ContextAsync(userId);
        return (await _repo.GetAgentsAsync(userId)).Select(a => ToAgentResponse(a, plan, profile, areas, disciplines)).ToList();
    }

    private async Task<StudentAgent> OwnedAgentAsync(int userId, int agentId)
    {
        var agent = await _repo.GetAgentAsync(agentId);
        if (agent == null || agent.UserId != userId) throw StudentAppException.NotFound("Agente");
        return agent;
    }

    public async Task<StudentAgentResponse> GetAgentAsync(int userId, int agentId)
    {
        await RequireConsumerStudentAsync(userId);
        var agent = await OwnedAgentAsync(userId, agentId);
        var (plan, profile, areas, disciplines) = await ContextAsync(userId);
        return ToAgentResponse(agent, plan, profile, areas, disciplines);
    }

    private StudentApp.PlanInfo RequireReady(User user, StudentProfile profile, StudentApp.PlanInfo? plan)
    {
        if (StudentApp.BelowMinAge(profile))
            throw new StudentAppException(403, "too_young", $"O TutorIA Estudantes é para estudantes a partir de {StudentApp.MinAge} anos.");
        if (!StudentApp.GuardianOk(user.Birthdate, profile, Today))
            throw new StudentAppException(403, "guardian_required", "Precisamos da autorização do seu responsável para liberar o app.");
        return plan ?? throw new StudentAppException(402, "plan_required", "Escolha um plano para continuar estudando.");
    }

    private static void ValidateAgentFields(StudentAgentInput input, bool creating)
    {
        if (creating || input.Name != null)
        {
            var name = (input.Name ?? string.Empty).Trim();
            if (name.Length < 1 || name.Length > 40) throw new StudentAppException(400, "invalid_name", "O nome do agente deve ter até 40 caracteres.");
        }
        if (input.Tone != null && !StudentApp.Tones.Contains(input.Tone))
            throw new StudentAppException(400, "invalid_tone", "Jeito de ensinar inválido.");
        if (input.Instructions != null && input.Instructions.Length > 1000)
            throw new StudentAppException(400, "instructions_too_long", "As instruções podem ter até 1000 caracteres.");
        if (input.Avatar != null && input.Avatar.Length > 20)
            throw new StudentAppException(400, "invalid_avatar", "Ícone inválido.");
    }

    public async Task<StudentAgentResponse> CreateAgentAsync(int userId, StudentAgentInput input)
    {
        var user = await RequireConsumerStudentAsync(userId);
        var (plan, profile, areas, disciplines) = await ContextAsync(userId);
        var active = RequireReady(user, profile, plan);
        ValidateAgentFields(input, creating: true);

        int moduleId;
        if (input.Area == "disciplina")
        {
            if (active.Id != "universitario")
                throw new StudentAppException(403, "plan_feature", "As disciplinas fazem parte do plano Universitário.");
            if (input.DisciplineId is not int did || !disciplines.ContainsKey(did))
                throw new StudentAppException(400, "invalid_discipline", "Escolha uma das suas disciplinas.");
            moduleId = did;
        }
        else
        {
            if (input.Area == null || !StudentApp.AreaIds.Contains(input.Area))
                throw new StudentAppException(400, "invalid_area", "Área inválida.");
            if (!StudentApp.AllowedAreas(active, profile).Contains(input.Area))
                throw new StudentAppException(403, "area_locked", $"{StudentApp.AreaLabels[input.Area]} não faz parte do seu plano.");
            moduleId = areas.First(kv => kv.Value == input.Area).Key;
        }
        if (await _repo.CountAgentsAsync(userId) >= StudentApp.MaxAgents)
            throw new StudentAppException(409, "agent_limit", $"Você pode ter até {StudentApp.MaxAgents} agentes. Apague um para criar outro.");

        var agent = new StudentAgent
        {
            UserId = userId, ModuleId = moduleId, Name = input.Name!.Trim(), Avatar = input.Avatar ?? "spark",
            Tone = input.Tone ?? "amigavel", Instructions = (input.Instructions ?? string.Empty).Trim(), IsDefault = false,
        };
        await _repo.AddAgentAsync(agent);
        return ToAgentResponse(agent, plan, profile, areas, disciplines);
    }

    public async Task<StudentAgentResponse> UpdateAgentAsync(int userId, int agentId, StudentAgentInput input)
    {
        await RequireConsumerStudentAsync(userId);
        var agent = await OwnedAgentAsync(userId, agentId);
        var (plan, profile, areas, disciplines) = await ContextAsync(userId);
        ValidateAgentFields(input, creating: false);

        if (input.Area != null)
        {
            var isDiscipline = disciplines.ContainsKey(agent.ModuleId);
            if ((input.Area == "disciplina") != isDiscipline)
                throw new StudentAppException(400, "invalid_area", "Agentes de disciplina e do ENEM não podem trocar de tipo.");
            if (!isDiscipline && areas.GetValueOrDefault(agent.ModuleId) != input.Area)
            {
                if (!StudentApp.AreaIds.Contains(input.Area))
                    throw new StudentAppException(400, "invalid_area", "Área inválida.");
                if (!StudentApp.AllowedAreas(plan, profile).Contains(input.Area))
                    throw new StudentAppException(403, "area_locked", $"{StudentApp.AreaLabels[input.Area]} não faz parte do seu plano.");
                agent.ModuleId = areas.First(kv => kv.Value == input.Area).Key;
            }
        }
        if (input.Name != null) agent.Name = input.Name.Trim();
        if (input.Avatar != null) agent.Avatar = input.Avatar;
        if (input.Tone != null) agent.Tone = input.Tone;
        if (input.Instructions != null) agent.Instructions = input.Instructions.Trim();
        await _repo.SaveChangesAsync();
        return ToAgentResponse(agent, plan, profile, areas, disciplines);
    }

    public async Task DeleteAgentAsync(int userId, int agentId)
    {
        await RequireConsumerStudentAsync(userId);
        await _repo.DeleteAgentAsync(await OwnedAgentAsync(userId, agentId));
    }

    // ─── Disciplines (Universitário) ─────────────────────────────────────────

    private async Task<StudentApp.PlanInfo> RequireUniversityPlanAsync(User user, StudentProfile profile)
    {
        var plan = RequireReady(user, profile, StudentApp.ActivePlan(await _repo.GetSubscriptionAsync(user.UserId), UtcNow));
        if (plan.Id != "universitario")
            throw new StudentAppException(403, "plan_feature", "As disciplinas fazem parte do plano Universitário.");
        return plan;
    }

    private static string DisciplinePrompt(User user, StudentProfile profile, string discipline, string? professor)
    {
        var who = new List<string>();
        if (!string.IsNullOrEmpty(profile.Major)) who.Add($"curso de {profile.Major}");
        if (!string.IsNullOrEmpty(profile.UniversityName)) who.Add($"na {profile.UniversityName}");
        if (profile.Semester != null) who.Add($"{profile.Semester}º semestre");
        var context = who.Count > 0 ? $" ({string.Join(", ", who)})" : string.Empty;
        var prof = string.IsNullOrEmpty(professor) ? string.Empty : $" A disciplina é ministrada por {professor}.";
        return $"Você é um agente de estudos da disciplina \"{discipline}\" de um estudante universitário{context}.{prof} " +
               "Ajude-o a entender o conteúdo, resolver exercícios passo a passo e revisar para provas, com rigor de nível " +
               "universitário e linguagem clara. Use o material que ele enviou como fonte principal e diga quando a " +
               "resposta não estiver nele. Em trabalhos ou provas avaliativas, ajude-o a entender e a estruturar o " +
               "próprio raciocínio em vez de entregar o trabalho pronto.";
    }

    private async Task<int> EnsurePersonalCourseAsync(User user, StudentProfile profile)
    {
        if (profile.PersonalCourseId is int existing && await _courses.GetByIdAsync(existing) != null) return existing;
        var university = await ConsumerUniversityAsync();
        var course = await _courses.AddAsync(new Course
        {
            Name = $"{profile.Major ?? "Faculdade"} — {user.FirstName}",
            Code = $"STU-{user.UserId}",
            Description = "Disciplinas do estudante (TutorIA Estudantes, plano Universitário).",
            UniversityId = university.Id,
        });
        await _enrollments.EnrollStudentInCourseAsync(user.UserId, course.Id);
        profile.PersonalCourseId = course.Id;
        await _repo.SaveChangesAsync();
        return course.Id;
    }

    private async Task<Module> OwnedDisciplineAsync(StudentProfile profile, int disciplineId)
    {
        var module = await _modules.GetByIdAsync(disciplineId);
        if (module == null || profile.PersonalCourseId == null || module.CourseId != profile.PersonalCourseId)
            throw StudentAppException.NotFound("Disciplina");
        return module;
    }

    private async Task<StudentDisciplineResponse> ToDisciplineResponseAsync(int userId, Module m)
    {
        var counts = await _repo.GetFileStatusCountsAsync(new[] { m.Id });
        var agents = await _repo.GetAgentsAsync(userId);
        var agent = agents.Where(a => a.ModuleId == m.Id).OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id).FirstOrDefault();
        var (ready, processing) = counts.GetValueOrDefault(m.Id);
        return new StudentDisciplineResponse
        {
            Id = m.Id, Name = m.Name, Professor = m.Description, CreatedAt = m.CreatedAt,
            DocumentsReady = ready, DocumentsProcessing = processing, AgentId = agent?.Id,
        };
    }

    public async Task<List<StudentDisciplineResponse>> GetDisciplinesAsync(int userId)
    {
        await RequireConsumerStudentAsync(userId);
        var profile = await ProfileAsync(userId);
        if (profile.PersonalCourseId == null) return new List<StudentDisciplineResponse>();
        var modules = await _repo.GetModulesByCourseAsync(profile.PersonalCourseId.Value);
        var counts = await _repo.GetFileStatusCountsAsync(modules.Select(m => m.Id));
        var agents = await _repo.GetAgentsAsync(userId);
        return modules.Select(m =>
        {
            var (ready, processing) = counts.GetValueOrDefault(m.Id);
            var agent = agents.Where(a => a.ModuleId == m.Id).OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id).FirstOrDefault();
            return new StudentDisciplineResponse
            {
                Id = m.Id, Name = m.Name, Professor = m.Description, CreatedAt = m.CreatedAt,
                DocumentsReady = ready, DocumentsProcessing = processing, AgentId = agent?.Id,
            };
        }).ToList();
    }

    public async Task<StudentDisciplineResponse> CreateDisciplineAsync(int userId, string name, string? professor)
    {
        var user = await RequireConsumerStudentAsync(userId);
        var profile = await ProfileAsync(userId);
        await RequireUniversityPlanAsync(user, profile);
        var cleanName = Clean(name, 80);
        if (cleanName == null || cleanName.Length < 2)
            throw new StudentAppException(400, "invalid_name", "Informe o nome da disciplina.");
        var cleanProfessor = Clean(professor, 80);

        var courseId = await EnsurePersonalCourseAsync(user, profile);
        if ((await _repo.GetModulesByCourseAsync(courseId)).Count >= StudentApp.MaxDisciplines)
            throw new StudentAppException(409, "discipline_limit", $"Você pode ter até {StudentApp.MaxDisciplines} disciplinas.");

        var module = await _modules.AddAsync(new Module
        {
            Name = cleanName,
            Code = $"DISC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Description = cleanProfessor,
            SystemPrompt = DisciplinePrompt(user, profile, cleanName, cleanProfessor),
            CourseId = courseId,
            TutorLanguage = "pt-br",
            CourseType = CourseType.TheoryText,
            IsActive = true,
        });
        // Its study agent, ready to chat. "Monitor" = the familiar university role (monitoria).
        var agentName = $"Monitor de {cleanName}";
        await _repo.AddAgentAsync(new StudentAgent
        {
            UserId = userId, ModuleId = module.Id, Name = agentName.Length > 40 ? agentName[..40] : agentName,
            Avatar = "book", Tone = "amigavel", IsDefault = true,
        });
        return await ToDisciplineResponseAsync(userId, module);
    }

    public async Task<StudentDisciplineResponse> UpdateDisciplineAsync(int userId, int disciplineId, string? name, string? professor, bool hasProfessor)
    {
        var user = await RequireConsumerStudentAsync(userId);
        var profile = await ProfileAsync(userId);
        var module = await OwnedDisciplineAsync(profile, disciplineId);
        if (name != null)
        {
            module.Name = Clean(name, 80) is { Length: >= 2 } n ? n : throw new StudentAppException(400, "invalid_name", "Informe o nome da disciplina.");
        }
        if (hasProfessor) module.Description = Clean(professor, 80);
        module.SystemPrompt = DisciplinePrompt(user, profile, module.Name, module.Description);
        await _modules.UpdateAsync(module);
        return await ToDisciplineResponseAsync(userId, module);
    }

    public async Task DeleteDisciplineAsync(int userId, int disciplineId)
    {
        await RequireConsumerStudentAsync(userId);
        var profile = await ProfileAsync(userId);
        var module = await OwnedDisciplineAsync(profile, disciplineId);
        foreach (var file in await _files.GetByModuleIdAsync(module.Id))
        {
            if (!string.IsNullOrEmpty(file.BlobPath))
            {
                try { await _blobs.DeleteFileAsync(file.BlobPath); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not delete blob for file {FileId}", file.Id); }
            }
        }
        // Agents, files, decks and their cards cascade with the module.
        await _modules.DeleteAsync(module);
    }

    private static string KindOf(string? extension) => extension switch
    {
        "pdf" => "pdf",
        "pptx" => "pptx",
        "docx" => "docx",
        "txt" or "md" => "text",
        _ => "image",
    };

    private static StudentFileResponse ToFileResponse(FileEntity f) => new()
    {
        Id = f.Id,
        DisciplineId = f.ModuleId ?? 0,
        Filename = f.Name,
        Kind = KindOf(f.FileType),
        SizeBytes = f.FileSize ?? 0,
        Status = f.ProcessingStatus switch { "ready" => "ready", "failed" => "failed", _ => "processing" },
        CreatedAt = f.CreatedAt,
    };

    public async Task<List<StudentFileResponse>> GetDisciplineFilesAsync(int userId, int disciplineId)
    {
        await RequireConsumerStudentAsync(userId);
        var profile = await ProfileAsync(userId);
        var module = await OwnedDisciplineAsync(profile, disciplineId);
        return (await _files.GetByModuleIdAsync(module.Id))
            .Where(f => f.IsActive)
            .OrderByDescending(f => f.CreatedAt)
            .Select(ToFileResponse)
            .ToList();
    }

    public async Task<StudentFileResponse> UploadDisciplineFileAsync(int userId, int disciplineId, Stream content, string fileName, string contentType, long size, bool confirmRights)
    {
        var user = await RequireConsumerStudentAsync(userId);
        var profile = await ProfileAsync(userId);
        await RequireUniversityPlanAsync(user, profile);
        var module = await OwnedDisciplineAsync(profile, disciplineId);
        if (!confirmRights)
            throw new StudentAppException(400, "rights_required", "Confirme que você tem direito de usar este material para estudar.");
        if (size <= 0)
            throw new StudentAppException(400, "empty_file", "O arquivo está vazio.");
        if (size > StudentApp.MaxUploadMb * 1024L * 1024L)
            throw new StudentAppException(413, "file_too_large", $"O arquivo passa de {StudentApp.MaxUploadMb} MB.");
        var safeName = FileHelper.SanitizeFilename(fileName);
        var extension = Path.GetExtension(safeName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(safeName) || !UploadKinds.Contains(extension))
            throw new StudentAppException(415, "unsupported_file", "Envie PDF, slides (PPTX), Word (DOCX), foto ou texto.");
        if (await _repo.CountFilesInCourseAsync(module.CourseId) >= StudentApp.MaxDocuments)
            throw new StudentAppException(409, "document_limit", $"Você chegou ao limite de {StudentApp.MaxDocuments} arquivos. Apague alguns para enviar outros.");
        var usage = await _repo.GetUsageAsync(userId, Today);
        if ((usage?.Uploads ?? 0) >= StudentApp.DailyUploads)
            throw new StudentAppException(429, "daily_limit", "Você atingiu o limite de arquivos enviados hoje. Volte amanhã!");

        var university = await ConsumerUniversityAsync();
        var blobPath = _blobs.GenerateBlobPath(university.Id, module.CourseId, module.Id, safeName);
        var blobUrl = await _blobs.UploadFileAsync(content, blobPath, contentType);
        var file = await _files.AddAsync(new FileEntity
        {
            Name = safeName.Length > 200 ? safeName[..200] : safeName,
            FileType = extension,
            FileName = safeName,
            BlobUrl = blobUrl,
            BlobPath = blobPath,
            ContentType = contentType,
            FileSize = size,
            ModuleId = module.Id,
            SourceType = "upload",
            IsActive = true,
            ProcessingStatus = "pending",
        });
        await _repo.IncrementUploadsAsync(userId, Today);

        // Same pipeline as professors' module files: SQS → tutoria-worker extraction.
        try { await _sqs.SendExtractionJobAsync(file.Id, module.Id); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to enqueue extraction for student file {FileId}", file.Id); }
        return ToFileResponse(file);
    }

    public async Task DeleteDisciplineFileAsync(int userId, int fileId)
    {
        await RequireConsumerStudentAsync(userId);
        var profile = await ProfileAsync(userId);
        var file = await _files.GetByIdAsync(fileId);
        if (file?.ModuleId == null) throw StudentAppException.NotFound("Arquivo");
        await OwnedDisciplineAsync(profile, file.ModuleId.Value); // 404 if not the student's
        if (!string.IsNullOrEmpty(file.BlobPath))
        {
            try { await _blobs.DeleteFileAsync(file.BlobPath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete blob for file {FileId}", file.Id); }
        }
        await _files.DeleteAsync(file);
    }

    // ─── Devices ─────────────────────────────────────────────────────────────

    public async Task RegisterDeviceAsync(int userId, string token, string? platform)
    {
        await RequireConsumerStudentAsync(userId);
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
            throw new StudentAppException(400, "invalid_token", "Token de notificação inválido.");
        await _repo.UpsertDeviceTokenAsync(userId, token.Trim(), platform is "ios" or "android" ? platform : null);
    }

    public async Task RemoveDeviceAsync(int userId, string token)
    {
        await RequireConsumerStudentAsync(userId);
        await _repo.RemoveDeviceTokenAsync(userId, token.Trim());
    }
}
