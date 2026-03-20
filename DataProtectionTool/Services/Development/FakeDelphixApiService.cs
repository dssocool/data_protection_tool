using DataProtectionTool.Services.Abstractions;
using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataProtectionTool.Services.Development;

public sealed class FakeDelphixApiService : IDelphixApiService
{
    private readonly bool _simulateFailure;

    public FakeDelphixApiService(bool simulateFailure = false)
    {
        _simulateFailure = simulateFailure;
    }

    public Task<DelphixFileFormatUploadResponse> UploadFileFormatAsync(
        DelphixFileFormatUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        if (_simulateFailure)
        {
            return Task.FromResult(
                new DelphixFileFormatUploadResponse(
                    IsSuccess: false,
                    StatusCode: HttpStatusCode.BadRequest,
                    RawJson: "{\"error\":\"Simulated Delphix failure in development mode.\"}",
                    FileFormatId: null,
                    FileFormatName: null,
                    ErrorMessage: "Simulated Delphix failure in development mode."));
        }

        var payload = new
        {
            id = "fake-file-format-id",
            name = request.FileName,
            fileFormatType = request.FileFormatType,
            source = "FakeDelphixApiService"
        };

        var rawJson = JsonSerializer.Serialize(payload);

        return Task.FromResult(
            new DelphixFileFormatUploadResponse(
                IsSuccess: true,
                StatusCode: HttpStatusCode.OK,
                RawJson: rawJson,
                FileFormatId: payload.id,
                FileFormatName: payload.name,
                ErrorMessage: null));
    }
}
