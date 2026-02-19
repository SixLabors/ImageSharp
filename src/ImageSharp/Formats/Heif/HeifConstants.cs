// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Contains HEIF constant values defined in the specification.
/// </summary>
internal static class HeifConstants
{
    public const Heif4CharCode HeicBrand = Heif4CharCode.Heic;

    /// <summary>
    /// The list of mimetypes that equate to a HEIC.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = new[] { "image/heif", "image/heic", "image/avif" };

    /// <summary>
    /// The list of file extensions that equate to a HEIC.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = new[] { "heic", "heif", "hif", "avif" };
}
