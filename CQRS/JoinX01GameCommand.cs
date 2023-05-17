using System;
using Amazon.Lambda.APIGatewayEvents;
using MediatR;

public class JoinX01GameCommand : IRequest<APIGatewayProxyResponse>
{
    public long GameId { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; }
}