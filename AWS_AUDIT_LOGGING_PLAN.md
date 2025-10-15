# AWS Audit Logging - Comprehensive Plan

## 🎯 Goal
Implement comprehensive audit logging for all **management actions** in the Tutoria platform, leveraging AWS free tier and sponsor credits for cost-effective, scalable, and compliant logging.

## 📋 Scope

### What to Audit ✅
**Management Actions Only** (Admin/Professor UI actions):
- User management (create, update, delete, role changes)
- Module management (create, update, delete, file uploads)
- Course management (create, update, delete)
- University management (create, update, delete)
- Access token management (create, revoke)
- Configuration changes (system settings, AI models)
- Authentication events (login, logout, password reset)
- Authorization changes (permission grants/revokes)
- Data exports (CSV, PDF downloads)
- Bulk operations (batch imports, mass updates)

### What NOT to Audit ❌
- **Chat messages** (already in DynamoDB, not management actions)
- **Widget API calls** (student interactions, not admin)
- **File downloads by students** (too high volume)
- **Health check endpoints** (noisy, not actionable)
- **Static asset requests** (irrelevant)

---

## 🏗️ Architecture

### AWS Services Used

#### 1. **AWS CloudWatch Logs** (Primary Storage)
- **Cost**: Free tier = 5GB ingestion/month + 5GB storage
- **Retention**: 30 days (configurable)
- **Search**: Native CloudWatch Insights
- **Purpose**: Real-time log aggregation

#### 2. **Amazon S3** (Long-Term Archive)
- **Cost**: Sponsor credits + $0.023/GB/month (Standard)
- **Retention**: Unlimited (or 7 years for compliance)
- **Storage Class**: S3 Glacier for old logs ($0.004/GB/month)
- **Purpose**: Compliance, historical analysis

#### 3. **AWS Athena** (Analytics)
- **Cost**: $5 per TB scanned
- **Purpose**: Query archived logs with SQL
- **Use Case**: Compliance audits, forensics

#### 4. **Amazon SNS** (Alerts - Optional)
- **Cost**: Free tier = 1000 email notifications/month
- **Purpose**: Alert on critical audit events
- **Use Case**: Failed admin logins, role changes, bulk deletes

### Architecture Diagram
```
.NET Management API
        ↓
   Audit Middleware
        ↓
  CloudWatch Logs ──→ S3 (Export Daily)
        ↓                     ↓
   CloudWatch            Athena (Query)
   Insights (Search)          ↓
                        Compliance Reports
        ↓
   SNS Alerts (Critical Events)
```

---

## 📝 Audit Log Structure

### Standard Audit Log Format (JSON)
```json
{
  "eventId": "uuid",
  "timestamp": "2025-10-15T14:22:35.123Z",
  "eventType": "USER_CREATED",
  "category": "USER_MANAGEMENT",
  "severity": "INFO",
  "actor": {
    "userId": 123,
    "username": "professor@example.com",
    "role": "Professor",
    "ipAddress": "192.168.1.100",
    "userAgent": "Mozilla/5.0...",
    "sessionId": "session-uuid"
  },
  "target": {
    "resourceType": "User",
    "resourceId": 456,
    "resourceName": "newstudent@example.com"
  },
  "action": {
    "operation": "CREATE",
    "endpoint": "/api/users",
    "httpMethod": "POST",
    "success": true,
    "statusCode": 201
  },
  "changes": {
    "before": null,
    "after": {
      "email": "newstudent@example.com",
      "role": "Student",
      "isActive": true
    }
  },
  "metadata": {
    "universityId": 1,
    "courseId": 3,
    "moduleId": 15,
    "requestDuration": 125
  },
  "compliance": {
    "dataClassification": "PII",
    "regulatoryFramework": "GDPR",
    "retentionRequired": true
  }
}
```

---

## 🔧 Implementation Details

### Phase 1: .NET Audit Middleware (Week 1)

#### 1.1 Install AWS SDK
```bash
cd TutoriaApi/TutoriaApi.Infrastructure
dotnet add package AWSSDK.CloudWatchLogs
dotnet add package AWSSDK.S3
dotnet add package AWSSDK.Extensions.NETCore.Setup
```

#### 1.2 Create Audit Service Interface
**File**: `TutoriaApi.Core/Interfaces/IAuditService.cs`

```csharp
namespace TutoriaApi.Core.Interfaces;

public interface IAuditService
{
    Task LogAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<List<AuditEvent>> SearchAuditLogsAsync(AuditSearchCriteria criteria, CancellationToken cancellationToken = default);
}

public class AuditEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "INFO"; // INFO, WARN, ERROR, CRITICAL
    public ActorInfo Actor { get; set; } = new();
    public TargetInfo Target { get; set; } = new();
    public ActionInfo Action { get; set; } = new();
    public Dictionary<string, object>? Changes { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public ComplianceInfo Compliance { get; set; } = new();
}

public class ActorInfo
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}

public class TargetInfo
{
    public string ResourceType { get; set; } = string.Empty;
    public int? ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
}

public class ActionInfo
{
    public string Operation { get; set; } = string.Empty; // CREATE, UPDATE, DELETE, VIEW
    public string Endpoint { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ComplianceInfo
{
    public string DataClassification { get; set; } = "INTERNAL"; // PUBLIC, INTERNAL, CONFIDENTIAL, PII
    public string RegulatoryFramework { get; set; } = "GDPR";
    public bool RetentionRequired { get; set; } = true;
}

public class AuditSearchCriteria
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? UserId { get; set; }
    public string? EventType { get; set; }
    public string? Category { get; set; }
    public int? ResourceId { get; set; }
    public int Limit { get; set; } = 100;
}
```

#### 1.3 CloudWatch Audit Service Implementation
**File**: `TutoriaApi.Infrastructure/Services/CloudWatchAuditService.cs`

```csharp
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class CloudWatchAuditService : IAuditService
{
    private readonly IAmazonCloudWatchLogs _cloudWatchClient;
    private readonly ILogger<CloudWatchAuditService> _logger;
    private readonly string _logGroupName;
    private readonly string _logStreamName;
    private readonly bool _enabled;

    public CloudWatchAuditService(
        IConfiguration configuration,
        ILogger<CloudWatchAuditService> logger)
    {
        _logger = logger;
        _enabled = bool.Parse(configuration["AWS:AuditLogging:Enabled"] ?? "false");
        _logGroupName = configuration["AWS:AuditLogging:LogGroup"] ?? "/tutoria/audit";
        _logStreamName = $"audit-{Environment.MachineName}-{DateTime.UtcNow:yyyy-MM-dd}";

        if (_enabled)
        {
            var awsAccessKey = configuration["AWS:AccessKeyId"];
            var awsSecretKey = configuration["AWS:SecretAccessKey"];
            var awsRegion = configuration["AWS:Region"] ?? "sa-east-1";

            _cloudWatchClient = new AmazonCloudWatchLogsClient(
                awsAccessKey,
                awsSecretKey,
                Amazon.RegionEndpoint.GetBySystemName(awsRegion)
            );

            EnsureLogStreamExists().Wait();
        }
        else
        {
            _cloudWatchClient = null!;
        }
    }

    private async Task EnsureLogStreamExists()
    {
        try
        {
            // Check if log group exists
            var logGroupsResponse = await _cloudWatchClient.DescribeLogGroupsAsync(
                new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = _logGroupName
                });

            if (!logGroupsResponse.LogGroups.Any(lg => lg.LogGroupName == _logGroupName))
            {
                // Create log group
                await _cloudWatchClient.CreateLogGroupAsync(
                    new CreateLogGroupRequest { LogGroupName = _logGroupName });

                _logger.LogInformation("Created CloudWatch log group: {LogGroup}", _logGroupName);
            }

            // Check if log stream exists
            var logStreamsResponse = await _cloudWatchClient.DescribeLogStreamsAsync(
                new DescribeLogStreamsRequest
                {
                    LogGroupName = _logGroupName,
                    LogStreamNamePrefix = _logStreamName
                });

            if (!logStreamsResponse.LogStreams.Any(ls => ls.LogStreamName == _logStreamName))
            {
                // Create log stream
                await _cloudWatchClient.CreateLogStreamAsync(
                    new CreateLogStreamRequest
                    {
                        LogGroupName = _logGroupName,
                        LogStreamName = _logStreamName
                    });

                _logger.LogInformation("Created CloudWatch log stream: {LogStream}", _logStreamName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure CloudWatch log stream exists");
        }
    }

    public async Task LogAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            _logger.LogInformation("[Audit Disabled] Would log: {EventType} by {User}",
                auditEvent.EventType, auditEvent.Actor.Username);
            return;
        }

        try
        {
            var logMessage = JsonSerializer.Serialize(auditEvent, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var putLogEventsRequest = new PutLogEventsRequest
            {
                LogGroupName = _logGroupName,
                LogStreamName = _logStreamName,
                LogEvents = new List<InputLogEvent>
                {
                    new InputLogEvent
                    {
                        Message = logMessage,
                        Timestamp = DateTime.UtcNow
                    }
                }
            };

            await _cloudWatchClient.PutLogEventsAsync(putLogEventsRequest, cancellationToken);

            _logger.LogInformation("✅ Audit event logged: {EventType} by {User}",
                auditEvent.EventType, auditEvent.Actor.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to log audit event to CloudWatch");
            // Don't throw - audit failure shouldn't break the application
        }
    }

    public async Task<List<AuditEvent>> SearchAuditLogsAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            return new List<AuditEvent>();
        }

        try
        {
            var startTime = criteria.StartDate?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-7);
            var endTime = criteria.EndDate?.ToUniversalTime() ?? DateTime.UtcNow;

            var filterLogEventsRequest = new FilterLogEventsRequest
            {
                LogGroupName = _logGroupName,
                StartTime = new DateTimeOffset(startTime).ToUnixTimeMilliseconds(),
                EndTime = new DateTimeOffset(endTime).ToUnixTimeMilliseconds(),
                Limit = criteria.Limit
            };

            // Build filter pattern
            if (!string.IsNullOrEmpty(criteria.EventType))
            {
                filterLogEventsRequest.FilterPattern = $"{{$.eventType = \"{criteria.EventType}\"}}";
            }

            var response = await _cloudWatchClient.FilterLogEventsAsync(filterLogEventsRequest, cancellationToken);

            var auditEvents = response.Events
                .Select(e => JsonSerializer.Deserialize<AuditEvent>(e.Message))
                .Where(e => e != null)
                .Select(e => e!)
                .ToList();

            return auditEvents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search audit logs");
            return new List<AuditEvent>();
        }
    }
}
```

#### 1.4 Audit Middleware
**File**: `TutoriaApi.Infrastructure/Middleware/AuditMiddleware.cs`

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Middleware;

public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditMiddleware> _logger;

    // Endpoints to audit (whitelist approach)
    private static readonly HashSet<string> AuditableEndpoints = new()
    {
        "/api/users",
        "/api/modules",
        "/api/courses",
        "/api/universities",
        "/api/files",
        "/api/module-access-tokens",
        "/api/aimodels",
        "/api/auth/login",
        "/api/auth/register"
    };

    public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Check if this endpoint should be audited
        var shouldAudit = AuditableEndpoints.Any(endpoint => path.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase));

        if (!shouldAudit || context.Request.Method == "GET") // Don't audit GET requests (read-only)
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        try
        {
            // Capture response
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            stopwatch.Stop();

            // Build audit event
            var auditEvent = BuildAuditEvent(context, stopwatch.ElapsedMilliseconds);

            // Log audit event asynchronously (fire-and-forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await auditService.LogAuditEventAsync(auditEvent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to log audit event");
                }
            });

            // Copy response back
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private AuditEvent BuildAuditEvent(HttpContext context, long durationMs)
    {
        var user = context.User;
        var request = context.Request;

        return new AuditEvent
        {
            EventType = DetermineEventType(request),
            Category = DetermineCategory(request.Path),
            Severity = context.Response.StatusCode >= 400 ? "ERROR" : "INFO",
            Actor = new ActorInfo
            {
                UserId = GetUserId(user),
                Username = user.Identity?.Name ?? "Anonymous",
                Role = user.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown",
                IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserAgent = request.Headers["User-Agent"].ToString(),
                SessionId = context.Session?.Id ?? "No-Session"
            },
            Target = new TargetInfo
            {
                ResourceType = DetermineResourceType(request.Path),
                ResourceName = request.Path
            },
            Action = new ActionInfo
            {
                Operation = request.Method,
                Endpoint = request.Path,
                HttpMethod = request.Method,
                Success = context.Response.StatusCode < 400,
                StatusCode = context.Response.StatusCode
            },
            Metadata = new Dictionary<string, object>
            {
                ["requestDuration"] = durationMs,
                ["queryString"] = request.QueryString.ToString()
            }
        };
    }

    private string DetermineEventType(HttpRequest request)
    {
        var path = request.Path.Value?.ToLower() ?? "";
        var method = request.Method.ToUpper();

        return $"{DetermineResourceType(request.Path)}_{method}";
    }

    private string DetermineCategory(PathString path)
    {
        if (path.StartsWithSegments("/api/users")) return "USER_MANAGEMENT";
        if (path.StartsWithSegments("/api/modules")) return "MODULE_MANAGEMENT";
        if (path.StartsWithSegments("/api/courses")) return "COURSE_MANAGEMENT";
        if (path.StartsWithSegments("/api/universities")) return "UNIVERSITY_MANAGEMENT";
        if (path.StartsWithSegments("/api/files")) return "FILE_MANAGEMENT";
        if (path.StartsWithSegments("/api/auth")) return "AUTHENTICATION";
        return "GENERAL";
    }

    private string DetermineResourceType(PathString path)
    {
        if (path.StartsWithSegments("/api/users")) return "User";
        if (path.StartsWithSegments("/api/modules")) return "Module";
        if (path.StartsWithSegments("/api/courses")) return "Course";
        if (path.StartsWithSegments("/api/universities")) return "University";
        if (path.StartsWithSegments("/api/files")) return "File";
        return "Unknown";
    }

    private int GetUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }
}
```

#### 1.5 Register Services
**File**: `TutoriaApi.Infrastructure/DependencyInjection.cs`

Add to existing file:
```csharp
// Register Audit Service
services.AddSingleton<IAuditService, CloudWatchAuditService>();
```

**File**: `TutoriaApi.Web.Management/Program.cs`

Add middleware:
```csharp
// Add after authentication middleware
app.UseMiddleware<AuditMiddleware>();
```

---

### Phase 2: S3 Archival (Week 2)

#### 2.1 Daily Export Lambda Function
Create Lambda to export CloudWatch logs to S3 daily.

**File**: `AWS Lambda Function (Python)`

```python
import boto3
import os
from datetime import datetime, timedelta

cloudwatch = boto3.client('logs')
s3 = boto3.client('s3')

LOG_GROUP = os.environ['LOG_GROUP']
S3_BUCKET = os.environ['S3_BUCKET']

def lambda_handler(event, context):
    # Export yesterday's logs
    yesterday = datetime.now() - timedelta(days=1)
    start_time = int(yesterday.replace(hour=0, minute=0, second=0).timestamp() * 1000)
    end_time = int(yesterday.replace(hour=23, minute=59, second=59).timestamp() * 1000)

    # Create export task
    response = cloudwatch.create_export_task(
        logGroupName=LOG_GROUP,
        fromTime=start_time,
        to=end_time,
        destination=S3_BUCKET,
        destinationPrefix=f'audit-logs/{yesterday.strftime("%Y/%m/%d")}'
    )

    print(f"Export task created: {response['taskId']}")
    return {"statusCode": 200, "body": "Export started"}
```

Schedule: CloudWatch Events (EventBridge) - Daily at 2 AM

---

### Phase 3: Analytics & Compliance (Week 3)

#### 3.1 Athena Table Definition
```sql
CREATE EXTERNAL TABLE IF NOT EXISTS tutoria_audit_logs (
  eventId STRING,
  timestamp TIMESTAMP,
  eventType STRING,
  category STRING,
  severity STRING,
  actor STRUCT<
    userId: INT,
    username: STRING,
    role: STRING,
    ipAddress: STRING,
    userAgent: STRING,
    sessionId: STRING
  >,
  target STRUCT<
    resourceType: STRING,
    resourceId: INT,
    resourceName: STRING
  >,
  action STRUCT<
    operation: STRING,
    endpoint: STRING,
    httpMethod: STRING,
    success: BOOLEAN,
    statusCode: INT
  >
)
ROW FORMAT SERDE 'org.openx.data.jsonserde.JsonSerDe'
LOCATION 's3://tutoria-audit-logs/';
```

#### 3.2 Example Athena Queries

**Find all failed admin logins**:
```sql
SELECT timestamp, actor.username, actor.ipAddress
FROM tutoria_audit_logs
WHERE eventType = 'AUTH_LOGIN'
  AND action.success = false
  AND actor.role = 'SuperAdmin'
ORDER BY timestamp DESC
LIMIT 100;
```

**User activity summary**:
```sql
SELECT
  actor.userId,
  actor.username,
  COUNT(*) as totalActions,
  COUNT(DISTINCT DATE(timestamp)) as activeDays
FROM tutoria_audit_logs
WHERE timestamp > CURRENT_TIMESTAMP - INTERVAL '30' DAY
GROUP BY actor.userId, actor.username
ORDER BY totalActions DESC;
```

---

## 📊 Monitoring & Alerts

### CloudWatch Alarms

#### 1. High Volume of Failed Logins
```
Metric: Custom Metric (from logs)
Condition: > 5 failed logins in 5 minutes
Action: SNS → Email alert to security team
```

#### 2. Bulk Delete Operations
```
Metric: Custom Metric
Condition: DELETE operation with > 10 resources
Action: SNS → Email alert to admin team
```

#### 3. Unauthorized Access Attempts
```
Metric: HTTP 403 responses
Condition: > 10 in 5 minutes from same IP
Action: SNS → Email + Block IP (optional)
```

---

## 💰 Cost Estimation

### AWS Free Tier (First 12 Months)
- CloudWatch Logs: 5GB ingestion + 5GB storage FREE
- S3: 5GB storage FREE
- Lambda: 1M requests FREE
- SNS: 1000 emails FREE

### Beyond Free Tier (Estimated for 10,000 audit events/day)
- **CloudWatch Logs**: ~3GB/month = FREE (under 5GB)
- **S3 Storage**: 90GB/year (3GB/month × 30 months) = $2/month
- **S3 Glacier**: 90GB/year = $0.36/month
- **Lambda**: 30 executions/month = FREE
- **Athena Queries**: 10 queries/month × 1GB = $0.05/month

**Total Monthly Cost**: **$0-3** (mostly covered by free tier + sponsor credits)

---

## 🔒 Compliance Features

### GDPR Compliance
- ✅ Right to Access: Query all logs for a user
- ✅ Right to Erasure: Pseudonymize user data
- ✅ Data Retention: Automatic expiry after 7 years
- ✅ Audit Trail: Immutable log records

### SOC 2 Compliance
- ✅ Access Logging: All privileged operations logged
- ✅ Change Management: Before/after snapshots
- ✅ Segregation of Duties: Role-based logging
- ✅ Incident Response: Alert on anomalies

---

## ✅ Testing Checklist

### Functional Tests
- [ ] Audit logs created for all management actions
- [ ] CloudWatch receives logs correctly
- [ ] S3 export works daily
- [ ] Athena queries return correct results
- [ ] Alerts trigger on critical events

### Performance Tests
- [ ] Audit middleware adds < 10ms latency
- [ ] Async logging doesn't block requests
- [ ] CloudWatch handles 1000+ events/minute

### Security Tests
- [ ] PII data properly classified
- [ ] Logs are immutable (cannot be edited)
- [ ] Access controls on log groups
- [ ] Encryption at rest and in transit

---

## 🚀 Rollout Plan

### Week 1: Core Implementation
- Set up CloudWatch log groups
- Implement audit service
- Add audit middleware
- Test in development

### Week 2: Integration & Testing
- Deploy to staging
- Test all management endpoints
- Verify CloudWatch logging
- Load testing

### Week 3: S3 Archival & Analytics
- Set up S3 bucket
- Create Lambda for daily export
- Configure Athena tables
- Test queries

### Week 4: Monitoring & Alerts
- Create CloudWatch alarms
- Set up SNS topics
- Test alerting
- Deploy to production

---

## 📋 Audit Dashboard (Future Enhancement)

### Management UI Features
- **Recent Activity**: Last 100 audit events
- **User Activity Timeline**: Visual timeline per user
- **Failed Login Attempts**: Security monitoring
- **Bulk Operations**: Track mass changes
- **Export to CSV/PDF**: Compliance reports

### Endpoint
`GET /api/audit/logs` with filtering:
- Date range
- User ID
- Event type
- Resource type

---

## 🎓 Key Takeaways

✅ **Easy**: AWS CloudWatch is straightforward
✅ **Cheap**: Free tier covers most usage, sponsor credits for rest
✅ **Maintainable**: Simple middleware, logs everything automatically
✅ **Expandable**: Easy to add more event types, alerting rules
✅ **Scalable**: AWS handles millions of logs
✅ **Compliant**: Meets GDPR, SOC 2 requirements

---

## 🛠️ Configuration Summary

### appsettings.json
```json
{
  "AWS": {
    "Region": "sa-east-1",
    "AccessKeyId": "your-access-key",
    "SecretAccessKey": "your-secret-key",
    "AuditLogging": {
      "Enabled": true,
      "LogGroup": "/tutoria/audit",
      "RetentionDays": 30,
      "S3Bucket": "tutoria-audit-logs",
      "S3ArchiveEnabled": true
    }
  }
}
```

### Environment Variables
```bash
AWS_ACCESS_KEY_ID=your-access-key
AWS_SECRET_ACCESS_KEY=your-secret-key
AWS_REGION=sa-east-1
AWS_AUDIT_LOG_GROUP=/tutoria/audit
AWS_AUDIT_ENABLED=true
```

---

## 🔄 Next Steps After Implementation

1. **Week 1**: Deploy to staging, test thoroughly
2. **Week 2**: Monitor for issues, tune performance
3. **Week 3**: Deploy to production with monitoring
4. **Week 4**: Build audit dashboard UI
5. **Month 2**: Add advanced analytics, ML anomaly detection
