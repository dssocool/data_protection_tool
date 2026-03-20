using System.Net;

namespace DataProtectionTool.Services.Abstractions;

public sealed record DelphixFileFormatUploadResponse(
    bool IsSuccess,
    HttpStatusCode StatusCode,
    string RawJson,
    string? FileFormatId,
    string? FileFormatName,
    string? ErrorMessage);
