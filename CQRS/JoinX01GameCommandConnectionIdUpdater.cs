using System.Threading;
using System.Threading.Tasks;
using MediatR.Pipeline;
public record JoinX01GameCommandConnectionIdUpdater(IDynamoDbService DynamoDbService) : IRequestPreProcessor<JoinX01GameCommand>
{
    public async Task Process(JoinX01GameCommand request, CancellationToken cancellationToken)
    {
        var user = await DynamoDbService.ReadUserAsync(request.PlayerId, cancellationToken);

        user.ConnectionId = request.ConnectionId;

        await DynamoDbService.WriteUserAsync(user, cancellationToken);
    }
}