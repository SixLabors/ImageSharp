// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon.Cur;

/// <summary>
/// Defines constants relating to ICOs
/// </summary>
internal static class CurConstants
{
    /// <summary>
    /// The list of mimetypes that equate to a ico.
    /// </summary>
    /// <remarks>
    /// See <see href="https://en.wikipedia.org/wiki/ICO_(file_format)#MIME_type"/>
    /// </remarks>
    public static readonly IEnumerable<string> MimeTypes = new[]
    {
        "application/octet-stream",
    };

    /// <summary>
    /// The list of file extensions that equate to a ico.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = new[] { "cur" };

    public const uint FileHeader = 0x00_02_00_00;
}
