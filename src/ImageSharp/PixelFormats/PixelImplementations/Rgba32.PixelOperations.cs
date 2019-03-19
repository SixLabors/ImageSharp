// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorSpaces.Companding;

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
            internal override void ToPremultipliedVector4(
                Configuration configuration,
                ReadOnlySpan<Rgba32> sourcePixels,
                Span<Vector4> destVectors)
            {
                this.ToVector4(configuration, sourcePixels, destVectors);

                // TODO: Investigate optimized 1-pass approach.
                Vector4Utils.Premultiply(destVectors);
            }

            /// <inheritdoc />
            internal override void FromPremultipliedVector4(
                Configuration configuration,
                Span<Vector4> sourceVectors,
                Span<Rgba32> destPixels)
            {
                // TODO: Investigate optimized 1-pass approach.
                Vector4Utils.UnPremultiply(sourceVectors);
                this.FromVector4(configuration, sourceVectors, destPixels);
            }

            /// <inheritdoc />
            internal override void ToScaledVector4(
                Configuration configuration,
                ReadOnlySpan<Rgba32> sourcePixels,
                Span<Vector4> destVectors) => this.ToVector4(configuration, sourcePixels, destVectors);

            /// <inheritdoc />
            internal override void FromScaledVector4(
                Configuration configuration,
                ReadOnlySpan<Vector4> sourceVectors,
                Span<Rgba32> destPixels) => this.FromVector4(configuration, sourceVectors, destPixels);

            /// <inheritdoc />
            internal override void ToPremultipliedScaledVector4(
                Configuration configuration,
                ReadOnlySpan<Rgba32> sourcePixels,
                Span<Vector4> destVectors) => this.ToPremultipliedVector4(configuration, sourcePixels, destVectors);

            /// <inheritdoc />q
            internal override void FromPremultipliedScaledVector4(
                Configuration configuration,
                Span<Vector4> sourceVectors,
                Span<Rgba32> destPixels) => this.FromPremultipliedVector4(configuration, sourceVectors, destPixels);

            /// <inheritdoc />
            internal override void ToCompandedVector4(
                Configuration configuration,
                ReadOnlySpan<Rgba32> sourcePixels,
                Span<Vector4> destVectors)
            {
                this.ToVector4(configuration, sourcePixels, destVectors);

                // TODO: Investigate optimized 1-pass approach.
                SRgbCompanding.Expand(destVectors);
            }

            /// <inheritdoc />
            internal override void FromCompandedVector4(
                Configuration configuration,
                Span<Vector4> sourceVectors,
                Span<Rgba32> destPixels)
            {
                // TODO: Investigate optimized 1-pass approach.
                SRgbCompanding.Compress(sourceVectors);
                this.FromVector4(configuration, sourceVectors, destPixels);
            }

            /// <inheritdoc />
            internal override void ToCompandedScaledVector4(
                Configuration configuration,
                ReadOnlySpan<Rgba32> sourcePixels,
                Span<Vector4> destVectors) => this.ToCompandedVector4(configuration, sourcePixels, destVectors);

            /// <inheritdoc />
            internal override void FromCompandedScaledVector4(
                Configuration configuration,
                Span<Vector4> sourceVectors,
                Span<Rgba32> destPixels) => this.FromCompandedVector4(configuration, sourceVectors, destPixels);

            /// <inheritdoc />
            internal override void ToCompandedPremultipliedVector4(
                Configuration configuration,
                ReadOnlySpan<Rgba32> sourcePixels,
                Span<Vector4> destVectors)
            {
                this.ToVector4(configuration, sourcePixels, destVectors);

                // TODO: Investigate optimized 1-pass approach.
                SRgbCompanding.Expand(destVectors);
                Vector4Utils.Premultiply(destVectors);
            }

            /// <inheritdoc />
            internal override void FromCompandedPremultipliedVector4(
                Configuration configuration,
                Span<Vector4> sourceVectors,
                Span<Rgba32> destPixels)
            {
                // TODO: Investigate optimized 1-pass approach.
                Vector4Utils.UnPremultiply(sourceVectors);
                SRgbCompanding.Compress(sourceVectors);
                this.FromVector4(configuration, sourceVectors, destPixels);
            }

            /// <inheritdoc />
            internal override void ToCompandedPremultipliedScaledVector4(
                Configuration configuration,
                ReadOnlySpan<Rgba32> sourcePixels,
                Span<Vector4> destVectors) => this.ToCompandedPremultipliedVector4(configuration, sourcePixels, destVectors);

            /// <inheritdoc />
            internal override void FromCompandedPremultipliedScaledVector4(
                Configuration configuration,
                Span<Vector4> sourceVectors,
                Span<Rgba32> destPixels) => this.FromCompandedPremultipliedVector4(configuration, sourceVectors, destPixels);
        }
    }
}