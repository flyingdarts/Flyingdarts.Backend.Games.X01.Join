using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.Lambda.APIGatewayEvents;
using Flyingdarts.Lambdas.Shared;
using MediatR.Pipeline;

public record JoinX01GameCommandNotifyRoomHandler(IDynamoDbService DynamoDbService, IAmazonApiGatewayManagementApi ApiGatewayClient) : IRequestPostProcessor<JoinX01GameCommand, APIGatewayProxyResponse>
{
    public async Task Process(JoinX01GameCommand request, APIGatewayProxyResponse response, CancellationToken cancellationToken)
    {
        var players = await DynamoDbService.ReadGamePlayersAsync(long.Parse(request.GameId), cancellationToken);
        var users = await DynamoDbService.ReadUsersAsync(players.Select(x => x.PlayerId).ToArray(), cancellationToken);

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

                await ApiGatewayClient.PostToConnectionAsync(postConnectionRequest, cancellationToken);
            }
        }
    }
}

