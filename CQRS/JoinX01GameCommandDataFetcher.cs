using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR.Pipeline;
public record JoinX01GameCommandDataFetcher(IDynamoDbService DynamoDbService) : IRequestPreProcessor<JoinX01GameCommand>
{
    public async Task Process(JoinX01GameCommand request, CancellationToken cancellationToken)
    {
        request.Game = await DynamoDbService.ReadGameAsync(long.Parse(request.GameId), cancellationToken);
        request.Players = await DynamoDbService.ReadGamePlayersAsync(long.Parse(request.GameId), cancellationToken);
        request.Darts = await DynamoDbService.ReadGameDartsAsync(long.Parse(request.GameId), cancellationToken);
        request.Users = await DynamoDbService.ReadUsersAsync(request.Players.Select(x => x.PlayerId).ToArray(), cancellationToken);
    }
}