// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal class Vp8PictureHeader
    {
        /// <summary>
        /// Gets or sets the width of the image.
        /// </summary>
        public uint Width { get; set; }

        /// <summary>
        /// Gets or sets the Height of the image.
        /// </summary>
        public uint Height { get; set; }

        /// <summary>
        /// Gets or sets the horizontal scale.
        /// </summary>
        public sbyte XScale { get; set; }

        /// <summary>
        /// Gets or sets the vertical scale.
        /// </summary>
        public sbyte YScale { get; set; }

        /// <summary>
        /// Gets or sets the colorspace. 0 = YCbCr.
        /// </summary>
        public sbyte ColorSpace { get; set; }

        /// <summary>
        /// Gets or sets the clamp type.
        /// </summary>
        public sbyte ClampType { get; set; }
    }
}
