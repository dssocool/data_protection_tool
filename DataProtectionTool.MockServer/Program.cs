var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5057");

var app = builder.Build();

const string FileFormatsPath = "/masking/api/v5.1.44/file-formats";

app.MapPost(FileFormatsPath, async Task<IResult> (HttpRequest request) =>
{
    if (!request.Headers.TryGetValue("Authorization", out var auth) || string.IsNullOrWhiteSpace(auth))
    {
        return Results.Json(new { error = "Missing or empty Authorization header." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (!request.Headers.TryGetValue("Accept", out var accept) ||
        !accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            new { error = "Accept header must include application/json." },
            statusCode: StatusCodes.Status400BadRequest);
    }

    if (!request.HasFormContentType)
    {
        return Results.Json(
            new { error = "Content-Type must be multipart/form-data." },
            statusCode: StatusCodes.Status400BadRequest);
    }

    IFormCollection form;
    try
    {
        form = await request.ReadFormAsync();
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { error = "Invalid multipart form.", detail = ex.Message },
            statusCode: StatusCodes.Status400BadRequest);
    }

    var file = form.Files.GetFile("fileFormat");
    if (file is null || file.Length == 0)
    {
        return Results.Json(
            new { error = "Missing or empty fileFormat file field." },
            statusCode: StatusCodes.Status400BadRequest);
    }

    var fileFormatType = form["fileFormatType"].ToString();
    if (string.IsNullOrWhiteSpace(fileFormatType))
    {
        fileFormatType = "DELIMITED";
    }

    var fileName = string.IsNullOrWhiteSpace(file.FileName)
        ? "upload.csv"
        : Path.GetFileName(file.FileName);

    var body = new FileFormatUploadResponse(
        FileFormatId: 51,
        FileFormatName: fileName,
        FileFormatType: fileFormatType);

    return Results.Json(body);
})
.DisableAntiforgery();

app.MapGet("/", () => Results.Text(
    $"Mock Delphix API. POST {FileFormatsPath} with multipart fileFormat + fileFormatType."));

app.Run();

internal sealed record FileFormatUploadResponse(int FileFormatId, string FileFormatName, string FileFormatType);
