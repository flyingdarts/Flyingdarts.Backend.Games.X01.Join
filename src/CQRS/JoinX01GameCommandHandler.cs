using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using System.Threading;
using System.Threading.Tasks;
using Flyingdarts.Persistence;
using MediatR;
using Flyingdarts.Lambdas.Shared;
using System.Collections.Generic;
using System.Linq;
using System;

public record JoinX01GameCommandHandler(IDynamoDbService DynamoDbService) : IRequestHandler<JoinX01GameCommand, APIGatewayProxyResponse>
{
    public async Task<APIGatewayProxyResponse> Handle(JoinX01GameCommand request, CancellationToken cancellationToken)
    {
        var socketMessage = new SocketMessage<JoinX01GameCommand>
        {
            Action = "v2/games/x01/join",
            Message = request
        };

        request.Game = await DynamoDbService.ReadGameAsync(long.Parse(request.GameId), cancellationToken);
        request.Players = await DynamoDbService.ReadGamePlayersAsync(long.Parse(request.GameId), cancellationToken);
        request.Users = await DynamoDbService.ReadUsersAsync(request.Players.Select(x => x.PlayerId).ToArray(), cancellationToken);
        request.Darts = await DynamoDbService.ReadGameDartsAsync(long.Parse(request.GameId), cancellationToken);

        Metadata data = new Metadata();

        if (request.Game is not null)
        {
            var player = GamePlayer.Create(long.Parse(request.GameId), request.PlayerId);

            await DynamoDbService.WriteGamePlayerAsync(player, cancellationToken);

            data.Game = new GameDto
            {
                Id = request.Game.GameId.ToString(),
                PlayerCount = request.Game.PlayerCount,
                Status = (GameStatusDto)(int)request.Game.Status,
                Type = (GameTypeDto)(int)request.Game.Type,
                X01 = new X01GameSettingsDto
                {
                    DoubleIn = request.Game.X01.DoubleIn,
                    DoubleOut = request.Game.X01.DoubleOut,
                    Legs = request.Game.X01.Legs,
                    Sets = request.Game.X01.Sets,
                    StartingScore = request.Game.X01.StartingScore
                }
            };
        }

        if (request.Players is not null)
        {
            var orderedPlayers = request.Players.Select(x =>
            {
                return new PlayerDto
                {
                    PlayerId = x.PlayerId,
                    PlayerName = request.Users.Single(y => y.UserId == x.PlayerId).Profile.UserName,
                    Country = request.Users.Single(y => y.UserId == x.PlayerId).Profile.Country.ToLower(),
                    CreatedAt = long.Parse(x.PlayerId)
                };
            }).OrderBy(x => x.CreatedAt);

            data.Players = orderedPlayers;
        }

        if (request.Darts is not null)
        {
            data.Darts = new();
            request.Players.ForEach(p =>
            {
                data.Darts.Add(p.PlayerId, new());
                data.Darts[p.PlayerId] = request.Darts.OrderBy(x => x.CreatedAt).Where(x => x.PlayerId == p.PlayerId).Select(x => new DartDto {Id =x.Id, Score = x.Score, GameScore = x.GameScore}).ToList();
            });
        }

        socketMessage.Metadata = data.toDictionary();

        return new APIGatewayProxyResponse { StatusCode = 200, Body = JsonSerializer.Serialize(socketMessage) };
    }
}
