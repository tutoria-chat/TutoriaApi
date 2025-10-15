# Tutoria Analytics - Comprehensive Endpoints Plan

## Overview
This document outlines the complete analytics system for Tutoria, providing detailed cost analysis, usage tracking, engagement metrics, and insights with hierarchical filtering (University → Course → Module).

## Key Features

### 🎯 Core Principles
1. **Hierarchical Filtering**: Filter by University ID, Course ID, or Module ID
2. **Role-Based Access**: Super Admins see everything, Professors see their scope
3. **Time-Range Filtering**: All endpoints support start/end date filtering
4. **Real-Time Analytics**: Today's stats updated in real-time
5. **Cost Transparency**: Accurate cost estimation based on AI model pricing

### 💰 Cost Tracking
- OpenAI pricing from database (AIModels table)
- Anthropic pricing from database
- Cost estimation based on token count × model pricing
- Breakdown by provider, model, module, course, university

---

## New Analytics Endpoints

### 1. 💵 Cost Analysis Endpoints

#### GET `/api/analytics/costs/detailed`
**Purpose**: Comprehensive cost breakdown with filtering

**Query Parameters**:
- `startDate` (DateTime?, optional): Filter from date
- `endDate` (DateTime?, optional): Filter to date
- `universityId` (int?, optional): Filter by university
- `courseId` (int?, optional): Filter by course
- `moduleId` (int?, optional): Filter by module

**Response**:
```json
{
  "totalMessages": 12450,
  "totalTokens": 3450000,
  "estimatedCostUSD": 45.67,
  "costByProvider": {
    "openai": {
      "provider": "openai",
      "messageCount": 10200,
      "totalTokens": 2800000,
      "estimatedCostUSD": 32.50
    },
    "anthropic": {
      "provider": "anthropic",
      "messageCount": 2250,
      "totalTokens": 650000,
      "estimatedCostUSD": 13.17
    }
  },
  "costByModel": {
    "gpt-4o": {
      "model": "gpt-4o",
      "provider": "openai",
      "messageCount": 8500,
      "totalTokens": 2300000,
      "estimatedCostUSD": 28.75,
      "inputCostPer1M": 2.50,
      "outputCostPer1M": 10.00
    },
    "claude-3-5-sonnet-20241022": {
      "model": "claude-3-5-sonnet-20241022",
      "provider": "anthropic",
      "messageCount": 2250,
      "totalTokens": 650000,
      "estimatedCostUSD": 13.17,
      "inputCostPer1M": 3.00,
      "outputCostPer1M": 15.00
    }
  },
  "costByModule": {
    "1": 15.23,
    "2": 12.45,
    "3": 17.99
  },
  "costByCourse": {
    "1": 25.68,
    "2": 19.99
  },
  "costByUniversity": {
    "1": 45.67
  }
}
```

**Authorization**: ProfessorOrAbove

**Use Cases**:
- Budget tracking for universities
- Cost allocation per course/module
- Provider comparison (OpenAI vs Anthropic)
- Identify high-cost modules for optimization

---

#### GET `/api/analytics/costs/today`
**Purpose**: Today's costs with real-time updates

**Query Parameters**:
- `universityId` (int?, optional)
- `courseId` (int?, optional)
- `moduleId` (int?, optional)

**Response**:
```json
{
  "date": "2025-10-15T00:00:00Z",
  "totalMessages": 450,
  "totalTokens": 125000,
  "estimatedCostUSD": 1.85,
  "costByProvider": {
    "openai": 1.20,
    "anthropic": 0.65
  },
  "projectedDailyCost": 2.10,
  "comparedToYesterday": {
    "percentageChange": +15.5,
    "absoluteChange": +0.25
  }
}
```

**Authorization**: ProfessorOrAbove

**Use Cases**:
- Daily cost monitoring dashboard
- Budget alerts
- Real-time spending visibility

---

### 2. 📊 Usage Statistics Endpoints

#### GET `/api/analytics/usage/today`
**Purpose**: Comprehensive today's usage stats

**Query Parameters**:
- `universityId` (int?, optional)
- `courseId` (int?, optional)
- `moduleId` (int?, optional)

**Response**:
```json
{
  "date": "2025-10-15T00:00:00Z",
  "totalMessages": 1250,
  "uniqueStudents": 342,
  "uniqueConversations": 487,
  "activeModules": 15,
  "totalTokens": 345000,
  "averageResponseTime": 1850.5,
  "estimatedCostUSD": 4.32,
  "messagesByProvider": {
    "openai": 1050,
    "anthropic": 200
  },
  "messagesByModel": {
    "gpt-4o": 950,
    "gpt-4o-mini": 100,
    "claude-3-5-sonnet-20241022": 200
  },
  "peakHour": {
    "hour": 14,
    "messageCount": 187
  }
}
```

**Authorization**: ProfessorOrAbove

**Use Cases**:
- Real-time dashboard
- Monitor system health
- Track daily engagement
- Capacity planning

---

#### GET `/api/analytics/usage/trends`
**Purpose**: Usage trends over time (daily aggregation)

**Query Parameters**:
- `startDate` (DateTime?, default: 30 days ago)
- `endDate` (DateTime?, default: today)
- `universityId` (int?, optional)
- `courseId` (int?, optional)
- `moduleId` (int?, optional)

**Response**:
```json
{
  "trends": [
    {
      "date": "2025-10-01",
      "totalMessages": 850,
      "uniqueStudents": 234,
      "uniqueConversations": 312,
      "totalTokens": 245000,
      "estimatedCostUSD": 3.12,
      "averageResponseTime": 1920.5
    },
    {
      "date": "2025-10-02",
      "totalMessages": 920,
      "uniqueStudents": 267,
      "uniqueConversations": 354,
      "totalTokens": 278000,
      "estimatedCostUSD": 3.45,
      "averageResponseTime": 1875.0
    }
    // ... more days
  ],
  "summary": {
    "totalPeriodMessages": 25600,
    "totalPeriodCost": 98.45,
    "averageDailyMessages": 853,
    "averageDailyCost": 3.28,
    "growthRate": +12.5,
    "trendDirection": "increasing"
  }
}
```

**Authorization**: ProfessorOrAbove

**Use Cases**:
- Growth tracking
- Trend analysis
- Forecasting
- Reporting

---

#### GET `/api/analytics/usage/hourly`
**Purpose**: Hourly usage breakdown for peak time analysis

**Query Parameters**:
- `date` (DateTime?, default: today)
- `universityId` (int?, optional)
- `courseId` (int?, optional)
- `moduleId` (int?, optional)

**Response**:
```json
{
  "date": "2025-10-15",
  "hourlyBreakdown": [
    {
      "hour": 0,
      "messageCount": 12,
      "uniqueStudents": 8,
      "uniqueConversations": 10,
      "averageResponseTime": 1680.3
    },
    {
      "hour": 1,
      "messageCount": 5,
      "uniqueStudents": 4,
      "uniqueConversations": 5,
      "averageResponseTime": 1520.8
    },
    // ... hours 2-23
    {
      "hour": 14,
      "messageCount": 187,
      "uniqueStudents": 89,
      "uniqueConversations": 124,
      "averageResponseTime": 2150.5
    }
  ],
  "insights": {
    "peakHour": 14,
    "peakHourMessages": 187,
    "quietestHour": 4,
    "quietestHourMessages": 2,
    "businessHoursTotal": 980,
    "afterHoursTotal": 270
  }
}
```

**Authorization**: ProfessorOrAbove

**Use Cases**:
- Infrastructure capacity planning
- Peak time identification
- Student behavior patterns
- Support scheduling

---

### 3. 👥 Student & Engagement Endpoints

#### GET `/api/analytics/students/top-active`
**Purpose**: Top active students by message count

**Query Parameters**:
- `limit` (int, default: 10, max: 100)
- `startDate` (DateTime?, optional)
- `endDate` (DateTime?, optional)
- `moduleId` (int?, optional)

**Response**:
```json
{
  "topStudents": [
    {
      "studentId": 1234,
      "totalMessages": 487,
      "uniqueConversations": 45,
      "uniqueModules": 5,
      "firstMessageAt": "2025-09-01T10:30:00Z",
      "lastMessageAt": "2025-10-15T14:22:00Z",
      "averageMessagesPerConversation": 10.8
    },
    {
      "studentId": 5678,
      "totalMessages": 423,
      "uniqueConversations": 38,
      "uniqueModules": 4,
      "firstMessageAt": "2025-09-05T09:15:00Z",
      "lastMessageAt": "2025-10-15T16:45:00Z",
      "averageMessagesPerConversation": 11.1
    }
    // ... more students
  ],
  "summary": {
    "totalStudentsAnalyzed": 342,
    "averageMessagesPerStudent": 36.5,
    "medianMessagesPerStudent": 18.0
  }
}
```

**Authorization**: ProfessorOrAbove

**Use Cases**:
- Identify highly engaged students
- Recognition programs
- Behavior analysis
- Engagement benchmarking

---

#### GET `/api/analytics/engagement/conversations`
**Purpose**: Conversation engagement metrics

**Query Parameters**:
- `startDate` (DateTime?, optional)
- `endDate` (DateTime?, optional)
- `moduleId` (int?, optional)

**Response**:
```json
{
  "totalConversations": 2450,
  "averageMessagesPerConversation": 5.8,
  "medianMessagesPerConversation": 4.0,
  "singleMessageConversations": 450,
  "shortConversations": 980,
  "mediumConversations": 720,
  "longConversations": 300,
  "conversationCompletionRate": 81.6,
  "averageConversationDuration": "00:15:32",
  "conversationDistribution": {
    "1-message": 450,
    "2-5 messages": 980,
    "6-15 messages": 720,
    "16+ messages": 300
  },
  "insights": {
    "engagementQuality": "high",
    "dropoffRate": 18.4,
    "recommendedActions": [
      "Focus on reducing single-message conversations",
      "Average conversation length is healthy"
    ]
  }
}
```

**Authorization**: ProfessorOrAbove

**Use Cases**:
- Measure conversation quality
- Identify drop-off points
- Improve engagement strategies
- A/B testing results

---

### 4. ⚡ Performance & Quality Endpoints

#### GET `/api/analytics/performance/response-quality`
**Purpose**: Response quality and performance metrics

**Query Parameters**:
- `startDate` (DateTime?, optional)
- `endDate` (DateTime?, optional)
- `moduleId` (int?, optional)

**Response**:
```json
{
  "averageResponseTime": 1850.5,
  "medianResponseTime": 1620.0,
  "p95ResponseTime": 3200.0,
  "p99ResponseTime": 4500.0,
  "averageTokensPerMessage": 285.3,
  "tokenEfficiencyScore": 0.154,
  "fastResponses": 8950,
  "slowResponses": 234,
  "responseTimeDistribution": {
    "< 1s": 3450,
    "1-2s": 5500,
    "2-5s": 2800,
    "5-10s": 450,
    "10s+": 234
  },
  "performanceGrade": "A",
  "insights": {
    "status": "healthy",
    "issues": [
      "2% of responses exceed 10 seconds"
    ],
    "recommendations": [
      "Consider caching for common questions"
    ]
  }
}
```

**Authorization**: ProfessorOrAbove

**Use Cases**:
- Monitor system performance
- Identify slow responses
- Quality assurance
- Infrastructure optimization

---

### 5. 🔬 Module Comparison & Analysis

#### GET `/api/analytics/modules/compare`
**Purpose**: Compare multiple modules side-by-side

**Query Parameters**:
- `moduleIds` (comma-separated list, e.g., "1,2,3,4")
- `startDate` (DateTime?, optional)
- `endDate` (DateTime?, optional)

**Response**:
```json
{
  "modules": [
    {
      "moduleId": 1,
      "moduleName": "Introduction to Python",
      "totalMessages": 2450,
      "uniqueStudents": 145,
      "averageMessagesPerStudent": 16.9,
      "averageResponseTime": 1750.0,
      "totalTokens": 685000,
      "estimatedCostUSD": 8.65,
      "engagementScore": 85.5
    },
    {
      "moduleId": 2,
      "moduleName": "Calculus I",
      "totalMessages": 1820,
      "uniqueStudents": 98,
      "averageMessagesPerStudent": 18.6,
      "averageResponseTime": 1920.0,
      "totalTokens": 545000,
      "estimatedCostUSD": 6.95,
      "engagementScore": 78.2
    }
    // ... more modules
  ],
  "insights": {
    "mostActiveModule": {
      "moduleId": 1,
      "reason": "Highest message count"
    },
    "mostEngagedModule": {
      "moduleId": 2,
      "reason": "Highest messages per student"
    },
    "mostEfficientModule": {
      "moduleId": 3,
      "reason": "Lowest cost per message"
    },
    "recommendations": [
      "Module 2 shows high engagement - analyze best practices",
      "Module 4 has higher costs - review prompt optimization"
    ]
  }
}
```

**Authorization**: ProfessorOrAbove

**Use Cases**:
- Benchmark modules against each other
- Identify successful patterns
- Cost optimization opportunities
- Engagement strategies

---

#### GET `/api/analytics/modules/{moduleId}/insights`
**Purpose**: AI-powered insights for a specific module

**Response**:
```json
{
  "moduleId": 1,
  "moduleName": "Introduction to Python",
  "period": {
    "startDate": "2025-09-01",
    "endDate": "2025-10-15"
  },
  "keyMetrics": {
    "totalMessages": 2450,
    "uniqueStudents": 145,
    "averageSatisfaction": 4.2,
    "engagementScore": 85.5
  },
  "insights": [
    {
      "type": "positive",
      "title": "High Engagement",
      "description": "Students averaging 16.9 messages each - 35% above platform average",
      "actionable": false
    },
    {
      "type": "concern",
      "title": "Peak Load Issues",
      "description": "Response times spike 45% during 2-4 PM",
      "actionable": true,
      "recommendation": "Consider load balancing or prompt optimization"
    },
    {
      "type": "opportunity",
      "title": "Common Question Pattern",
      "description": "15% of questions are about list comprehensions",
      "actionable": true,
      "recommendation": "Add FAQ or expand course materials on this topic"
    }
  ],
  "trends": {
    "engagement": "increasing",
    "cost": "stable",
    "responseQuality": "excellent"
  },
  "recommendations": [
    "Continue current engagement strategies",
    "Add pre-canned responses for top 5 common questions",
    "Consider upgrading to gpt-4o-mini for 50% cost savings on simple queries"
  ]
}
```

**Authorization**: ProfessorOrAbove

**Use Cases**:
- Module health monitoring
- Actionable insights
- Continuous improvement
- Professor decision support

---

### 6. 📈 Executive Dashboard Endpoints

#### GET `/api/analytics/dashboard/summary`
**Purpose**: High-level summary for executives and super admins

**Query Parameters**:
- `universityId` (int?, optional - Super Admin only)
- `period` (enum: "today", "week", "month", "quarter", "year")

**Response**:
```json
{
  "period": "month",
  "dateRange": {
    "start": "2025-09-15",
    "end": "2025-10-15"
  },
  "overview": {
    "totalMessages": 45600,
    "totalCostUSD": 567.89,
    "uniqueStudents": 1240,
    "activeModules": 48,
    "activeCourses": 12,
    "activeUniversities": 3
  },
  "growth": {
    "messagesGrowth": +15.5,
    "studentGrowth": +8.2,
    "costGrowth": +12.1
  },
  "topPerformers": {
    "mostActiveModule": {
      "id": 1,
      "name": "Introduction to Python",
      "messages": 2450
    },
    "mostEngagedCourse": {
      "id": 1,
      "name": "Computer Science Fundamentals",
      "avgMessagesPerStudent": 18.6
    },
    "mostActiveUniversity": {
      "id": 1,
      "name": "University of Example",
      "messages": 35000
    }
  },
  "costBreakdown": {
    "byProvider": {
      "openai": 425.67,
      "anthropic": 142.22
    },
    "topCostModule": {
      "id": 5,
      "name": "Advanced AI Topics",
      "cost": 85.50
    }
  },
  "healthIndicators": {
    "systemHealth": "excellent",
    "averageResponseTime": 1850.5,
    "errorRate": 0.02,
    "uptime": 99.98
  }
}
```

**Authorization**: ProfessorOrAbove (filtered by role)

**Use Cases**:
- Executive reporting
- Board presentations
- High-level monitoring
- Strategic decisions

---

## Authorization & Filtering Rules

### Role-Based Access

#### Super Admin
- Access to **ALL** data across all universities
- No filtering restrictions
- Can filter by any combination: university, course, module
- Use case: Platform-wide analytics

#### University Admin
- Access to data from **their university only**
- Can filter by courses and modules within their university
- Cannot see other universities' data
- Use case: University-level reporting

#### Professor
- Access to data from **their courses only**
- Can filter by modules within their courses
- Cannot see other professors' data
- Use case: Course and module management

### Automatic Filtering
All endpoints automatically apply role-based filtering:

```csharp
// Pseudo-code for filtering logic
if (user.Role == "SuperAdmin")
{
    // No restrictions - apply query filters as-is
    moduleIds = filters.ModuleId ?? GetAllModules();
}
else if (user.Role == "UniversityAdmin")
{
    // Restrict to user's university
    var universityId = user.UniversityId;
    moduleIds = GetModulesForUniversity(universityId);

    if (filters.ModuleId.HasValue)
    {
        // Verify module belongs to their university
        if (!moduleIds.Contains(filters.ModuleId.Value))
            throw new UnauthorizedException();
    }
}
else if (user.Role == "Professor")
{
    // Restrict to user's courses
    var courseIds = GetProfessorCourses(user.Id);
    moduleIds = GetModulesForCourses(courseIds);

    if (filters.ModuleId.HasValue)
    {
        // Verify module belongs to their courses
        if (!moduleIds.Contains(filters.ModuleId.Value))
            throw new UnauthorizedException();
    }
}

// Now query DynamoDB with filtered module IDs
var messages = await QueryDynamoDB(moduleIds, startDate, endDate);
```

---

## Implementation Notes

### Database Integration
Some endpoints require joining DynamoDB data with SQL Server:

1. **Get module IDs from SQL** based on filters (university, course)
2. **Query DynamoDB** with filtered module IDs
3. **Aggregate and calculate** metrics
4. **Enrich with SQL data** (module names, university names)

### Cost Calculation
```csharp
// Cost estimation logic
public double EstimateCost(string model, long inputTokens, long outputTokens)
{
    var aiModel = _context.AIModels.FirstOrDefault(m => m.ModelName == model);

    if (aiModel == null)
        return 0.0; // Unknown model

    var inputCost = (inputTokens / 1_000_000.0) * (double)aiModel.InputCostPer1M;
    var outputCost = (outputTokens / 1_000_000.0) * (double)aiModel.OutputCostPer1M;

    return inputCost + outputCost;
}
```

### Caching Strategy
High-traffic endpoints should implement caching:
- Today's stats: Cache for 5 minutes
- Historical trends: Cache for 1 hour
- Module comparisons: Cache for 30 minutes

```csharp
[ResponseCache(Duration = 300)] // 5 minutes
public async Task<ActionResult<DailyUsageStatsDto>> GetTodayUsageStatsAsync(...)
```

### Performance Optimization
1. **Pagination**: Large result sets should be paginated
2. **Async/Await**: All DynamoDB queries are async
3. **Parallel Queries**: Use `Task.WhenAll` for multiple module queries
4. **Batch Operations**: Batch DynamoDB queries when possible

---

## API Versioning & Migration
While we're not using `/v1/` or `/v2/` routes:
- All endpoints are in `/api/analytics/`
- Breaking changes will be introduced carefully
- Deprecation notices added to response headers
- Documentation maintained on changes

---

## Testing Requirements

### Unit Tests
- Test all calculation logic (costs, percentiles, etc.)
- Test role-based filtering
- Test edge cases (empty data, invalid dates)

### Integration Tests
- Test DynamoDB + SQL Server integration
- Test authorization for different roles
- Test query performance with large datasets

### Load Tests
- Test with 100,000+ messages
- Test concurrent requests (50+ simultaneous)
- Test caching effectiveness

---

## Monitoring & Alerts

### Key Metrics to Monitor
- Endpoint response times
- DynamoDB query performance
- Error rates
- Cache hit rates
- Cost anomalies

### Alert Conditions
- Daily cost exceeds threshold (e.g., $100/day)
- Response time > 5 seconds
- Error rate > 1%
- Unusual spike in messages (>200% increase)

---

## Future Enhancements

### Phase 2 Features
1. **Predictive Analytics**: ML-based usage forecasting
2. **Anomaly Detection**: Automatic detection of unusual patterns
3. **Custom Dashboards**: User-configurable widgets
4. **Export to PDF/Excel**: Report generation
5. **Real-Time WebSockets**: Live dashboard updates
6. **Natural Language Queries**: Ask questions about analytics
7. **Sentiment Analysis**: Analyze conversation sentiment
8. **Topic Modeling**: Identify common question themes

### Phase 3 Features
1. **A/B Testing Framework**: Test different prompts/models
2. **Student Journey Mapping**: Visualize learning paths
3. **Recommendation Engine**: Suggest content based on questions
4. **Integration with LMS**: Sync with Canvas, Moodle, etc.
5. **Mobile App**: Native iOS/Android analytics app

---

## Complete Endpoint List

| Endpoint | Method | Purpose | Auth |
|----------|--------|---------|------|
| `/api/analytics/costs/detailed` | GET | Comprehensive cost breakdown | ProfessorOrAbove |
| `/api/analytics/costs/today` | GET | Today's costs real-time | ProfessorOrAbove |
| `/api/analytics/usage/today` | GET | Today's usage stats | ProfessorOrAbove |
| `/api/analytics/usage/trends` | GET | Daily usage trends | ProfessorOrAbove |
| `/api/analytics/usage/hourly` | GET | Hourly breakdown | ProfessorOrAbove |
| `/api/analytics/students/top-active` | GET | Most active students | ProfessorOrAbove |
| `/api/analytics/engagement/conversations` | GET | Conversation metrics | ProfessorOrAbove |
| `/api/analytics/performance/response-quality` | GET | Performance metrics | ProfessorOrAbove |
| `/api/analytics/modules/compare` | GET | Compare modules | ProfessorOrAbove |
| `/api/analytics/modules/{id}/insights` | GET | AI-powered insights | ProfessorOrAbove |
| `/api/analytics/dashboard/summary` | GET | Executive summary | ProfessorOrAbove |
| `/api/analytics/conversations/{id}` | GET | Conversation history | ProfessorOrAbove |
| `/api/analytics/modules/{id}/summary` | GET | Module summary | ProfessorOrAbove |
| `/api/analytics/modules/{id}/messages` | GET | Module messages | ProfessorOrAbove |
| `/api/analytics/modules/{id}/faq` | GET | Generate FAQ | ProfessorOrAbove |
| `/api/analytics/students/{id}/activity` | GET | Student activity | ProfessorOrAbove |
| `/api/analytics/providers/{provider}/usage` | GET | Provider usage | ProfessorOrAbove |

**Total: 17 endpoints** (7 already implemented, 10 new planned)

---

## Success Metrics

### Technical KPIs
- All endpoints respond in < 2 seconds (p95)
- Cache hit rate > 80%
- Error rate < 0.1%
- Query efficiency: < 1000 DynamoDB reads per request

### Business KPIs
- Professors use analytics weekly: >70%
- Cost tracking accuracy: >95%
- Actionable insights generated: >5 per module/month
- User satisfaction with analytics: >4.5/5

---

## Documentation Requirements

### Developer Docs
- OpenAPI/Swagger documentation
- Code examples for each endpoint
- Authentication guide
- Rate limiting info

### User Docs
- Professor guide to analytics
- Admin dashboard guide
- Cost tracking guide
- FAQ and troubleshooting

---

## Conclusion

This analytics system provides:
✅ Comprehensive cost tracking
✅ Rich usage insights
✅ Student engagement metrics
✅ Performance monitoring
✅ Comparison tools
✅ Actionable recommendations
✅ Role-based access with filtering
✅ Scalable architecture

Next steps:
1. Review and approve this plan
2. Implement DTOs and interfaces (DONE)
3. Implement service methods with SQL + DynamoDB integration
4. Add controller endpoints
5. Write tests
6. Document in Swagger
7. Deploy and monitor
