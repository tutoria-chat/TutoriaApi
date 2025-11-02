#!/bin/bash

# Add GSI indexes to existing DynamoDB ChatMessages table
# Run this script to add the required Global Secondary Indexes

REGION="us-east-2"
TABLE_NAME="ChatMessages"

echo "Adding Global Secondary Indexes to table: $TABLE_NAME in region: $REGION"
echo ""

# Add ModuleAnalyticsIndex
echo "1. Adding ModuleAnalyticsIndex..."
aws dynamodb update-table \
    --table-name $TABLE_NAME \
    --region $REGION \
    --attribute-definitions \
        AttributeName=moduleId,AttributeType=N \
        AttributeName=timestamp,AttributeType=N \
    --global-secondary-index-updates \
        "[{
            \"Create\": {
                \"IndexName\": \"ModuleAnalyticsIndex\",
                \"KeySchema\": [
                    {\"AttributeName\": \"moduleId\", \"KeyType\": \"HASH\"},
                    {\"AttributeName\": \"timestamp\", \"KeyType\": \"RANGE\"}
                ],
                \"Projection\": {\"ProjectionType\": \"ALL\"}
            }
        }]"

echo "   ✓ ModuleAnalyticsIndex creation initiated"
echo ""

# Wait for index to be created (table status returns to ACTIVE)
echo "   Waiting for index to be ready..."
aws dynamodb wait table-exists --table-name $TABLE_NAME --region $REGION
sleep 10  # Additional wait for index backfill

echo ""
echo "2. Adding StudentActivityIndex..."
aws dynamodb update-table \
    --table-name $TABLE_NAME \
    --region $REGION \
    --attribute-definitions \
        AttributeName=studentId,AttributeType=N \
        AttributeName=timestamp,AttributeType=N \
    --global-secondary-index-updates \
        "[{
            \"Create\": {
                \"IndexName\": \"StudentActivityIndex\",
                \"KeySchema\": [
                    {\"AttributeName\": \"studentId\", \"KeyType\": \"HASH\"},
                    {\"AttributeName\": \"timestamp\", \"KeyType\": \"RANGE\"}
                ],
                \"Projection\": {\"ProjectionType\": \"ALL\"}
            }
        }]"

echo "   ✓ StudentActivityIndex creation initiated"
echo ""

echo "   Waiting for index to be ready..."
aws dynamodb wait table-exists --table-name $TABLE_NAME --region $REGION
sleep 10

echo ""
echo "3. Adding ProviderUsageIndex..."
aws dynamodb update-table \
    --table-name $TABLE_NAME \
    --region $REGION \
    --attribute-definitions \
        AttributeName=provider,AttributeType=S \
        AttributeName=timestamp,AttributeType=N \
    --global-secondary-index-updates \
        "[{
            \"Create\": {
                \"IndexName\": \"ProviderUsageIndex\",
                \"KeySchema\": [
                    {\"AttributeName\": \"provider\", \"KeyType\": \"HASH\"},
                    {\"AttributeName\": \"timestamp\", \"KeyType\": \"RANGE\"}
                ],
                \"Projection\": {\"ProjectionType\": \"ALL\"}
            }
        }]"

echo "   ✓ ProviderUsageIndex creation initiated"
echo ""

echo "   Waiting for index to be ready..."
aws dynamodb wait table-exists --table-name $TABLE_NAME --region $REGION

echo ""
echo "✓ All indexes created successfully!"
echo ""

# Display table details
echo "Table details with indexes:"
aws dynamodb describe-table --table-name $TABLE_NAME --region $REGION --query 'Table.[TableName,TableStatus,GlobalSecondaryIndexes[*].[IndexName,IndexStatus]]' --output table

echo ""
echo "✓ All done! Indexes are ready to use."
