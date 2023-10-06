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

           data.Game = request.Game;
        }

        if (request.Players is not null)
        {
            var orderedPlayers = request.Players.Select(x =>
            {
                return new PlayerDto
                {
                    PlayerId = x.PlayerId,
                    PlayerName = request.Users.Single(y => y.UserId == x.PlayerId).Profile.UserName
                };
            }).OrderBy(x=>x.CreatedAt);

            data.Players = orderedPlayers;
        }

        if (request.Darts is not null)
        {
            data.Darts = new();
            request.Players.ForEach(p =>
            {
                data.Darts.Add(p.PlayerId, new());
                data.Darts[p.PlayerId].History = request.Darts.OrderBy(x => x.CreatedAt).Where(x => x.PlayerId == p.PlayerId).Select(x => x.Score).ToList();
            });
        }

        socketMessage.Metadata = data.toDictionary();

        return new APIGatewayProxyResponse { StatusCode = 200, Body = JsonSerializer.Serialize(socketMessage) };
    }
}


class Metadata
{
    public Game Game { get; set; }
    public IOrderedEnumerable<PlayerDto> Players { get; set; }
    public Dictionary<string, ScoreboardRecord> Darts { get; set; }

    public Dictionary<string, object> toDictionary()
    {
        var result = new Dictionary<string, object>
        {
            { "Game", Game },
            { "Players", Players },
            { "Darts", Darts }
        };

        return result;
    }
}

class PlayerDto
{
    public String PlayerId { get; set; }
    public String PlayerName { get; set; }
    public DateTime CreatedAt { get; set; }
}