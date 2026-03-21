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
dotnet run --project DataProtectionTool.ClientApp/DataProtectionTool.ClientApp.csproj -c Debug
```

### Production mode (real Delphix service)

`Release` builds run in real mode by default.

```bash
dotnet run --project DataProtectionTool.ClientApp/DataProtectionTool.ClientApp.csproj -c Release
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
DATA_PROTECTION_TOOL_DELPHIX_MODE=fake dotnet run --project DataProtectionTool.ClientApp/DataProtectionTool.ClientApp.csproj -c Release
```

Run in real mode:

```bash
DATA_PROTECTION_TOOL_DELPHIX_MODE=real dotnet run --project DataProtectionTool.ClientApp/DataProtectionTool.ClientApp.csproj -c Debug
```

### Windows PowerShell

```powershell
$env:DATA_PROTECTION_TOOL_DELPHIX_MODE = "fake"
dotnet run --project DataProtectionTool.ClientApp/DataProtectionTool.ClientApp.csproj -c Release
```

```powershell
$env:DATA_PROTECTION_TOOL_DELPHIX_MODE = "real"
dotnet run --project DataProtectionTool.ClientApp/DataProtectionTool.ClientApp.csproj -c Debug
```

## Mock Delphix HTTP server (local)

For manual testing with `curl`, run the minimal mock that implements `POST /masking/api/v5.1.44/file-formats`:

```bash
dotnet run --project DataProtectionTool.MockServer/DataProtectionTool.MockServer.csproj
```

Swagger UI is available at:

- `http://localhost:5057/swagger`

Default URL: `http://localhost:5057`. Override with `--urls`, for example:

```bash
dotnet run --project DataProtectionTool.MockServer/DataProtectionTool.MockServer.csproj --urls "http://localhost:8080"
```

Example `curl` (replace the CSV path with a real file on your machine):

```bash
curl -X POST 'http://localhost:5057/masking/api/v5.1.44/file-formats' \
  -H 'accept: application/json' \
  -H 'Authorization: 5d8b21d0-779f-475f-bdae-41cf13f93541' \
  -H 'Content-Type: multipart/form-data' \
  -F 'fileFormat=@./your-format.csv;type=text/csv' \
  -F 'fileFormatType=DELIMITED'
```

Success response shape:

```json
{
  "fileFormatId": 51,
  "fileFormatName": "your-format.csv",
  "fileFormatType": "DELIMITED"
}
```

- Missing `Authorization` → `401` with JSON `{ "error": "..." }`.
- Missing `fileFormat` file or invalid form → `400` with JSON `{ "error": "..." }`.

## Run Tests

Run all tests:

```bash
dotnet test DataProtectionTool.Tests/DataProtectionTool.Tests.csproj
```

Run tests with detailed output:

```bash
dotnet test DataProtectionTool.Tests/DataProtectionTool.Tests.csproj -v normal
```
