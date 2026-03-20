# Data Protection Tool

Desktop app built with Avalonia and .NET 9.

## Prerequisites

- .NET SDK 9.0+

Verify your SDK:

```bash
dotnet --version
```

## Run The App

### Development mode (fake Delphix service)

`Debug` builds run in fake mode by default.

```bash
dotnet run --project DataProtectionTool/DataProtectionTool.csproj -c Debug
```

### Production mode (real Delphix service)

`Release` builds run in real mode by default.

```bash
dotnet run --project DataProtectionTool/DataProtectionTool.csproj -c Release
```

## Force Mode With Environment Variable

You can override the default mode selection with:

`DATA_PROTECTION_TOOL_DELPHIX_MODE`

Allowed values:

- `fake` (development behavior)
- `real` (production behavior)

### macOS / Linux

Run in fake mode:

```bash
DATA_PROTECTION_TOOL_DELPHIX_MODE=fake dotnet run --project DataProtectionTool/DataProtectionTool.csproj -c Release
```

Run in real mode:

```bash
DATA_PROTECTION_TOOL_DELPHIX_MODE=real dotnet run --project DataProtectionTool/DataProtectionTool.csproj -c Debug
```

### Windows PowerShell

```powershell
$env:DATA_PROTECTION_TOOL_DELPHIX_MODE = "fake"
dotnet run --project DataProtectionTool/DataProtectionTool.csproj -c Release
```

```powershell
$env:DATA_PROTECTION_TOOL_DELPHIX_MODE = "real"
dotnet run --project DataProtectionTool/DataProtectionTool.csproj -c Debug
```

## Run Tests

Run all tests:

```bash
dotnet test DataProtectionTool.Tests/DataProtectionTool.Tests.csproj
```

Run tests with detailed output:

```bash
dotnet test DataProtectionTool.Tests/DataProtectionTool.Tests.csproj -v normal
```
