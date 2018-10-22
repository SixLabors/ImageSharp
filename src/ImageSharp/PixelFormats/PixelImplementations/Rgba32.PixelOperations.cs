// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <content>
    /// Provides optimized overrides for bulk operations.
    /// </content>
    public partial struct Rgba32
    {
        /// <summary>
        /// <see cref="PixelOperations{TPixel}"/> implementation optimized for <see cref="Rgba32"/>.
        /// </summary>
        internal partial class PixelOperations : PixelOperations<Rgba32>
        {
            /// <inheritdoc />
            internal override void ToVector4(ReadOnlySpan<Rgba32> sourceColors, Span<Vector4> destinationVectors, int count)
            {
                Guard.MustBeSizedAtLeast(sourceColors, count, nameof(sourceColors));
                Guard.MustBeSizedAtLeast(destinationVectors, count, nameof(destinationVectors));

                sourceColors = sourceColors.Slice(0, count);
                destinationVectors = destinationVectors.Slice(0, count);

                SimdUtils.BulkConvertByteToNormalizedFloat(
                    MemoryMarshal.Cast<Rgba32, byte>(sourceColors),
                    MemoryMarshal.Cast<Vector4, float>(destinationVectors));
            }

            /// <inheritdoc />
            internal override void FromVector4(ReadOnlySpan<Vector4> sourceVectors, Span<Rgba32> destinationColors, int count)
            {
                GuardSpans(sourceVectors, nameof(sourceVectors), destinationColors, nameof(destinationColors), count);

                sourceVectors = sourceVectors.Slice(0, count);
                destinationColors = destinationColors.Slice(0, count);

                SimdUtils.BulkConvertNormalizedFloatToByteClampOverflows(
                    MemoryMarshal.Cast<Vector4, float>(sourceVectors),
                    MemoryMarshal.Cast<Rgba32, byte>(destinationColors));
            }

            /// <inheritdoc />
            internal override void ToScaledVector4(ReadOnlySpan<Rgba32> sourceColors, Span<Vector4> destinationVectors, int count)
            {
                this.ToVector4(sourceColors, destinationVectors, count);
            }

            /// <inheritdoc />
            internal override void FromScaledVector4(ReadOnlySpan<Vector4> sourceVectors, Span<Rgba32> destinationColors, int count)
            {
                this.FromVector4(sourceVectors, destinationColors, count);
            }
        }
    }
}