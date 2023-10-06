using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR.Pipeline;
public record JoinX01GameCommandDataFetcher(IDynamoDbService DynamoDbService) : IRequestPreProcessor<JoinX01GameCommand>
{
    public async Task Process(JoinX01GameCommand request, CancellationToken cancellationToken)
    {
        request.Game = await DynamoDbService.ReadGameAsync(long.Parse(request.GameId), cancellationToken);
        if (request.Game == null)
        {
            throw new Exception($"Game is null ${request.GameId}");
        }
        request.Players = await DynamoDbService.ReadGamePlayersAsync(long.Parse(request.GameId), cancellationToken);
        if (request.Players == null)
        {
            throw new Exception($"Game players is null ${request.GameId}");
        }
        request.Users = await DynamoDbService.ReadUsersAsync(request.Players.Select(x => x.PlayerId).ToArray(), cancellationToken);
        if (request.Users == null)
        {
            throw new Exception($"Users is null ${request.GameId}");
        }
        request.Darts = await DynamoDbService.ReadGameDartsAsync(long.Parse(request.GameId), cancellationToken);
    }
}