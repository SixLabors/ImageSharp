// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Data associated with a VP8L transformation to reduce the entropy.
    /// </summary>
    internal class Vp8LTransform
    {
        public Vp8LTransform(Vp8LTransformType transformType, int xSize, int ySize)
        {
            this.TransformType = transformType;
            this.XSize = xSize;
            this.YSize = ySize;
        }

        /// <summary>
        /// Gets the transform type.
        /// </summary>
        public Vp8LTransformType TransformType { get; }

        /// <summary>
        /// Subsampling bits defining transform window.
        /// </summary>
        public int Bits { get; set; }

        /// <summary>
        /// Gets or sets the transform window X index.
        /// </summary>
        public int XSize { get; set; }

        /// <summary>
        /// Gets the transform window Y index.
        /// </summary>
        public int YSize { get; }

        /// <summary>
        /// Gets or sets the transform data.
        /// </summary>
        public uint[] Data { get; set; }
    }
}
