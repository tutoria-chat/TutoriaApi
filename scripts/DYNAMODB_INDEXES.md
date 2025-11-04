# DynamoDB Indexes Documentation

## Table Overview

**Table Name**: `ChatMessages`
**Primary Key**:
- Partition Key: `conversationId` (String)
- Sort Key: `timestamp` (Number)

**Billing Mode**: PAY_PER_REQUEST (On-Demand)

## Global Secondary Indexes (GSIs)

### 1. ModuleAnalyticsIndex

**Purpose**: Query all chat messages for a specific module, sorted by time.

**Keys**:
- Partition Key: `moduleId` (Number)
- Sort Key: `timestamp` (Number)

**Projection**: ALL (includes all item attributes)

**Use Cases**:
- Module-level analytics (total messages, unique students, etc.)
- Time-range queries per module (daily/weekly/monthly reports)
- Cost analysis per module
- Student engagement metrics per module

**Query Patterns**:
```python
# Get all messages for a module
moduleId = 123 AND timestamp BETWEEN start_time AND end_time

# Get last 24 hours of activity for a module
moduleId = 123 AND timestamp > yesterday_timestamp
```

**Performance Characteristics**:
- **Query latency**: <10ms for typical queries (up to 1000 items)
- **Throughput**: Auto-scales with on-demand billing
- **Optimal query size**: 1000-5000 items (use pagination for larger datasets)

**Cost Estimates** (Monthly, assumes 1 million chat messages):
- **Storage**: ~$0.25/GB (GSI replicates ALL attributes)
  - Example: 1M messages × 2KB avg = 2GB × $0.25 = **$0.50/month**
- **Read operations**: $1.25 per million read request units
  - Example: 100K queries/month × 10 items avg = **$1.25/month**
- **Write operations**: $1.25 per million write request units
  - Example: 1M writes/month = **$1.25/month**
- **Total estimated cost**: **~$3.00/month** for 1M messages

**Scaling Considerations**:
- For modules with >100K messages, consider:
  - Using pagination with `LastEvaluatedKey`
  - Filtering by date ranges to reduce result set
  - Archiving old messages to DynamoDB cold storage (Glacier)

---

### 2. StudentActivityIndex

**Purpose**: Query all chat activity for a specific student across all modules.

**Keys**:
- Partition Key: `studentId` (Number)
- Sort Key: `timestamp` (Number)

**Projection**: ALL

**Use Cases**:
- Student-level engagement tracking
- Individual student performance reports
- Student learning path analysis
- Detect inactive students

**Query Patterns**:
```python
# Get all messages from a student
studentId = 456 AND timestamp BETWEEN start_time AND end_time

# Get student's most recent activity
studentId = 456 (sorted DESC by timestamp, limit 10)
```

**Performance Characteristics**:
- **Query latency**: <10ms (students typically have <1000 messages)
- **Throughput**: Auto-scales
- **Optimal query size**: 100-1000 items

**Cost Estimates** (Monthly, assumes 10K active students):
- **Storage**: Similar to ModuleAnalyticsIndex (~$0.50/month for 1M messages)
- **Read operations**: Typically lower volume than module queries
  - Example: 50K queries/month × 5 items avg = **$0.63/month**
- **Write operations**: Same as table writes (**$1.25/month** for 1M writes)
- **Total estimated cost**: **~$2.38/month**

**Scaling Considerations**:
- Student-level queries are naturally bounded (avg 50-200 messages per student)
- For power users (>10K messages), implement pagination

---

### 3. ProviderUsageIndex

**Purpose**: Track AI provider usage (OpenAI vs Anthropic) over time for cost analysis.

**Keys**:
- Partition Key: `provider` (String) - e.g., "openai", "anthropic"
- Sort Key: `timestamp` (Number)

**Projection**: ALL

**Use Cases**:
- Cost analysis by AI provider
- Provider performance comparison
- Budget tracking and alerts
- Model usage distribution

**Query Patterns**:
```python
# Get all OpenAI usage for a time period
provider = "openai" AND timestamp BETWEEN start_time AND end_time

# Get today's Anthropic costs
provider = "anthropic" AND timestamp > today_start
```

**Performance Characteristics**:
- **Query latency**: <20ms (larger result sets due to only 2-3 providers)
- **Throughput**: Auto-scales
- **Optimal query size**: 10K-50K items (requires pagination for large time ranges)

**Cost Estimates** (Monthly, assumes 1M messages):
- **Storage**: Same GSI storage cost (~$0.50/month)
- **Read operations**: Lower volume (only for admin/analytics dashboards)
  - Example: 10K queries/month × 100 items avg = **$0.13/month**
- **Write operations**: Same as table writes (**$1.25/month**)
- **Total estimated cost**: **~$1.88/month**

**Scaling Considerations**:
- Provider-level queries can return very large datasets (50% of all messages)
- **CRITICAL**: Always use date range filtering to limit results
- Consider aggregating data daily/weekly to reduce query volume

---

## Overall Cost Summary

**Total DynamoDB Costs** (Monthly estimates for 1 million chat messages):

| Component | Cost |
|-----------|------|
| Base table storage (1M messages × 2KB) | $0.50 |
| ModuleAnalyticsIndex GSI | $3.00 |
| StudentActivityIndex GSI | $2.38 |
| ProviderUsageIndex GSI | $1.88 |
| **Total Monthly Cost** | **~$7.76** |

**Scaling Estimates**:
- 10M messages/month: ~$77/month
- 100M messages/month: ~$770/month
- 1B messages/month: ~$7,700/month

**Cost Optimization Strategies**:
1. **Enable TTL**: Auto-delete old messages (e.g., older than 12 months)
2. **Sparse indexes**: Use conditional writes to exclude certain items from GSIs
3. **Use date range filters**: Always limit queries to specific time windows
4. **Implement pagination**: Use `LastEvaluatedKey` to avoid large result sets
5. **Archive old data**: Move historical data to S3 + Athena for infrequent queries

---

## Index Sizing Guidelines

### How to Calculate Your Index Size

```
Index Size = (Average Item Size) × (Number of Items) × (Number of GSIs + 1)

Example:
  Average chat message: 2KB
  Monthly messages: 1,000,000
  GSIs: 3

  Total DynamoDB storage = 2KB × 1M × (3 + 1) = 8GB
  Monthly storage cost = 8GB × $0.25 = $2.00
```

### Projection Type Impact

**ALL (Current configuration)**:
- Replicates all item attributes to the index
- Enables flexible queries without additional reads
- **Cost**: Highest storage cost (100% of base table size per index)
- **Performance**: Best (no additional reads needed)

**KEYS_ONLY** (Alternative):
- Only stores primary key + index keys
- Requires additional GetItem calls to fetch full items
- **Cost**: Lowest storage (~10% of base table size per index)
- **Performance**: Slower (additional reads required)

**INCLUDE** (Alternative):
- Store only specific attributes in the index
- Balance between storage cost and query performance
- **Cost**: Medium (depends on included attributes)
- **Performance**: Medium (may need additional reads)

**Recommendation**: Keep `ALL` projection for analytics workloads where query performance is critical and storage costs are manageable (<$100/month).

---

## Performance Best Practices

### 1. Avoid Hot Partitions
- **Bad**: Query a single module with 1M messages without date filtering
- **Good**: Query a module for the last 7 days (results in ~10K items)

### 2. Use Pagination
```python
# Python SDK example
response = dynamodb.query(
    TableName='ChatMessages',
    IndexName='ModuleAnalyticsIndex',
    KeyConditionExpression='moduleId = :mid',
    ExpressionAttributeValues={':mid': 123},
    Limit=1000  # Fetch in batches
)

while 'LastEvaluatedKey' in response:
    response = dynamodb.query(
        # ... same parameters ...
        ExclusiveStartKey=response['LastEvaluatedKey']
    )
```

### 3. Implement Caching
- Cache frequent queries (e.g., "today's usage") using Redis/ElastiCache
- TTL: 5-15 minutes for analytics data
- Can reduce DynamoDB read costs by 80-90%

### 4. Batch Reads
- Use `BatchGetItem` instead of individual `GetItem` calls
- Up to 100 items per batch (reduces API calls by 100x)

### 5. Monitor Throttling
- Set CloudWatch alarms for `UserErrors` and `SystemErrors`
- On-demand billing auto-scales, but extreme spikes may cause brief throttling

---

## Monitoring & Alerts

### Recommended CloudWatch Metrics

1. **ConsumedReadCapacityUnits** (per index)
   - Alert if >10K RCUs/minute (indicates heavy usage)

2. **ConsumedWriteCapacityUnits**
   - Alert if >5K WCUs/minute (indicates burst activity)

3. **UserErrors**
   - Alert if >10 errors/minute (indicates throttling or invalid queries)

4. **Query Latency** (P99)
   - Alert if >100ms (indicates performance degradation)

5. **Storage Size**
   - Alert if growing >50% month-over-month (indicates potential cost issue)

### Cost Anomaly Detection
- Enable AWS Cost Anomaly Detection for DynamoDB
- Set budget alerts at $50, $100, $500 thresholds

---

## Migration & Deployment

### Initial Setup
```bash
# Create table with all indexes
./scripts/create-dynamodb-table.sh
```

### Adding Indexes to Existing Table
```bash
# Add indexes one at a time (safer for production)
./scripts/add-dynamodb-indexes.sh
```

**IMPORTANT**:
- GSI creation on existing table triggers backfill (can take hours for large tables)
- During backfill, table remains available but performance may degrade
- Plan index additions during low-traffic periods

### Disaster Recovery
- **Point-in-Time Recovery (PITR)**: Enabled by default (35-day retention)
- **On-Demand Backups**: Create before major changes
- **Cross-Region Replication**: Not currently configured (consider for HA)

---

## Troubleshooting

### Issue: Query returns too many items
**Solution**: Add date range filtering
```python
moduleId = 123 AND timestamp > 1704067200000  # Jan 1, 2024
```

### Issue: High costs
**Symptoms**: DynamoDB bill >$100/month
**Diagnosis**:
1. Check CloudWatch for read/write patterns
2. Identify top consumers (modules, students, time periods)
**Solutions**:
- Enable TTL to auto-delete old messages
- Implement query result caching
- Use date range filters in all queries
- Consider archiving to S3

### Issue: Slow queries
**Symptoms**: Query latency >100ms
**Diagnosis**:
1. Check if query is missing key conditions
2. Verify index is being used (check execution plan)
**Solutions**:
- Ensure partition key is always specified
- Add sort key range condition
- Reduce result set size with limits
- Use parallel scan for batch operations

### Issue: Throttling errors
**Symptoms**: `ProvisionedThroughputExceededException`
**Note**: Rare with on-demand billing, but can occur during extreme spikes
**Solutions**:
- Implement exponential backoff retries
- Spread writes over time (avoid burst writes)
- Consider reserved capacity for predictable workloads

---

## Related Documentation

- [AWS DynamoDB Best Practices](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/best-practices.html)
- [DynamoDB Pricing Calculator](https://calculator.aws/#/createCalculator/DynamoDB)
- [DynamoDB Global Secondary Indexes](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/GSI.html)

---

**Last Updated**: January 2025
**Maintainer**: TutorIA Platform Team
