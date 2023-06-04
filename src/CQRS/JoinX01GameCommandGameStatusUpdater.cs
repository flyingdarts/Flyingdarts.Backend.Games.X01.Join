using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Flyingdarts.Persistence;
using MediatR.Pipeline;
public record JoinX01GameCommandGameStatusUpdater(IDynamoDbService DynamoDbService) : IRequestPostProcessor<JoinX01GameCommand, APIGatewayProxyResponse>
{
    public async Task Process(JoinX01GameCommand request, APIGatewayProxyResponse response, CancellationToken cancellationToken)
    {
        if (request.Players.Count() == 2)
        {
            request.Game.Status = GameStatus.Started;
        }

        await DynamoDbService.WriteGameAsync(request.Game, cancellationToken);
    }
}

