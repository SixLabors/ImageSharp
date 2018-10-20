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

                if (count < 128 || !SimdUtils.IsAvx2CompatibleArchitecture)
                {
                    // Doesn't worth to bother with SIMD:
                    ToVector4Common(sourceColors, destinationVectors, count);
                    return;
                }

                if (SimdUtils.ExtendedIntrinsics.IsAvailable)
                {
                    ConvertToVector4UsingExtendedIntrinsics(sourceColors, destinationVectors, count);
                }
                else
                {
                    ConvertToVector4UsingStandardIntrinsics(sourceColors, destinationVectors, count);
                }
            }

            /// <inheritdoc />
            internal override void PackFromVector4(ReadOnlySpan<Vector4> sourceVectors, Span<Rgba32> destinationColors, int count)
            {
                GuardSpans(sourceVectors, nameof(sourceVectors), destinationColors, nameof(destinationColors), count);

                if (count < 128 || !SimdUtils.IsAvx2CompatibleArchitecture)
                {
                    PackFromVector4Common(sourceVectors, destinationColors, count);
                    return;
                }

                if (SimdUtils.ExtendedIntrinsics.IsAvailable)
                {
                    ConvertFromVector4ExtendedIntrinsics(sourceVectors, destinationColors, count);
                }
                else
                {
                    ConvertFromVector4StandardIntrinsics(sourceVectors, destinationColors, count);
                }
            }

            /// <inheritdoc />
            internal override void ToScaledVector4(ReadOnlySpan<Rgba32> sourceColors, Span<Vector4> destinationVectors, int count)
            {
                this.ToVector4(sourceColors, destinationVectors, count);
            }

            /// <inheritdoc />
            internal override void PackFromScaledVector4(ReadOnlySpan<Vector4> sourceVectors, Span<Rgba32> destinationColors, int count)
            {
                this.PackFromVector4(sourceVectors, destinationColors, count);
            }

            /// <inheritdoc />
            internal override void PackFromRgba32(ReadOnlySpan<Rgba32> source, Span<Rgba32> destPixels, int count)
            {
                GuardSpans(source, nameof(source), destPixels, nameof(destPixels), count);

                source.Slice(0, count).CopyTo(destPixels);
            }

            /// <inheritdoc />
            internal override void ToRgba32(ReadOnlySpan<Rgba32> sourcePixels, Span<Rgba32> dest, int count)
            {
                GuardSpans(sourcePixels, nameof(sourcePixels), dest, nameof(dest), count);

                sourcePixels.Slice(0, count).CopyTo(dest);
            }

            private static void ConvertToVector4UsingExtendedIntrinsics(
                ReadOnlySpan<Rgba32> sourceColors,
                Span<Vector4> destinationVectors,
                int count)
            {
                int remainder = count % 8;
                int alignedCount = count - remainder;

                if (alignedCount > 0)
                {
                    ReadOnlySpan<byte> rawSrc = MemoryMarshal.Cast<Rgba32, byte>(sourceColors);
                    Span<float> rawDest = MemoryMarshal.Cast<Vector4, float>(destinationVectors.Slice(0, alignedCount));

                    SimdUtils.ExtendedIntrinsics.BulkConvertByteToNormalizedFloat(rawSrc, rawDest);
                }

                if (remainder > 0)
                {
                    ToVector4Common(sourceColors.Slice(alignedCount), destinationVectors.Slice(alignedCount), remainder);
                }
            }

            private static void ConvertToVector4UsingStandardIntrinsics(
                ReadOnlySpan<Rgba32> sourceColors,
                Span<Vector4> destinationVectors,
                int count)
            {
                int remainder = count % 2;
                int alignedCount = count - remainder;

                if (alignedCount > 0)
                {
                    ReadOnlySpan<byte> rawSrc = MemoryMarshal.Cast<Rgba32, byte>(sourceColors);
                    Span<float> rawDest = MemoryMarshal.Cast<Vector4, float>(destinationVectors.Slice(0, alignedCount));

                    SimdUtils.BulkConvertByteToNormalizedFloat(rawSrc, rawDest);
                }

                if (remainder > 0)
                {
                    // actually: remainder == 1
                    int lastIdx = count - 1;
                    destinationVectors[lastIdx] = sourceColors[lastIdx].ToVector4();
                }
            }

            private static void ConvertFromVector4ExtendedIntrinsics(ReadOnlySpan<Vector4> sourceVectors, Span<Rgba32> destinationColors, int count)
            {
                int remainder = count % 8;
                int alignedCount = count - remainder;

                if (alignedCount > 0)
                {
                    ReadOnlySpan<float> rawSrc = MemoryMarshal.Cast<Vector4, float>(sourceVectors);
                    Span<byte> rawDest = MemoryMarshal.Cast<Rgba32, byte>(destinationColors.Slice(0, alignedCount));

                    SimdUtils.ExtendedIntrinsics.BulkConvertNormalizedFloatToByteClampOverflows(rawSrc, rawDest);
                }

                if (remainder > 0)
                {
                    PackFromVector4Common(sourceVectors.Slice(alignedCount), destinationColors.Slice(alignedCount), remainder);
                }
            }

            private static void ConvertFromVector4StandardIntrinsics(ReadOnlySpan<Vector4> sourceVectors, Span<Rgba32> destinationColors, int count)
            {
                int remainder = count % 2;
                int alignedCount = count - remainder;

                if (alignedCount > 0)
                {
                    ReadOnlySpan<float> rawSrc = MemoryMarshal.Cast<Vector4, float>(sourceVectors.Slice(0, alignedCount));
                    Span<byte> rawDest = MemoryMarshal.Cast<Rgba32, byte>(destinationColors);

                    SimdUtils.BulkConvertNormalizedFloatToByteClampOverflows(rawSrc, rawDest);
                }

                if (remainder > 0)
                {
                    // actually: remainder == 1
                    int lastIdx = count - 1;
                    destinationColors[lastIdx].PackFromVector4(sourceVectors[lastIdx]);
                }
            }
        }
    }
}