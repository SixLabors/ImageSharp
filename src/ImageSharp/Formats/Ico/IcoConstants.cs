// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ico;

/// <summary>
/// Defines constants relating to ICOs
/// </summary>
internal static class IcoConstants
{
    /// <summary>
    /// The list of mimetypes that equate to a ico.
    /// </summary>
    /// <remarks>
    /// See <see href="https://en.wikipedia.org/wiki/ICO_(file_format)#MIME_type"/>
    /// </remarks>
    public static readonly IEnumerable<string> MimeTypes = new[]
    {
        // IANA-registered
        "image/vnd.microsoft.icon",

        // ICO & CUR types used by Windows
        "image/x-icon",

        // Erroneous types but have been used
        "image/ico",
        "image/icon",
        "text/ico",
        "application/ico",
    };

    /// <summary>
    /// The list of file extensions that equate to a ico.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = new[] { "ico" };

    public const uint FileHeader = 0x00_01_00_00;
}
