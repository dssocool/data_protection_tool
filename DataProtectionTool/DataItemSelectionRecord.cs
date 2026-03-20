using System;
using System.Collections.Generic;

namespace DataProtectionTool;

public class DataItemSelectionRecord
{
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    public string ConnectionType { get; set; } = string.Empty;
    public string ConnectionDisplayName { get; set; } = string.Empty;
    public List<string> ItemNames { get; set; } = [];
}
