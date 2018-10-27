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
            internal override void ToVector4(
                Configuration configuration,
                ReadOnlySpan<Rgba32> sourcePixels,
                Span<Vector4> destVectors)
            {
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destVectors, nameof(destVectors));

                destVectors = destVectors.Slice(0, sourcePixels.Length);

                SimdUtils.BulkConvertByteToNormalizedFloat(
                    MemoryMarshal.Cast<Rgba32, byte>(sourcePixels),
                    MemoryMarshal.Cast<Vector4, float>(destVectors));
            }

            /// <inheritdoc />
            internal override void FromVector4(
                Configuration configuration,
                ReadOnlySpan<Vector4> sourceVectors,
                Span<Rgba32> destPixels)
            {
                Guard.DestinationShouldNotBeTooShort(sourceVectors, destPixels, nameof(destPixels));

                destPixels = destPixels.Slice(0, sourceVectors.Length);

                SimdUtils.BulkConvertNormalizedFloatToByteClampOverflows(
                    MemoryMarshal.Cast<Vector4, float>(sourceVectors),
                    MemoryMarshal.Cast<Rgba32, byte>(destPixels));
            }

            /// <inheritdoc />
            internal override void ToScaledVector4(
                Configuration configuration,
                ReadOnlySpan<Rgba32> sourceColors,
                Span<Vector4> destinationVectors)
            {
                this.ToVector4(configuration, sourceColors, destinationVectors);
            }

            /// <inheritdoc />
            internal override void FromScaledVector4(
                Configuration configuration,
                ReadOnlySpan<Vector4> sourceVectors,
                Span<Rgba32> destinationColors)
            {
                this.FromVector4(configuration, sourceVectors, destinationColors);
            }
        }
    }
}