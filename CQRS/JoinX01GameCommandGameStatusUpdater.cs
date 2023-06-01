using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Flyingdarts.Persistence;
using Flyingdarts.Shared;
using MediatR.Pipeline;
using Microsoft.Extensions.Options;

namespace Flyingdarts.Backend.Games.X01.Join.CQRS
{
    public class JoinX01GameCommandGameStatusUpdater : IRequestPostProcessor<JoinX01GameCommand, APIGatewayProxyResponse>
    {
        private readonly IDynamoDBContext _dbContext;
        private readonly ApplicationOptions _applicationOptions;

        public JoinX01GameCommandGameStatusUpdater(IDynamoDBContext dbContext, IOptions<ApplicationOptions> applicationOptions)
        {
            _dbContext = dbContext;
            _applicationOptions = applicationOptions.Value;
        }

        public async Task Process(JoinX01GameCommand request, APIGatewayProxyResponse response, CancellationToken cancellationToken)
        {
            if (request.Players.Count() == 2) {
                request.Game.Status = GameStatus.Started;
            }

            var gameWrite = _dbContext.CreateBatchWrite<Game>(_applicationOptions.ToOperationConfig());

            gameWrite.AddPutItem(request.Game);

            await gameWrite.ExecuteAsync(cancellationToken);
        }
    }
}
