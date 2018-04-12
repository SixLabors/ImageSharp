// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.InteropServices;

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
            internal override unsafe void ToVector4(ReadOnlySpan<RgbaVector> sourceColors, Span<Vector4> destVectors, int count)
            {
                GuardSpans(sourceColors, nameof(sourceColors), destVectors, nameof(destVectors), count);

                MemoryMarshal.Cast<RgbaVector, Vector4>(sourceColors).Slice(0, count).CopyTo(destVectors);
            }
        }
    }
}