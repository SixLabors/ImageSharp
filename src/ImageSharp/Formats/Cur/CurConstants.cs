// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// Defines constants used by the CUR format.
/// </summary>
internal static class CurConstants
{
    /// <summary>
    /// The MIME types that identify CUR data.
    /// </summary>
    /// <remarks>
    /// See <see href="https://en.wikipedia.org/wiki/ICO_(file_format)#MIME_type"/>
    /// </remarks>
    public static readonly IEnumerable<string> MimeTypes =
    [

        // IANA-registered
        "image/vnd.microsoft.icon",

        // ICO & CUR types used by Windows
        "image/x-icon",

        // Erroneous types but have been used
        "image/ico",
        "image/icon",
        "text/ico",
        "application/ico"
    ];

    /// <summary>
    /// The file extensions that identify CUR data.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = ["cur"];
}
