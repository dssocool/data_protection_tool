using DataProtectionTool.Services.Abstractions;
using DataProtectionTool.Services.Development;
using DataProtectionTool.Services.Http;
using System;
using System.Net.Http;

namespace DataProtectionTool.Services;

public static class DelphixApiServiceResolver
{
    private const string ModeOverrideEnvironmentVariable = "DATA_PROTECTION_TOOL_DELPHIX_MODE";
    private const string FakeMode = "fake";
    private const string RealMode = "real";

    public static IDelphixApiService CreateForCurrentMode()
    {
        var modeOverride = Environment.GetEnvironmentVariable(ModeOverrideEnvironmentVariable);
        if (modeOverride is not null)
        {
            if (modeOverride.Equals(FakeMode, StringComparison.OrdinalIgnoreCase))
            {
                return new FakeDelphixApiService();
            }

            if (modeOverride.Equals(RealMode, StringComparison.OrdinalIgnoreCase))
            {
                return CreateRealService();
            }
        }

#if DEBUG
        return new FakeDelphixApiService();
#else
        return CreateRealService();
#endif
    }

    private static IDelphixApiService CreateRealService()
    {
        var options = new DelphixApiServiceOptions(
            BaseApiUrl: "https://delphixphx.net/masking/api/v5.1.44",
            AuthorizationToken: "b40abefd-af25-4d89-a6cc-54455254cbb9");

        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        return new DelphixApiService(httpClient, options);
    }
}
