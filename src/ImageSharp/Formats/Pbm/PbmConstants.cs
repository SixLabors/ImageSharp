// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Contains PBM constant values defined in the specification.
/// </summary>
internal static class PbmConstants
{
    /// <summary>
    /// The maximum allowable pixel value of a ppm image.
    /// </summary>
    public const ushort MaxLength = 65535;

    /// <summary>
    /// The list of mimetypes that equate to a ppm.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = ["image/x-portable-pixmap", "image/x-portable-anymap"];

    /// <summary>
    /// The list of file extensions that equate to a ppm.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = ["ppm", "pbm", "pgm"];
}
