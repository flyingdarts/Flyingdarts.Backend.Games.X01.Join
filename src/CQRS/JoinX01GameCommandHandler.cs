using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using System.Threading;
using System.Threading.Tasks;
using Flyingdarts.Persistence;
using MediatR;
using Flyingdarts.Lambdas.Shared;
using System.Collections.Generic;
using System.Linq;
using Amazon.ApiGatewayManagementApi.Model;
using System.IO;
using System.Text;
using Amazon.ApiGatewayManagementApi;

public record JoinX01GameCommandHandler(IDynamoDbService DynamoDbService, IAmazonApiGatewayManagementApi ApiGatewayClient) : IRequestHandler<JoinX01GameCommand, APIGatewayProxyResponse>
{
    public async Task<APIGatewayProxyResponse> Handle(JoinX01GameCommand request, CancellationToken cancellationToken)
    {
        var socketMessage = new SocketMessage<JoinX01GameCommand>
        {
            Action = "v2/games/x01/join",
            Message = request
        };

        await UpdateConnectionId(socketMessage, cancellationToken);

        request.Game = await DynamoDbService.ReadGameAsync(long.Parse(request.GameId), cancellationToken);

        if (request.Game is not null)
        {
            var player = GamePlayer.Create(long.Parse(request.GameId), request.PlayerId);

            await DynamoDbService.WriteGamePlayerAsync(player, cancellationToken);
        }

        request.Players = await DynamoDbService.ReadGamePlayersAsync(long.Parse(request.GameId), cancellationToken);
        request.Users = await DynamoDbService.ReadUsersAsync(request.Players.Select(x => x.PlayerId).ToArray(), cancellationToken);
        request.Darts = await DynamoDbService.ReadGameDartsAsync(long.Parse(request.GameId), cancellationToken);

        await UpdateGameStatus(socketMessage, cancellationToken);

        socketMessage.Metadata = CreateMetaData(request.Game, request.Darts, request.Players, request.Users);

        await NotifyRoomAsync(socketMessage, cancellationToken);

        return new APIGatewayProxyResponse { StatusCode = 200, Body = JsonSerializer.Serialize(socketMessage) };
    }
    public async Task UpdateConnectionId(SocketMessage<JoinX01GameCommand> message, CancellationToken cancellationToken)
    {
        var user = await DynamoDbService.ReadUserAsync(message.Message.PlayerId, cancellationToken);

        user.ConnectionId = message.Message.ConnectionId;

        await DynamoDbService.WriteUserAsync(user, cancellationToken);
    }
    public async Task NotifyRoomAsync(SocketMessage<JoinX01GameCommand> message, CancellationToken cancellationToken)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));

        foreach (var user in message.Message.Users)
        {
            if (!string.IsNullOrEmpty(user.ConnectionId))
            {
                var connectionId = user.UserId == message.Message.PlayerId
                    ? message.Message.ConnectionId : user.ConnectionId;

                var postConnectionRequest = new PostToConnectionRequest
                {
                    ConnectionId = connectionId,
                    Data = stream
                };

                stream.Position = 0;

                await ApiGatewayClient.PostToConnectionAsync(postConnectionRequest, cancellationToken);
            }
        }
    }
    public async Task UpdateGameStatus(SocketMessage<JoinX01GameCommand> message, CancellationToken cancellationToken)
    {
        if (message.Message.Players.Count() == 2)
        {
            message.Message.Game.Status = GameStatus.Started;
        }

        await DynamoDbService.WriteGameAsync(message.Message.Game, cancellationToken);
    }
    public static Dictionary<string, object> CreateMetaData(Game game, List<GameDart> darts, List<GamePlayer> players, List<User> users)
    {
        Metadata data = new Metadata();

        if (game is not null)
        {
            data.Game = new GameDto
            {
                Id = game.GameId.ToString(),
                PlayerCount = game.PlayerCount,
                Status = (GameStatusDto)(int)game.Status,
                Type = (GameTypeDto)(int)game.Type,
                X01 = new X01GameSettingsDto
                {
                    DoubleIn = game.X01.DoubleIn,
                    DoubleOut = game.X01.DoubleOut,
                    Legs = game.X01.Legs,
                    Sets = game.X01.Sets,
                    StartingScore = game.X01.StartingScore
                }
            };
        }

        if (darts is not null)
        {
            data.Darts = new();
            players.ForEach(p =>
            {
                data.Darts.Add(p.PlayerId, new());
                data.Darts[p.PlayerId] = darts
                    .OrderBy(x => x.CreatedAt)
                    .Where(x => x.PlayerId == p.PlayerId)
                    .Select(x => new DartDto
                    {
                        Id = x.Id,
                        Score = x.Score,
                        GameScore = x.GameScore,
                        Set = x.Set,
                        Leg = x.Leg,
                        CreatedAt = x.CreatedAt.Ticks
                    })
                    .ToList();
            });
        }

        if (players is not null)
        {
            var orderedPlayers = players.Select(x =>
            {
                return new PlayerDto
                {
                    PlayerId = x.PlayerId,
                    PlayerName = users.Single(y => y.UserId == x.PlayerId).Profile.UserName,
                    Country = users.Single(y => y.UserId == x.PlayerId).Profile.Country.ToLower(),
                    CreatedAt = x.PlayerId,
                    Legs = CalculateLegs(data, x.PlayerId),
                    Sets = CalculateSets(data, x.PlayerId)
                };
            }).OrderBy(x => x.CreatedAt);

            data.Players = orderedPlayers;
        }

        DetermineNextPlayer(data);

        return data.toDictionary();
    }
    public static string CalculateLegs(Metadata metadata, string playerId)
    {
        var darts = metadata.Darts[playerId].OrderBy(x => x.CreatedAt).Where(x => x.GameScore == 0);
        return darts.Count().ToString();
    }
    public static string CalculateSets(Metadata metadata, string playerId)
    {
        var darts = metadata.Darts[playerId].OrderBy(x => x.CreatedAt).Where(x => x.GameScore == 0);
        if (darts.Count() < metadata.Game.X01.Legs)
            return 0.ToString();
        return (darts.Count() / metadata.Game.X01.Legs).ToString();
    }
    public static void DetermineNextPlayer(Metadata metadata)
    {
        if (metadata.Players.Count() == 2)
        {
            var p1_count = metadata.Darts[metadata.Players.First().PlayerId].Count();
            var p2_count = metadata.Darts[metadata.Players.Last().PlayerId].Count();
            if (p1_count > p2_count)
            {
                metadata.NextPlayer = metadata.Players.Last().PlayerId;
            }
            else
            {
                metadata.NextPlayer = metadata.Players.First().PlayerId;
            }
        }
    }
}
