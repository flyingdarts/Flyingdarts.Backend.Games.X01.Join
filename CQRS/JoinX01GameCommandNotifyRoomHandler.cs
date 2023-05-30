﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Flyingdarts.Lambdas.Shared;
using Flyingdarts.Persistence;
using Flyingdarts.Shared;
using MediatR.Pipeline;
using Microsoft.Extensions.Options;

namespace Flyingdarts.Backend.Games.X01.Join.CQRS
{
    public class JoinX01GameCommandNotifyRoomHandler : IRequestPostProcessor<JoinX01GameCommand, APIGatewayProxyResponse>
    {
        private readonly IDynamoDBContext _dbContext;
        private readonly ApplicationOptions _applicationOptions;
        private readonly IAmazonApiGatewayManagementApi _apiGatewayClient;

        public JoinX01GameCommandNotifyRoomHandler(IDynamoDBContext dbContext, IOptions<ApplicationOptions> applicationOptions, IAmazonApiGatewayManagementApi apiGatewayClient)
        {
            _dbContext = dbContext;
            _applicationOptions = applicationOptions.Value;
            _apiGatewayClient = apiGatewayClient;
        }

        public async Task Process(JoinX01GameCommand request, APIGatewayProxyResponse response, CancellationToken cancellationToken)
        {
            var players = await GetGamePlayersAsync(long.Parse(request.GameId), cancellationToken);
            var users = await GetUsersWithIds(players.Select(x => x.PlayerId).ToArray());
        
            var socketMessage = new SocketMessage<JoinX01GameCommand> 
            { 
                Message = request,
                Action = "v2/games/x01/join"
            };

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(socketMessage)));

            foreach (var user in users)
            {
                if (!string.IsNullOrEmpty(user.ConnectionId))
                {
                    var connectionId = user.UserId == request.PlayerId 
                        ? request.ConnectionId : user.ConnectionId;
                        
                    var postConnectionRequest = new PostToConnectionRequest
                    {
                        ConnectionId = connectionId,
                        Data = stream
                    };

                    stream.Position = 0;

                    await _apiGatewayClient.PostToConnectionAsync(postConnectionRequest, cancellationToken);
                }
            }
        }

        private async Task<List<User>> GetUsersWithIds(string[] userIds)
        {
            List<User> users = new List<User>();
            for (var i = 0; i < userIds.Length; i++)
            {
                var resultSet = await _dbContext.FromQueryAsync<User>(QueryUserConfig(userIds[i]), _applicationOptions.ToOperationConfig()).GetRemainingAsync();
                var user = resultSet.Single();
                users.Add(user);
            }
            return users;
        }

        private async Task<List<GamePlayer>> GetGamePlayersAsync(long gameId, CancellationToken cancellationToken)
        {
            var gamePlayers = await _dbContext.FromQueryAsync<GamePlayer>(QueryConfig(gameId.ToString()), _applicationOptions.ToOperationConfig())
                .GetRemainingAsync(cancellationToken);
            return gamePlayers;
        }

        private static QueryOperationConfig QueryConfig(string gameId)
        {
            var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.GamePlayer);
            queryFilter.AddCondition("SK", QueryOperator.BeginsWith, gameId);
            return new QueryOperationConfig { Filter = queryFilter };
        }

        private static QueryOperationConfig QueryUserConfig(string userId)
        {
            var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.User);
            queryFilter.AddCondition("SK", QueryOperator.BeginsWith, userId);
            return new QueryOperationConfig { Filter = queryFilter };
        }
    }
}
