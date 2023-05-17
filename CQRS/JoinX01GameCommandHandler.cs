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
        await JoinGame(request.GameId, request.PlayerId, cancellationToken);

        var socketMessage = new SocketMessage<JoinX01GameCommand>
        {
            Action = "v2/games/x01/join",
            Message = request
        };
        return new APIGatewayProxyResponse { StatusCode = 200, Body = JsonSerializer.Serialize(socketMessage) };
    }

    private async Task JoinGame(long gameId, Guid playerId, CancellationToken cancellationToken)
    {
        var gamePlayer = GamePlayer.Create(gameId, playerId);
        var gamePlayerWrite = _dbContext.CreateBatchWrite<GamePlayer>(_applicationOptions.ToOperationConfig()); gamePlayerWrite.AddPutItem(gamePlayer);

        await gamePlayerWrite.ExecuteAsync(cancellationToken);
    }
}