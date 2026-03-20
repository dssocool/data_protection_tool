using DataProtectionTool.Services.Abstractions;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DataProtectionTool.Services.Http;

public sealed class DelphixApiService : IDelphixApiService
{
    private readonly HttpClient _httpClient;
    private readonly DelphixApiServiceOptions _options;

    public DelphixApiService(HttpClient httpClient, DelphixApiServiceOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public async Task<DelphixFileFormatUploadResponse> UploadFileFormatAsync(
        DelphixFileFormatUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var endpoint = $"{_options.BaseApiUrl.TrimEnd('/')}/file-formats";

        using var multipartContent = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(request.FileContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);

        multipartContent.Add(fileContent, "fileFormat", request.FileName);
        multipartContent.Add(
            new StringContent(request.FileFormatType, Encoding.UTF8),
            "fileFormatType");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.TryAddWithoutValidation("Authorization", _options.AuthorizationToken);
        httpRequest.Content = multipartContent;

        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var rawJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorMessage = $"Delphix upload failed with status {(int)httpResponse.StatusCode} ({httpResponse.StatusCode}).";
            if (!string.IsNullOrWhiteSpace(rawJson))
            {
                errorMessage = $"{errorMessage} Response: {rawJson}";
            }

            return new DelphixFileFormatUploadResponse(
                IsSuccess: false,
                StatusCode: httpResponse.StatusCode,
                RawJson: rawJson,
                FileFormatId: null,
                FileFormatName: null,
                ErrorMessage: errorMessage);
        }

        string? fileFormatId = null;
        string? fileFormatName = null;

        if (!string.IsNullOrWhiteSpace(rawJson))
        {
            try
            {
                using var document = JsonDocument.Parse(rawJson);
                var root = document.RootElement;

                if (root.TryGetProperty("fileFormatId", out var fileFormatIdProperty))
                {
                    fileFormatId = fileFormatIdProperty.GetString();
                }
                else if (root.TryGetProperty("id", out var idProperty))
                {
                    fileFormatId = idProperty.GetString();
                }

                if (root.TryGetProperty("name", out var nameProperty))
                {
                    fileFormatName = nameProperty.GetString();
                }
            }
            catch (JsonException)
            {
                // Keep raw JSON for diagnostics; response can still be treated as success.
            }
        }

        return new DelphixFileFormatUploadResponse(
            IsSuccess: true,
            StatusCode: httpResponse.StatusCode,
            RawJson: rawJson,
            FileFormatId: fileFormatId,
            FileFormatName: fileFormatName,
            ErrorMessage: null);
    }
}
