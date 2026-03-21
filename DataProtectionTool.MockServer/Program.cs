using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5057");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Mock Delphix Masking API",
        Version = "v5.1.44",
        Description = "Local mock server. Use Authorize and paste your token as the Authorization header value."
    });

    var authorizationScheme = new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Name = "Authorization",
        Description = "Delphix-style token sent as raw Authorization header value."
    };

    options.AddSecurityDefinition("Authorization", authorizationScheme);
    options.AddSecurityRequirement(document =>
    {
        var authorizationSchemeReference = new Microsoft.OpenApi.OpenApiSecuritySchemeReference(
            "Authorization",
            hostDocument: document,
            externalResource: null!);

        return new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            [authorizationSchemeReference] = new List<string>()
        };
    });

});

var app = builder.Build();

const string FileFormatsPath = "/masking/api/v5.1.44/file-formats";

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost(FileFormatsPath, IResult (
    HttpRequest request,
    [FromForm] UploadFileFormatForm form) =>
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

    if (form.FileFormat is null || form.FileFormat.Length == 0)
    {
        return Results.Json(
            new { error = "Missing or empty fileFormat file field." },
            statusCode: StatusCodes.Status400BadRequest);
    }

    var fileFormatType = string.IsNullOrWhiteSpace(form.FileFormatType)
        ? "DELIMITED"
        : form.FileFormatType;

    var fileName = string.IsNullOrWhiteSpace(form.FileFormat.FileName)
        ? "upload.csv"
        : Path.GetFileName(form.FileFormat.FileName);

    var body = new FileFormatUploadResponse(
        FileFormatId: 51,
        FileFormatName: fileName,
        FileFormatType: fileFormatType);

    return Results.Ok(body);
})
.DisableAntiforgery()
.Accepts<UploadFileFormatForm>("multipart/form-data")
.Produces<FileFormatUploadResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.WithName("UploadFileFormat")
.WithSummary("Mock Delphix file-format upload")
.WithDescription("Accepts multipart/form-data with fileFormat file and fileFormatType text field.");

app.MapGet("/", () => Results.Text(
    $"Mock Delphix API. POST {FileFormatsPath} with multipart fileFormat + fileFormatType. Swagger UI at /swagger."));

app.Run();

internal sealed record FileFormatUploadResponse(int FileFormatId, string FileFormatName, string FileFormatType);

internal sealed class UploadFileFormatForm
{
    [FromForm(Name = "fileFormat")]
    public IFormFile? FileFormat { get; init; }

    [FromForm(Name = "fileFormatType")]
    public string? FileFormatType { get; init; }
}
