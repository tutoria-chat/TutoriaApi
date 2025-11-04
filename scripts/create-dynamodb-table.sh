#!/bin/bash

# Create DynamoDB ChatMessages table with GSI indexes
# Run this script to create the table in AWS

REGION="us-east-2"
TABLE_NAME="ChatMessages"

echo "Creating DynamoDB table: $TABLE_NAME in region: $REGION"

aws dynamodb create-table \
    --table-name $TABLE_NAME \
    --region $REGION \
    --billing-mode PAY_PER_REQUEST \
    --attribute-definitions \
        AttributeName=conversationId,AttributeType=S \
        AttributeName=timestamp,AttributeType=N \
        AttributeName=moduleId,AttributeType=N \
        AttributeName=studentId,AttributeType=N \
        AttributeName=provider,AttributeType=S \
    --key-schema \
        AttributeName=conversationId,KeyType=HASH \
        AttributeName=timestamp,KeyType=RANGE \
    --global-secondary-indexes \
        "[
            {
                \"IndexName\": \"ModuleAnalyticsIndex\",
                \"KeySchema\": [
                    {\"AttributeName\": \"moduleId\", \"KeyType\": \"HASH\"},
                    {\"AttributeName\": \"timestamp\", \"KeyType\": \"RANGE\"}
                ],
                \"Projection\": {\"ProjectionType\": \"ALL\"}
            },
            {
                \"IndexName\": \"StudentActivityIndex\",
                \"KeySchema\": [
                    {\"AttributeName\": \"studentId\", \"KeyType\": \"HASH\"},
                    {\"AttributeName\": \"timestamp\", \"KeyType\": \"RANGE\"}
                ],
                \"Projection\": {\"ProjectionType\": \"ALL\"}
            },
            {
                \"IndexName\": \"ProviderUsageIndex\",
                \"KeySchema\": [
                    {\"AttributeName\": \"provider\", \"KeyType\": \"HASH\"},
                    {\"AttributeName\": \"timestamp\", \"KeyType\": \"RANGE\"}
                ],
                \"Projection\": {\"ProjectionType\": \"ALL\"}
            }
        ]" \
    --sse-specification Enabled=true

echo ""
echo "Table creation initiated. Checking status..."
echo ""

# Wait for table to become active
aws dynamodb wait table-exists --table-name $TABLE_NAME --region $REGION

echo ""
echo "✓ Table created successfully!"
echo ""

# Enable point-in-time recovery
echo "Enabling point-in-time recovery..."
aws dynamodb update-continuous-backups \
    --table-name $TABLE_NAME \
    --region $REGION \
    --point-in-time-recovery-specification PointInTimeRecoveryEnabled=true

echo ""
echo "✓ Point-in-time recovery enabled!"
echo ""

# Display table details
echo "Table details:"
aws dynamodb describe-table --table-name $TABLE_NAME --region $REGION --query 'Table.[TableName,TableStatus,GlobalSecondaryIndexes[*].IndexName]' --output table

echo ""
echo "✓ All done! Table is ready to use."
