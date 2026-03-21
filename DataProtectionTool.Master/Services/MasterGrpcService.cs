using DataProtectionTool.Contracts.Grpc;
using Grpc.Core;

namespace DataProtectionTool.Master.Services;

public sealed class MasterGrpcService : MasterService.MasterServiceBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        var name = string.IsNullOrWhiteSpace(request.ClientName) ? "client" : request.ClientName.Trim();
        var reply = new PingReply
        {
            Message = $"Pong from master (hello {name})",
            ServerUtcTimeRfc3339 = DateTimeOffset.UtcNow.ToString("o")
        };
        return Task.FromResult(reply);
    }
}
