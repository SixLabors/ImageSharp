// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Image decoder options for decoding Windows bitmap streams.
    /// </summary>
    internal interface IBmpDecoderOptions
    {
        /// <summary>
        /// Gets the value indicating how to deal with skipped pixels, which can occur during decoding run length encoded bitmaps.
        /// </summary>
        RleSkippedPixelHandling RleSkippedPixelHandling { get; }
    }
}