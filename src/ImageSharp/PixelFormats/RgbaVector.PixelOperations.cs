// <copyright file="RgbaVector.BulkOperations.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System.Numerics;

    /// <content>
    /// Provides optimized overrides for bulk operations.
    /// </content>
    public partial struct RgbaVector
    {
        /// <summary>
        /// <see cref="PixelOperations{TPixel}"/> implementation optimized for <see cref="RgbaVector"/>.
        /// </summary>
        internal class PixelOperations : PixelOperations<RgbaVector>
        {
            /// <inheritdoc />
            internal override unsafe void ToVector4(BufferSpan<RgbaVector> sourceColors, BufferSpan<Vector4> destVectors, int count)
            {
                BufferSpan.Copy(sourceColors.AsBytes(), destVectors.AsBytes(), count * sizeof(Vector4));
            }
        }
    }
}