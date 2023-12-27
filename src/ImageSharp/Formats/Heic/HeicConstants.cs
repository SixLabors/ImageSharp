// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Contains HEIC (and H.265) constant values defined in the specification.
/// </summary>
internal static class HeicConstants
{
    public const Heic4CharCode HeicBrand = Heic4CharCode.heic;

    /// <summary>
    /// The list of mimetypes that equate to a HEIC.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = new[] { "image/heif", "image/heic", "image/avif" };

    /// <summary>
    /// The list of file extensions that equate to a HEIC.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = new[] { "heic", "heif", "avif" };
}
