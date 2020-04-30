// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Identifies the colorspace of a Jpeg image.
    /// </summary>
    internal enum JpegColorSpace
    {
        Undefined = 0,

        Grayscale,

        Ycck,

        Cmyk,

        RGB,

        YCbCr
    }
}