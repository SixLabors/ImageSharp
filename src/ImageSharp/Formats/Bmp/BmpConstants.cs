// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Defines constants relating to BMPs
/// </summary>
internal static class BmpConstants
{
    /// <summary>
    /// The list of mimetypes that equate to a bmp.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes =
    [
        "image/bmp",
        "image/x-windows-bmp",
        "image/x-win-bitmap"
    ];

    /// <summary>
    /// The list of file extensions that equate to a bmp.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = ["bm", "bmp", "dip"];

    /// <summary>
    /// Valid magic bytes markers identifying a Bitmap file.
    /// </summary>
    internal static class TypeMarkers
    {
        /// <summary>
        /// Single-image BMP file that may have been created under Windows or OS/2.
        /// </summary>
        public const int Bitmap = 0x4D42;

        /// <summary>
        /// OS/2 Bitmap Array.
        /// </summary>
        public const int BitmapArray = 0x4142;

        /// <summary>
        /// OS/2 Color Icon.
        /// </summary>
        public const int ColorIcon = 0x4943;

        /// <summary>
        /// OS/2 Color Pointer.
        /// </summary>
        public const int ColorPointer = 0x5043;

        /// <summary>
        /// OS/2 Icon.
        /// </summary>
        public const int Icon = 0x4349;

        /// <summary>
        /// OS/2 Pointer.
        /// </summary>
        public const int Pointer = 0x5450;
    }
}
