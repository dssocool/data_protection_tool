using System.Collections.Generic;

namespace DataProtectionTool;

public sealed record DataItemDiscoveryResult(IReadOnlyList<string> ItemNames, string ErrorMessage);
