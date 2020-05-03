// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal enum AlphaCompressionMethod
    {
        /// <summary>
        /// No compression.
        /// </summary>
        NoCompression = 0,

        /// <summary>
        /// Compressed using the WebP lossless format.
        /// </summary>
        WebPLosslessCompression = 1
    }
}
