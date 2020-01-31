// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Holds information for decoding a lossless webp image.
    /// </summary>
    internal class Vp8LDecoder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LDecoder"/> class.
        /// </summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        public Vp8LDecoder(int width, int height)
        {
            this.Width = width;
            this.Height = height;
            this.Metadata = new Vp8LMetadata();
        }

        /// <summary>
        /// Gets or sets the width of the image to decode.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the image to decode.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the necessary VP8L metadata (like huffman tables) to decode the image.
        /// </summary>
        public Vp8LMetadata Metadata { get; set; }

        /// <summary>
        /// Gets or sets the transformations which needs to be reversed.
        /// </summary>
        public List<Vp8LTransform> Transforms { get; set; }
    }
}
