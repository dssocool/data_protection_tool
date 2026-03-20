using System.Threading;
using System.Threading.Tasks;

namespace DataProtectionTool.Services.Abstractions;

public interface IDelphixApiService
{
    Task<DelphixFileFormatUploadResponse> UploadFileFormatAsync(
        DelphixFileFormatUploadRequest request,
        CancellationToken cancellationToken = default);
}
