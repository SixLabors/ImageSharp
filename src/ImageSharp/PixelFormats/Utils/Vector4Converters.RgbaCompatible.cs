using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
            /// and <see cref="PixelOperations{TPixel}.ToScaledVector4"/>
            /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
            /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
            /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void ToVector4<TPixel>(
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

            /// <summary>
            /// Provides an efficient default implementation for <see cref="PixelOperations{TPixel}.FromVector4"/>
            /// and <see cref="PixelOperations{TPixel}.FromScaledVector4"/>
            /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
            /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
            /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void FromVector4<TPixel>(
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