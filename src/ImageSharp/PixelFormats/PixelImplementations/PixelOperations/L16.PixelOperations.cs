// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct L16
{
    /// <summary>
    /// Provides optimized overrides for bulk operations.
    /// </summary>
    internal partial class PixelOperations : PixelOperations<L16>
    {
        // Alpha is implicitly one, so both outward representations already contain associated color components.

        /// <inheritdoc />
        protected override void ToUnassociatedVector4(
            Configuration configuration,
            ReadOnlySpan<L16> source,
            Span<Vector4> destination)
            => ConvertToVector4(source, destination);

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(
            Configuration configuration,
            ReadOnlySpan<L16> source,
            Span<Vector4> destination)
            => ConvertToVector4(source, destination);

        /// <inheritdoc />
        protected override void ToAssociatedVector4(
            Configuration configuration,
            ReadOnlySpan<L16> source,
            Span<Vector4> destination)
            => this.ToUnassociatedVector4(configuration, source, destination);

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(
            Configuration configuration,
            ReadOnlySpan<L16> source,
            Span<Vector4> destination)
            => this.ToUnassociatedScaledVector4(configuration, source, destination);

        /// <summary>
        /// Expands packed luminance samples into normalized RGB vectors with opaque alpha.
        /// </summary>
        /// <param name="source">The packed luminance samples.</param>
        /// <param name="destination">The destination vectors.</param>
        private static void ConvertToVector4(ReadOnlySpan<L16> source, Span<Vector4> destination)
        {
            ref ushort sourceBase = ref Unsafe.As<L16, ushort>(ref MemoryMarshal.GetReference(source));
            ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
            int length = source.Length;
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<float> maximum = Vector512.Create((float)ushort.MaxValue);
                int samplesPerVector = Vector512<ushort>.Count;
                int oneVectorFromEnd = length - samplesPerVector;

                for (; i <= oneVectorFromEnd; i += samplesPerVector)
                {
                    Vector512<ushort> packed = Vector512.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector512<uint> lower, Vector512<uint> upper) = Vector512.Widen(packed);

                    StoreLuminanceVectors(Vector512.ConvertToSingle(lower.AsInt32()) / maximum, ref Unsafe.Add(ref destinationBase, (uint)i));
                    StoreLuminanceVectors(Vector512.ConvertToSingle(upper.AsInt32()) / maximum, ref Unsafe.Add(ref destinationBase, (uint)(i + (samplesPerVector / 2))));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<float> maximum = Vector256.Create((float)ushort.MaxValue);
                int samplesPerVector = Vector256<ushort>.Count;
                int oneVectorFromEnd = length - samplesPerVector;

                for (; i <= oneVectorFromEnd; i += samplesPerVector)
                {
                    Vector256<ushort> packed = Vector256.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector256<uint> lower, Vector256<uint> upper) = Vector256.Widen(packed);

                    StoreLuminanceVectors(Vector256.ConvertToSingle(lower.AsInt32()) / maximum, ref Unsafe.Add(ref destinationBase, (uint)i));
                    StoreLuminanceVectors(Vector256.ConvertToSingle(upper.AsInt32()) / maximum, ref Unsafe.Add(ref destinationBase, (uint)(i + (samplesPerVector / 2))));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<float> maximum = Vector128.Create((float)ushort.MaxValue);
                int samplesPerVector = Vector128<ushort>.Count;
                int oneVectorFromEnd = length - samplesPerVector;

                for (; i <= oneVectorFromEnd; i += samplesPerVector)
                {
                    Vector128<ushort> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                    (Vector128<uint> lower, Vector128<uint> upper) = Vector128.Widen(packed);

                    StoreLuminanceVectors(Vector128.ConvertToSingle(lower.AsInt32()) / maximum, ref Unsafe.Add(ref destinationBase, (uint)i));
                    StoreLuminanceVectors(Vector128.ConvertToSingle(upper.AsInt32()) / maximum, ref Unsafe.Add(ref destinationBase, (uint)(i + (samplesPerVector / 2))));
                }
            }

            for (; i < length; i++)
            {
                Unsafe.Add(ref destinationBase, (uint)i) = Unsafe.As<ushort, L16>(ref Unsafe.Add(ref sourceBase, (uint)i)).ToVector4();
            }
        }

        /// <summary>
        /// Replicates sixteen normalized luminance samples into sixteen RGB vectors with opaque alpha.
        /// </summary>
        /// <param name="source">The normalized luminance samples.</param>
        /// <param name="destination">The first destination vector.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreLuminanceVectors(Vector512<float> source, ref Vector4 destination)
        {
            Vector512<int> rgbMask = Vector512.Create(-1, -1, -1, 0, -1, -1, -1, 0, -1, -1, -1, 0, -1, -1, -1, 0);
            Vector512<float> opaqueAlpha = Vector512.Create(0F, 0F, 0F, 1F, 0F, 0F, 0F, 1F, 0F, 0F, 0F, 1F, 0F, 0F, 0F, 1F);
            Vector512<int> indices0 = Vector512.Create(0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3);
            Vector512<int> indices1 = Vector512.Create(4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7);
            Vector512<int> indices2 = Vector512.Create(8, 8, 8, 8, 9, 9, 9, 9, 10, 10, 10, 10, 11, 11, 11, 11);
            Vector512<int> indices3 = Vector512.Create(12, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 14, 15, 15, 15, 15);
            ref Vector512<float> destinationBase = ref Unsafe.As<Vector4, Vector512<float>>(ref destination);

            // Native indexed shuffles expand four luminance values per store. Clearing every fourth lane before
            // inserting one preserves the implicit opaque alpha without scalar lane extraction.
            destinationBase = (Vector512.ShuffleNative(source, indices0) & rgbMask.AsSingle()) | opaqueAlpha;
            Unsafe.Add(ref destinationBase, 1) = (Vector512.ShuffleNative(source, indices1) & rgbMask.AsSingle()) | opaqueAlpha;
            Unsafe.Add(ref destinationBase, 2) = (Vector512.ShuffleNative(source, indices2) & rgbMask.AsSingle()) | opaqueAlpha;
            Unsafe.Add(ref destinationBase, 3) = (Vector512.ShuffleNative(source, indices3) & rgbMask.AsSingle()) | opaqueAlpha;
        }

        /// <summary>
        /// Replicates eight normalized luminance samples into eight RGB vectors with opaque alpha.
        /// </summary>
        /// <param name="source">The normalized luminance samples.</param>
        /// <param name="destination">The first destination vector.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreLuminanceVectors(Vector256<float> source, ref Vector4 destination)
        {
            Vector256<int> rgbMask = Vector256.Create(-1, -1, -1, 0, -1, -1, -1, 0);
            Vector256<float> opaqueAlpha = Vector256.Create(0F, 0F, 0F, 1F, 0F, 0F, 0F, 1F);
            Vector256<int> indices0 = Vector256.Create(0, 0, 0, 0, 1, 1, 1, 1);
            Vector256<int> indices1 = Vector256.Create(2, 2, 2, 2, 3, 3, 3, 3);
            Vector256<int> indices2 = Vector256.Create(4, 4, 4, 4, 5, 5, 5, 5);
            Vector256<int> indices3 = Vector256.Create(6, 6, 6, 6, 7, 7, 7, 7);
            ref Vector256<float> destinationBase = ref Unsafe.As<Vector4, Vector256<float>>(ref destination);

            destinationBase = (Vector256.ShuffleNative(source, indices0) & rgbMask.AsSingle()) | opaqueAlpha;
            Unsafe.Add(ref destinationBase, 1) = (Vector256.ShuffleNative(source, indices1) & rgbMask.AsSingle()) | opaqueAlpha;
            Unsafe.Add(ref destinationBase, 2) = (Vector256.ShuffleNative(source, indices2) & rgbMask.AsSingle()) | opaqueAlpha;
            Unsafe.Add(ref destinationBase, 3) = (Vector256.ShuffleNative(source, indices3) & rgbMask.AsSingle()) | opaqueAlpha;
        }

        /// <summary>
        /// Replicates four normalized luminance samples into four RGB vectors with opaque alpha.
        /// </summary>
        /// <param name="source">The normalized luminance samples.</param>
        /// <param name="destination">The first destination vector.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreLuminanceVectors(Vector128<float> source, ref Vector4 destination)
        {
            Vector128<int> rgbMask = Vector128.Create(-1, -1, -1, 0);
            Vector128<float> opaqueAlpha = Vector128.Create(0F, 0F, 0F, 1F);
            ref Vector128<float> destinationBase = ref Unsafe.As<Vector4, Vector128<float>>(ref destination);

            // The immediate controls broadcast one source lane to RGB. The mask replaces the fourth lane with the
            // implicit alpha value without extracting an individual sample from the SIMD register.
            destinationBase = (Vector128_.ShuffleNative(source, 0b_00_00_00_00) & rgbMask.AsSingle()) | opaqueAlpha;
            Unsafe.Add(ref destinationBase, 1) = (Vector128_.ShuffleNative(source, 0b_01_01_01_01) & rgbMask.AsSingle()) | opaqueAlpha;
            Unsafe.Add(ref destinationBase, 2) = (Vector128_.ShuffleNative(source, 0b_10_10_10_10) & rgbMask.AsSingle()) | opaqueAlpha;
            Unsafe.Add(ref destinationBase, 3) = (Vector128_.ShuffleNative(source, 0b_11_11_11_11) & rgbMask.AsSingle()) | opaqueAlpha;
        }
    }
}
