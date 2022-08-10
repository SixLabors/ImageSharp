// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    internal ref struct Vp8Io
    {
        /// <summary>
        /// Gets or sets the picture width in pixels (invariable).
        /// The actual area passed to put() is stored in <see cref="MbW"/> /> field.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the picture height in pixels (invariable).
        /// The actual area passed to put() is stored in <see cref="MbH"/> /> field.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the y-position of the current macroblock.
        /// </summary>
        public int MbY { get; set; }

        /// <summary>
        /// Gets or sets number of columns in the sample.
        /// </summary>
        public int MbW { get; set; }

        /// <summary>
        /// Gets or sets number of rows in the sample.
        /// </summary>
        public int MbH { get; set; }

        /// <summary>
        /// Gets or sets the luma component.
        /// </summary>
        public Span<byte> Y { get; set; }

        /// <summary>
        /// Gets or sets the U chroma component.
        /// </summary>
        public Span<byte> U { get; set; }

        /// <summary>
        /// Gets or sets the V chroma component.
        /// </summary>
        public Span<byte> V { get; set; }

        /// <summary>
        /// Gets or sets the row stride for luma.
        /// </summary>
        public int YStride { get; set; }

        /// <summary>
        /// Gets or sets the row stride for chroma.
        /// </summary>
        public int UvStride { get; set; }

        public bool UseScaling { get; set; }

        public int ScaledWidth { get; set; }

        public int ScaledHeight { get; set; }
    }
}
