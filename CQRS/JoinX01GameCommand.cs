using System;
using Amazon.Lambda.APIGatewayEvents;
using Flyingdarts.Persistence;
using MediatR;

public class JoinX01GameCommand : IRequest<APIGatewayProxyResponse>
{
    public string GameId { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; }
    public Game? Game { get; set; }
    internal string ConnectionId { get; set; }
}