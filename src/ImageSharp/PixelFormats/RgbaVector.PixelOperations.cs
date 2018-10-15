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
            internal override void PackFromScaledVector4(ReadOnlySpan<Vector4> sourceVectors, Span<RgbaVector> destinationColors, int count)
            {
                GuardSpans(sourceVectors, nameof(sourceVectors), destinationColors, nameof(destinationColors), count);

                MemoryMarshal.Cast<Vector4, RgbaVector>(sourceVectors).Slice(0, count).CopyTo(destinationColors);
            }

            /// <inheritdoc />
            internal override void ToScaledVector4(ReadOnlySpan<RgbaVector> sourceColors, Span<Vector4> destinationVectors, int count)
                => this.ToVector4(sourceColors, destinationVectors, count);

            /// <inheritdoc />
            internal override void ToVector4(ReadOnlySpan<RgbaVector> sourceColors, Span<Vector4> destinationVectors, int count)
            {
                GuardSpans(sourceColors, nameof(sourceColors), destinationVectors, nameof(destinationVectors), count);

                MemoryMarshal.Cast<RgbaVector, Vector4>(sourceColors).Slice(0, count).CopyTo(destinationVectors);
            }
        }
    }
}