// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Contains information about the pixels that make up an images visual data.
    /// </summary>
    public class PixelTypeInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PixelTypeInfo"/> class.
        /// </summary>
        /// <param name="bitsPerPixel">Color depth, in number of bits per pixel.</param>
        internal PixelTypeInfo(int bitsPerPixel)
        {
            this.BitsPerPixel = bitsPerPixel;
        }

        /// <summary>
        /// Gets color depth, in number of bits per pixel.
        /// </summary>
        public int BitsPerPixel { get; }
    }
}
