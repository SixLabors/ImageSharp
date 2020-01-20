// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.WebP
{
    // from
    public ref struct Vp8Io
    {
        /// <summary>
        /// Picture Width in pixels (invariable).
        /// Original, uncropped dimensions.
        /// The actual area passed to put() is stored in <see cref="MbW"/> /> field.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Picture Width in pixels (invariable).
        /// Original, uncropped dimensions.
        /// The actual area passed to put() is stored in <see cref="MbH"/> /> field.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Position of the current Rows (in pixels)
        /// </summary>
        public int MbY { get; set; }

        /// <summary>
        /// number of columns in the sample
        /// </summary>
        public int MbW { get; set; }

        /// <summary>
        /// Number of Rows in the sample
        /// </summary>
        public int MbH { get; set; }

        /// <summary>
        /// Rows to copy (in YUV format)
        /// </summary>
        private Span<byte> Y { get; set; }

        /// <summary>
        /// Rows to copy (in YUV format)
        /// </summary>
        private Span<byte> U { get; set; }

        /// <summary>
        /// Rows to copy (in YUV format)
        /// </summary>
        private Span<byte> V { get; set; }

        /// <summary>
        /// Row stride for luma
        /// </summary>
        public int YStride { get; set; }

        /// <summary>
        /// Row stride for chroma
        /// </summary>
        public int UvStride { get; set; }

        /// <summary>
        /// User data
        /// </summary>
        private object Opaque { get; set; }
    }
}
