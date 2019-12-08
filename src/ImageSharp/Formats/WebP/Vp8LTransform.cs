// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Data associated with a VP8L transformation to reduce the entropy.
    /// </summary>
    internal class Vp8LTransform
    {
        public Vp8LTransform(Vp8LTransformType transformType) => this.TransformType = transformType;

        /// <summary>
        /// Gets or sets the transform type.
        /// </summary>
        public Vp8LTransformType TransformType { get; private set; }

        /// <summary>
        /// Subsampling bits defining transform window.
        /// </summary>
        public int Bits { get; set; }

        /// <summary>
        /// Transform window X index.
        /// </summary>
        public int XSize { get; set; }

        /// <summary>
        /// Transform window Y index.
        /// </summary>
        public int YSize { get; set; }

        /// <summary>
        /// Transform data.
        /// </summary>
        public int[] Data { get; set; }
    }
}
