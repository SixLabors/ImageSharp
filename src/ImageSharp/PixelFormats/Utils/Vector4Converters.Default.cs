using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats.Utils
{
    /// <summary>
    /// Helper class for (bulk) conversion of <see cref="Vector4"/> buffers to/from other buffer types.
    /// </summary>
    internal static partial class Vector4Converters
    {
        /// <summary>
        /// Provides default implementations for batched to/from <see cref="Vector4"/> conversion.
        /// WARNING: The methods are operating without bounds checking and input validation!
        /// Input validation is the responsibility of the caller!
        /// </summary>
        public static class Default
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void DangerousFromVector4<TPixel>(
                ReadOnlySpan<Vector4> sourceVectors,
                Span<TPixel> destPixels)
                where TPixel : struct, IPixel<TPixel>
            {
                ref Vector4 sourceRef = ref MemoryMarshal.GetReference(sourceVectors);
                ref TPixel destRef = ref MemoryMarshal.GetReference(destPixels);

                for (int i = 0; i < sourceVectors.Length; i++)
                {
                    ref Vector4 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                    dp.FromVector4(sp);
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void DangerousToVector4<TPixel>(
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors)
                where TPixel : struct, IPixel<TPixel>
            {
                ref TPixel sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
                ref Vector4 destRef = ref MemoryMarshal.GetReference(destVectors);

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Vector4 dp = ref Unsafe.Add(ref destRef, i);
                    dp = sp.ToVector4();
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void DangerousFromScaledVector4<TPixel>(
                ReadOnlySpan<Vector4> sourceVectors,
                Span<TPixel> destinationColors)
                where TPixel : struct, IPixel<TPixel>
            {
                ref Vector4 sourceRef = ref MemoryMarshal.GetReference(sourceVectors);
                ref TPixel destRef = ref MemoryMarshal.GetReference(destinationColors);

                for (int i = 0; i < sourceVectors.Length; i++)
                {
                    ref Vector4 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                    dp.FromScaledVector4(sp);
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void DangerousToScaledVector4<TPixel>(
                ReadOnlySpan<TPixel> sourceColors,
                Span<Vector4> destinationVectors)
                where TPixel : struct, IPixel<TPixel>
            {
                ref TPixel sourceRef = ref MemoryMarshal.GetReference(sourceColors);
                ref Vector4 destRef = ref MemoryMarshal.GetReference(destinationVectors);

                for (int i = 0; i < sourceColors.Length; i++)
                {
                    ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Vector4 dp = ref Unsafe.Add(ref destRef, i);
                    dp = sp.ToScaledVector4();
                }
            }
        }
    }
}