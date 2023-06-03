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
        request.History = new();
        request.Players.ForEach(p =>
        {
            request.History.Add(p.PlayerId, new());
            request.History[p.PlayerId].History = request.Darts.OrderBy(x => x.CreatedAt).Where(x => x.PlayerId == p.PlayerId).Select(x => x.Score).ToList();
        });

        var socketMessage = new SocketMessage<JoinX01GameCommand>
        {
            Action = "v2/games/x01/join",
            Message = request
        };

        if (request.Game is not null)
        {
            var player = GamePlayer.Create(long.Parse(request.GameId), request.PlayerId);
            var write = _dbContext.CreateBatchWrite<GamePlayer>(_applicationOptions.ToOperationConfig());

            write.AddPutItem(player);
            await write.ExecuteAsync(cancellationToken);

            socketMessage.Message.Game = request.Game;
        }

        if (request.Players is not null)
        {
            var retVal = request.Players.Select(x =>
            {
                return new JoinX01GameCommand
                {
                    PlayerId = x.PlayerId,
                    PlayerName = request.Users.Single(y => y.UserId == x.PlayerId).Profile.UserName
                };
            }).ToArray();

            socketMessage.Message.Metadata = new Dictionary<string, object>
            {
                { "CurrentPlayers", retVal }
            };
        }

        return new APIGatewayProxyResponse { StatusCode = 200, Body = JsonSerializer.Serialize(socketMessage) };
    }
}