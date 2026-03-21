using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DataProtectionTool.Contracts.Grpc;
using Grpc.Net.Client;

namespace DataProtectionTool.ClientApp.Services;

/// <summary>
/// Lightweight gRPC client for <see cref="MasterService.MasterServiceClient"/>.
/// </summary>
public static class MasterGrpcClient
{
    private const string MasterUrlEnvVar = "DATA_PROTECTION_TOOL_MASTER_URL";
    private const string DefaultMasterUrl = "http://localhost:5055";

    /// <summary>
    /// Sends a single Ping to the master. Intended for startup connectivity checks; failures are logged, not thrown to callers that wrap this.
    /// </summary>
    public static async Task TryPingAsync(CancellationToken cancellationToken = default)
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var baseUrl = Environment.GetEnvironmentVariable(MasterUrlEnvVar);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultMasterUrl;
        }

        using var channel = GrpcChannel.ForAddress(baseUrl.Trim());
        var client = new MasterService.MasterServiceClient(channel);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(3));

        var reply = await client.PingAsync(
            new PingRequest { ClientName = "DataProtectionTool.ClientApp" },
            cancellationToken: linked.Token).ConfigureAwait(false);

        Trace.WriteLine($"[Master] Ping OK: {reply.Message} (server time {reply.ServerUtcTimeRfc3339})");
    }
}
