using System;

namespace DataProtectionTool.Services.Abstractions;

public sealed record DelphixApiServiceOptions(string BaseApiUrl, string AuthorizationToken)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseApiUrl))
        {
            throw new ArgumentException("Base API URL is required.", nameof(BaseApiUrl));
        }

        if (string.IsNullOrWhiteSpace(AuthorizationToken))
        {
            throw new ArgumentException("Authorization token is required.", nameof(AuthorizationToken));
        }
    }
}
