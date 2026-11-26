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
    /// The bit depths of each color type. The first depth of each color type is the default depth. The encoder uses it
    /// when the requested depth does not fit the color type.
    /// </summary>
    public static readonly Dictionary<BmpColorType, BmpBitsPerPixel[]> ColorTypes = new()
    {
        [BmpColorType.Palette] = [BmpBitsPerPixel.Bit8, BmpBitsPerPixel.Bit1, BmpBitsPerPixel.Bit2, BmpBitsPerPixel.Bit4],
        [BmpColorType.Bgr] = [BmpBitsPerPixel.Bit24, BmpBitsPerPixel.Bit16, BmpBitsPerPixel.Bit32],
        [BmpColorType.Bgra] = [BmpBitsPerPixel.Bit32],
        [BmpColorType.Bgr565] = [BmpBitsPerPixel.Bit16],
        [BmpColorType.Bgra5551] = [BmpBitsPerPixel.Bit16]
    };

    /// <summary>
    /// Gets the bit depth that the encoder writes for a color type. It is the requested depth when that depth fits the
    /// color type. Otherwise it is the first depth of the color type in <see cref="ColorTypes"/>.
    /// </summary>
    /// <param name="colorType">The color type.</param>
    /// <param name="bitsPerPixel">The requested bit depth.</param>
    /// <returns>The bit depth.</returns>
    public static BmpBitsPerPixel GetBitsPerPixel(BmpColorType colorType, BmpBitsPerPixel bitsPerPixel)
    {
        BmpBitsPerPixel[] depths = ColorTypes[colorType];
        return Array.IndexOf(depths, bitsPerPixel) >= 0 ? bitsPerPixel : depths[0];
    }

    /// <summary>
    /// Gets the byte count of a stored row. A stored row starts on a four-byte boundary, so the pixel bits round up to a multiple of 32. See BITMAPINFOHEADER,
    /// "Calculating Surface Stride": https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader
    /// </summary>
    /// <remarks>
    /// The bit count uses 64-bit arithmetic, so a wide row does not overflow before the division by 8.
    /// </remarks>
    /// <param name="width">The pixel count of the row.</param>
    /// <param name="bitsPerPixel">The bit count of one pixel.</param>
    /// <returns>The byte count of the row, with its padding.</returns>
    public static int GetStride(int width, int bitsPerPixel) => (int)(((((long)width * bitsPerPixel) + 31) & ~31L) >> 3);

    /// <summary>
    /// The channel masks of a 16-bit bitmap with the 5-5-5 layout. A 16-bit BI_RGB bitmap has the layout of the color
    /// masks. The 5-5-5-1 layout adds the alpha mask.
    /// </summary>
    public static class Bgr555Masks
    {
        /// <summary>
        /// The red mask: bits 10 to 14.
        /// </summary>
        public const int Red = 0x7C00;

        /// <summary>
        /// The green mask: bits 5 to 9.
        /// </summary>
        public const int Green = 0x03E0;

        /// <summary>
        /// The blue mask: bits 0 to 4.
        /// </summary>
        public const int Blue = 0x001F;

        /// <summary>
        /// The alpha mask: bit 15.
        /// </summary>
        public const int Alpha = 0x8000;
    }

    /// <summary>
    /// The channel masks of a 16-bit bitmap with the 5-6-5 layout.
    /// </summary>
    public static class Bgr565Masks
    {
        /// <summary>
        /// The red mask: bits 11 to 15.
        /// </summary>
        public const int Red = 0xF800;

        /// <summary>
        /// The green mask: bits 5 to 10.
        /// </summary>
        public const int Green = 0x07E0;

        /// <summary>
        /// The blue mask: bits 0 to 4.
        /// </summary>
        public const int Blue = 0x001F;
    }

    /// <summary>
    /// The channel masks of a 32-bit bitmap that stores each pixel as the bytes B, G, R, A. A 32-bit BI_RGB bitmap has the layout of the color masks. The masks
    /// are unsigned, because the alpha mask sets the sign bit.
    /// </summary>
    public static class Bgra32Masks
    {
        /// <summary>
        /// The red mask: bits 16 to 23.
        /// </summary>
        public const uint Red = 0x00FF0000;

        /// <summary>
        /// The green mask: bits 8 to 15.
        /// </summary>
        public const uint Green = 0x0000FF00;

        /// <summary>
        /// The blue mask: bits 0 to 7.
        /// </summary>
        public const uint Blue = 0x000000FF;

        /// <summary>
        /// The alpha mask: bits 24 to 31.
        /// </summary>
        public const uint Alpha = 0xFF000000;
    }

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
