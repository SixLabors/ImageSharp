// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.PixelFormats
{
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
            internal override unsafe void ToVector4(Span<RgbaVector> sourceColors, Span<Vector4> destVectors, int count)
            {
                GuardSpans(sourceColors, nameof(sourceColors), destVectors, nameof(destVectors), count);

                SpanHelper.Copy(sourceColors.NonPortableCast<RgbaVector, Vector4>(), destVectors, count);
            }
        }
    }
}