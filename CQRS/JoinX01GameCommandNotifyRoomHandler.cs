using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Flyingdarts.Persistence;
using Flyingdarts.Shared;
using MediatR.Pipeline;
using Microsoft.Extensions.Options;

namespace Flyingdarts.Backend.Games.X01.Join.CQRS
{
    public class JoinX01GameCommandNotifyRoomHandler : IRequestPostProcessor<JoinX01GameCommand, APIGatewayProxyResponse>
    {
        private readonly IAmazonDynamoDB _dbContext;
        public JoinX01GameCommandNotifyRoomHandler(IAmazonDynamoDB dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task Process(JoinX01GameCommand request, APIGatewayProxyResponse response, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ConnectionId))
                return;

            await CreateSignallingRecord(request.ConnectionId, request.PlayerId.ToString());
        }

        private async Task CreateSignallingRecord(string connectionId, string userId)
        {
            var ddbRequest = new PutItemRequest
            {
                TableName = "Flyingdarts-Signalling-Table",
                Item = new Dictionary<string, AttributeValue>
                {
                    { "ConnectionId", new AttributeValue{ S = connectionId }},
                    { "UserId", new AttributeValue { S = userId }}
                }
            };

            await _dbContext.PutItemAsync(ddbRequest);
        }
    }
}
