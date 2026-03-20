using System;

namespace DataProtectionTool.Services.Abstractions;

public sealed record DelphixFileFormatUploadRequest(
    string FileName,
    byte[] FileContent,
    string FileFormatType = "DELIMITED",
    string ContentType = "text/csv")
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FileName))
        {
            throw new ArgumentException("File name is required.", nameof(FileName));
        }

        if (FileContent is null || FileContent.Length == 0)
        {
            throw new ArgumentException("File content must not be empty.", nameof(FileContent));
        }

        if (string.IsNullOrWhiteSpace(FileFormatType))
        {
            throw new ArgumentException("File format type is required.", nameof(FileFormatType));
        }

        if (string.IsNullOrWhiteSpace(ContentType))
        {
            throw new ArgumentException("Content type is required.", nameof(ContentType));
        }
    }
}
