// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorSpaces.Companding;

namespace SixLabors.ImageSharp.PixelFormats.Utils
{
    /// <content>
    /// Contains <see cref="RgbaCompatible"/>
    /// </content>
    internal static partial class Vector4Converters
    {
        /// <summary>
        /// Provides efficient implementations for batched to/from <see cref="Vector4"/> conversion.
        /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
        /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
        /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
        /// </summary>
        public static class RgbaCompatible
        {
            /// <summary>
            /// It's not worth to bother the transitive pixel conversion method below this limit.
            /// The value depends on the actual gain brought by the SIMD characteristics of the executing CPU and JIT.
            /// </summary>
            private static readonly int Vector4ConversionThreshold = CalculateVector4ConversionThreshold();

            /// <summary>
            /// Provides an efficient default implementation for <see cref="PixelOperations{TPixel}.ToVector4"/>
            /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
            /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
            /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void ToVector4<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors)
                where TPixel : struct, IPixel<TPixel>
                => ToVector4Impl(configuration, pixelOperations, sourcePixels, destVectors, false);

            /// <summary>
            /// Provides an efficient default implementation for <see cref="PixelOperations{TPixel}.FromVector4"/>
            /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
            /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
            /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void FromVector4<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                ReadOnlySpan<Vector4> sourceVectors,
                Span<TPixel> destPixels)
                where TPixel : struct, IPixel<TPixel>
                => FromVector4Impl(configuration, pixelOperations, sourceVectors, destPixels, false);

            /// <summary>
            /// Provides an efficient default implementation for <see cref="PixelOperations{TPixel}.ToScaledVector4"/>
            /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
            /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
            /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void ToScaledVector4<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors)
                where TPixel : struct, IPixel<TPixel>
                => ToVector4Impl(configuration, pixelOperations, sourcePixels, destVectors, true);

            /// <summary>
            /// Provides an efficient default implementation for <see cref="PixelOperations{TPixel}.FromScaledVector4"/>
            /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
            /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
            /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void FromScaledVector4<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                ReadOnlySpan<Vector4> sourceVectors,
                Span<TPixel> destPixels)
                where TPixel : struct, IPixel<TPixel>
                => FromVector4Impl(configuration, pixelOperations, sourceVectors, destPixels, true);

            /// <summary>
            /// Provides an efficient default implementation for converting pixels into alpha premultiplied destination vectors.
            /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
            /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
            /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void ToPremultipliedVector4<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors)
                where TPixel : struct, IPixel<TPixel>
            {
                ToVector4(configuration, pixelOperations, sourcePixels, destVectors);

                // TODO: Investigate optimized 1-pass approach.
                Vector4Utils.Premultiply(destVectors);
            }

            /// <summary>
            /// Provides an efficient default implementation for converting pixels into alpha premultiplied, scaled destination vectors.
            /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
            /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
            /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void ToPremultipliedScaledVector4<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors)
                where TPixel : struct, IPixel<TPixel>
            {
                ToScaledVector4(configuration, pixelOperations, sourcePixels, destVectors);

                // TODO: Investigate optimized 1-pass approach.
                Vector4Utils.Premultiply(destVectors);
            }

            /// <summary>
            /// Provides an efficient default implementation for converting pixels into companded, scaled destination vectors.
            /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
            /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
            /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void ToCompandedScaledVector4<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors)
                where TPixel : struct, IPixel<TPixel>
            {
                ToScaledVector4(configuration, pixelOperations, sourcePixels, destVectors);

                // TODO: Investigate optimized 1-pass approach.
                SRgbCompanding.Expand(destVectors);
            }

            /// <summary>
            /// Provides an efficient default implementation for converting pixels into alpha premultiplied, scaled destination vectors.
            /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
            /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
            /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void ToCompandedPremultipliedScaledVector4<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors)
                where TPixel : struct, IPixel<TPixel>
            {
                ToCompandedScaledVector4(configuration, pixelOperations, sourcePixels, destVectors);

                // TODO: Investigate optimized 1-pass approach.
                Vector4Utils.Premultiply(destVectors);
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private static void ToVector4Impl<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors,
                bool scaled)
                where TPixel : struct, IPixel<TPixel>
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destVectors, nameof(destVectors));

                int count = sourcePixels.Length;

                // Not worth for small buffers:
                if (count < Vector4ConversionThreshold)
                {
                    ToVector4Fallback(sourcePixels, destVectors, scaled);

                    return;
                }

                // Using the last quarter of 'destVectors' as a temporary buffer to avoid allocation:
                int countWithoutLastItem = count - 1;
                ReadOnlySpan<TPixel> reducedSource = sourcePixels.Slice(0, countWithoutLastItem);
                Span<Rgba32> lastQuarterOfDestBuffer = MemoryMarshal.Cast<Vector4, Rgba32>(destVectors).Slice((3 * count) + 1, countWithoutLastItem);
                pixelOperations.ToRgba32(configuration, reducedSource, lastQuarterOfDestBuffer);

                // 'destVectors' and 'lastQuarterOfDestBuffer' are overlapping buffers,
                // but we are always reading/writing at different positions:
                SimdUtils.BulkConvertByteToNormalizedFloat(
                    MemoryMarshal.Cast<Rgba32, byte>(lastQuarterOfDestBuffer),
                    MemoryMarshal.Cast<Vector4, float>(destVectors.Slice(0, countWithoutLastItem)));

                destVectors[countWithoutLastItem] = sourcePixels[countWithoutLastItem].ToVector4();
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void FromVector4Impl<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                ReadOnlySpan<Vector4> sourceVectors,
                Span<TPixel> destPixels,
                bool scaled)
                where TPixel : struct, IPixel<TPixel>
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourceVectors, destPixels, nameof(destPixels));

                int count = sourceVectors.Length;

                // Not worth for small buffers:
                if (count < Vector4ConversionThreshold)
                {
                    FromVector4Fallback(sourceVectors, destPixels, scaled);

                    return;
                }

                // For the opposite direction it's not easy to implement the trick used in RunRgba32CompatibleToVector4Conversion,
                // so let's allocate a temporary buffer as usually:
                using (IMemoryOwner<Rgba32> tempBuffer = configuration.MemoryAllocator.Allocate<Rgba32>(count))
                {
                    Span<Rgba32> tempSpan = tempBuffer.Memory.Span;

                    SimdUtils.BulkConvertNormalizedFloatToByteClampOverflows(
                        MemoryMarshal.Cast<Vector4, float>(sourceVectors),
                        MemoryMarshal.Cast<Rgba32, byte>(tempSpan));

                    pixelOperations.FromRgba32(configuration, tempSpan, destPixels);
                }
            }

            [MethodImpl(InliningOptions.ColdPath)]
            private static void ToVector4Fallback<TPixel>(ReadOnlySpan<TPixel> sourcePixels, Span<Vector4> destVectors, bool scaled)
                where TPixel : struct, IPixel<TPixel>
            {
                if (scaled)
                {
                    Default.DangerousToScaledVector4(sourcePixels, destVectors);
                }
                else
                {
                    Default.DangerousToVector4(sourcePixels, destVectors);
                }
            }

            [MethodImpl(InliningOptions.ColdPath)]
            private static void FromVector4Fallback<TPixel>(ReadOnlySpan<Vector4> sourceVectors, Span<TPixel> destPixels, bool scaled)
                where TPixel : struct, IPixel<TPixel>
            {
                if (scaled)
                {
                    Default.DangerousFromScaledVector4(sourceVectors, destPixels);
                }
                else
                {
                    Default.DangerousFromVector4(sourceVectors, destPixels);
                }
            }

            private static int CalculateVector4ConversionThreshold()
            {
                if (!Vector.IsHardwareAccelerated)
                {
                    return int.MaxValue;
                }

                return SimdUtils.ExtendedIntrinsics.IsAvailable && SimdUtils.IsAvx2CompatibleArchitecture ? 256 : 128;
            }
        }
    }
}