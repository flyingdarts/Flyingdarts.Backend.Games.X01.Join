using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using System.Threading;
using System.Threading.Tasks;
using Flyingdarts.Persistence;
using MediatR;
using Amazon.DynamoDBv2.DataModel;
using Flyingdarts.Lambdas.Shared;
using Flyingdarts.Shared;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.DynamoDBv2.DocumentModel;

public class JoinX01GameCommandHandler : IRequestHandler<JoinX01GameCommand, APIGatewayProxyResponse>
{
    private readonly IDynamoDBContext _dbContext;
    private readonly ApplicationOptions _applicationOptions;
    public JoinX01GameCommandHandler(IDynamoDBContext dbContext, IOptions<ApplicationOptions> applicationOptions)
    {
        _dbContext = dbContext;
        _applicationOptions = applicationOptions.Value;
    }
    public async Task<APIGatewayProxyResponse> Handle(JoinX01GameCommand request, CancellationToken cancellationToken)
    {
        var gameId = long.Parse(request.GameId);

        var socketMessage = new SocketMessage<JoinX01GameCommand>
        {
            Action = "v2/games/x01/join",
            Message = request
        };
        var game = await GetGameAsync(gameId, cancellationToken);

        if (game is not null)
        {
            await JoinGame(gameId, request.PlayerId, cancellationToken);
            socketMessage.Message.Game = game;
        }

        var gamePlayers = await GetGamePlayersAsync(gameId, cancellationToken);

        if (gamePlayers is not null)
        {
            var users = await GetUsersAsync(gamePlayers.Select(x => x.PlayerId).ToArray());
            var retVal = gamePlayers.Select(x =>
            {
                return new JoinX01GameCommand
                {
                    PlayerId = x.PlayerId,
                    PlayerName = users.Single(y => y.UserId == x.PlayerId).Profile.UserName
                };
            }).ToArray();

            socketMessage.Message.Metadata = new Dictionary<string, string> {
                { "CurrentPlayers", JsonSerializer.Serialize(retVal)}
            };
        }

        return new APIGatewayProxyResponse { StatusCode = 200, Body = JsonSerializer.Serialize(socketMessage) };
    }

    private async Task JoinGame(long gameId, string playerId, CancellationToken cancellationToken)
    {
        var gamePlayer = GamePlayer.Create(gameId, playerId);
        var gamePlayerWrite = _dbContext.CreateBatchWrite<GamePlayer>(_applicationOptions.ToOperationConfig()); gamePlayerWrite.AddPutItem(gamePlayer);

        await gamePlayerWrite.ExecuteAsync(cancellationToken);
    }

    private async Task<Game> GetGameAsync(long gameId, CancellationToken cancellationToken)
    {
        var games = await _dbContext.FromQueryAsync<Game>(QueryGamesConfig(gameId.ToString()), _applicationOptions.ToOperationConfig())
            .GetRemainingAsync(cancellationToken);
        return games.Where(x => x.Status == GameStatus.Qualifying).ToList().Single();
    }
    private async Task<List<GamePlayer>> GetGamePlayersAsync(long gameId, CancellationToken cancellationToken)
    {
        var gamePlayers = await _dbContext.FromQueryAsync<GamePlayer>(QueryPlayersConfig(gameId.ToString()), _applicationOptions.ToOperationConfig())
            .GetRemainingAsync(cancellationToken);
        return gamePlayers;
    }

    private async Task<List<User>> GetUsersAsync(string[] userIds)
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
    private static QueryOperationConfig QueryUserConfig(string userId)
    {
        var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.User);
        queryFilter.AddCondition("SK", QueryOperator.BeginsWith, userId);
        return new QueryOperationConfig { Filter = queryFilter };
    }
    private static QueryOperationConfig QueryGamesConfig(string gameId)
    {
        var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.Game);
        queryFilter.AddCondition("SK", QueryOperator.BeginsWith, gameId);
        return new QueryOperationConfig { Filter = queryFilter };
    }
    private static QueryOperationConfig QueryPlayersConfig(string gameId)
    {
        var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.GamePlayer);
        queryFilter.AddCondition("SK", QueryOperator.BeginsWith, gameId);
        return new QueryOperationConfig { Filter = queryFilter };
    }
}