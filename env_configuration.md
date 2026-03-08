# TutoriaApi (.NET 8) - Environment Configuration Checklist

All variables configured in `appsettings.json` / `appsettings.Development.json`. Environment variables override config file values.

| Config Key Path | Required | Example Value | Description |
|---|---|---|---|
| **DATABASE** ||||
| `ConnectionStrings:DefaultConnection` | REQUIRED | `Server=tcp:myserver.database.windows.net,1433;Initial Catalog=TutoriaDb;User ID=admin;Password=***;Encrypt=True;` | SQL Server connection string (also used by Hangfire) |
| **JWT** ||||
| `Jwt:SecretKey` | REQUIRED | `YourSuperSecretKeyThatIsAtLeast32CharactersLong!` | JWT signing key (min 32 chars for HS256) |
| `Jwt:Issuer` | OPTIONAL | `TutoriaAuthApi` | JWT issuer claim (default: TutoriaApi) |
| `Jwt:Audience` | OPTIONAL | `TutoriaApi` | JWT audience claim (default: TutoriaApi) |
| **AZURE BLOB STORAGE** ||||
| `AzureStorage:ConnectionString` | REQUIRED | `DefaultEndpointsProtocol=https;AccountName=mystg;AccountKey=***;EndpointSuffix=core.windows.net` | Azure Blob Storage connection string |
| `AzureStorage:ContainerName` | OPTIONAL | `tutoria-files` | Blob container name (default: tutoria-files) |
| **AI API** ||||
| `AiApi:BaseUrl` | OPTIONAL | `http://localhost:8000` | Python API URL for transcription/quiz endpoints (default: http://localhost:8000) |
| `OpenAI:ApiKey` | OPTIONAL | `sk-proj-...` | OpenAI API key (if used directly from .NET) |
| **EMAIL (Resend)** ||||
| `Resend:ApiKey` | OPTIONAL | `re_ABC123xyz...` | Resend email API key (email disabled if missing) |
| `RESEND_API_KEY` | OPTIONAL | `re_ABC123xyz...` | Env var override for Resend key (takes priority over config) |
| `Email:FromAddress` | OPTIONAL | `noreply@tutoria.com` | Sender email address (default: noreply@tutoria.com) |
| `Email:FromName` | OPTIONAL | `Tutoria Platform` | Sender display name (default: Tutoria Platform) |
| `Email:FrontendUrl` | OPTIONAL | `https://app.tutoria.tec.br` | Frontend URL for email links (default: http://localhost:3000) |
| `Email:Enabled` | OPTIONAL | `true` | Enable/disable email globally |
| **AWS** ||||
| `AWS:Region` | OPTIONAL | `sa-east-1` | AWS region (default: sa-east-1) |
| `AWS:AccessKeyId` | OPTIONAL | `AKIA1234567890ABCD` | AWS access key (required if DynamoDB enabled) |
| `AWS:SecretAccessKey` | OPTIONAL | `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY` | AWS secret key |
| `AWS:DynamoDb:Enabled` | OPTIONAL | `true` | Enable DynamoDB analytics (default: false) |
| `AWS:DynamoDb:ChatTable` | OPTIONAL | `ChatMessages` | DynamoDB table name (default: ChatMessages) |
| `AWS:DynamoDb:DefaultQueryLimit` | OPTIONAL | `10000` | Max items per DynamoDB query (default: 10000) |
| **STRIPE (Payments)** ||||
| `Stripe:SecretKey` | REQUIRED | `sk_test_51H...` or `sk_live_51H...` | Stripe secret API key (server-side) |
| `Stripe:PublishableKey` | OPTIONAL | `pk_test_51H...` or `pk_live_51H...` | Stripe publishable key (client-side, for future use) |
| `Stripe:WebhookSecret` | REQUIRED | `whsec_1234abcd...` | Stripe webhook endpoint signing secret |
| `Stripe:SuccessUrl` | OPTIONAL | `https://app.tutoria.tec.br/dashboard?checkout=success` | Redirect URL after successful checkout (default: http://localhost:3000/dashboard?checkout=success) |
| `Stripe:CancelUrl` | OPTIONAL | `https://app.tutoria.tec.br/dashboard?checkout=canceled` | Redirect URL after canceled checkout (default: http://localhost:3000/dashboard?checkout=canceled) |
| **TRANSCRIPTION** ||||
| `TranscriptionRetry:DelayBetweenRetriesMs` | OPTIONAL | `2000` | Retry delay for transcription in ms (default: 2000) |
| `TranscriptionRetry:MaxRetryAgeHours` | OPTIONAL | `72` | Max age for retrying failed transcriptions in hours (default: 72) |
| **PLATFORM** ||||
| `Platform:OwnerUserIds` | OPTIONAL | `[1, 2, 3]` | User IDs with platform owner privileges (default: empty) |
| **RATE LIMITING** ||||
| `IpRateLimiting:EnableEndpointRateLimiting` | OPTIONAL | `true` | Enable per-endpoint rate limiting |
| `IpRateLimiting:RealIpHeader` | OPTIONAL | `X-Real-IP` | Header for client IP behind proxy |
| `IpRateLimiting:ClientIdHeader` | OPTIONAL | `X-ClientId` | Header for client identifier |
| `IpRateLimiting:StackBlockedRequests` | OPTIONAL | `false` | Queue blocked rate-limit requests |
| `IpRateLimiting:HttpStatusCode` | OPTIONAL | `429` | HTTP status for rate-limited responses |
| `IpRateLimiting:GeneralRules` | OPTIONAL | see appsettings.json | Rate limit rules array |
| **LOGGING** ||||
| `Logging:LogLevel:Default` | OPTIONAL | `Information` | Default log level |
| `Logging:LogLevel:Microsoft.AspNetCore` | OPTIONAL | `Warning` | ASP.NET Core log level |
| `Logging:LogLevel:Microsoft.EntityFrameworkCore` | OPTIONAL | `Warning` | EF Core log level |
| **OTHER** ||||
| `AllowedHosts` | OPTIONAL | `*` | CORS allowed hosts (default: *) |

## Unused Configs (defined in appsettings but not referenced in code)

| Config Key Path | Notes |
|---|---|
| `AuthApi:BaseUrl` | Not referenced anywhere in code |
| `Swagger:ClientId` | Not referenced anywhere in code |
| `Swagger:ClientSecret` | Not referenced anywhere in code |
