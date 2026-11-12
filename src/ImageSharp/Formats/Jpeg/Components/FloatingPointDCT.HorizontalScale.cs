// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <content>
/// Contains the scale operators of the decoder stage store.
/// </content>
internal static partial class FloatingPointDCT
{
    /// <summary>
    /// Defines the number of destination rows for each sample of a subsampled component.
    /// </summary>
    /// <remarks>
    /// The factor is a constant of the operator type. Thus, the stores of each row are straight-line code:
    /// the JIT removes the stores for the row indices that are equal to or more than the factor.
    /// </remarks>
    internal interface IVerticalScaleOperator
    {
        /// <summary>
        /// Gets the number of destination rows for each sample: 1 to 4.
        /// </summary>
        static abstract int Factor { get; }
    }

    /// <summary>
    /// Stores samples and repeats each sample horizontally, for scalar values and for each SIMD width.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A subsampled component covers H x V destination samples with each sample.
    /// The operator writes each of the first <c>count</c> samples <see cref="Factor"/> times, in order.
    /// It makes the repeated registers once and stores them in V destination rows.
    /// </para>
    /// <para>
    /// The <see cref="Vector512{T}"/> form holds two blocks: lanes 0 to <c>count - 1</c> are the first block and lanes
    /// 8 to <c>8 + count - 1</c> are the second block. In the destination, the second block follows the first block.
    /// </para>
    /// <para>
    /// All shuffle indices are constants, and each index selects a lane of the same register. Thus, <c>ShuffleNative</c> gives the result
    /// with one permute instruction and no fix-up for indices out of range.
    /// </para>
    /// </remarks>
    internal interface IHorizontalScaleOperator
    {
        /// <summary>
        /// Gets the number of destination samples for each sample: 1 to 4.
        /// </summary>
        static abstract int Factor { get; }

        /// <summary>
        /// Stores one sample <see cref="Factor"/> times in each destination row.
        /// </summary>
        /// <typeparam name="TVertical">The number of destination rows.</typeparam>
        /// <param name="sample">The sample.</param>
        /// <param name="destination">The first sample of the destination.</param>
        /// <param name="index">The index of the first destination sample.</param>
        /// <param name="stride">The distance between two destination rows, in samples.</param>
        static abstract void Store<TVertical>(float sample, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator;

        /// <summary>
        /// Stores the first <paramref name="count"/> samples, each <see cref="Factor"/> times, in each destination row.
        /// </summary>
        /// <typeparam name="TVertical">The number of destination rows.</typeparam>
        /// <param name="samples">The samples.</param>
        /// <param name="count">The number of samples: 4 or 2.</param>
        /// <param name="destination">The first sample of the destination.</param>
        /// <param name="index">The index of the first destination sample.</param>
        /// <param name="stride">The distance between two destination rows, in samples.</param>
        static abstract void Store<TVertical>(Vector128<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator;

        /// <summary>
        /// Stores the first <paramref name="count"/> samples, each <see cref="Factor"/> times, in each destination row.
        /// </summary>
        /// <typeparam name="TVertical">The number of destination rows.</typeparam>
        /// <param name="samples">The samples.</param>
        /// <param name="count">The number of samples: 8, 4 or 2.</param>
        /// <param name="destination">The first sample of the destination.</param>
        /// <param name="index">The index of the first destination sample.</param>
        /// <param name="stride">The distance between two destination rows, in samples.</param>
        static abstract void Store<TVertical>(Vector256<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator;

        /// <summary>
        /// Stores the first <paramref name="count"/> samples of two blocks, each <see cref="Factor"/> times, in each destination row.
        /// </summary>
        /// <typeparam name="TVertical">The number of destination rows.</typeparam>
        /// <param name="samples">The samples. Lanes 0 to 7 hold the first block and lanes 8 to 15 hold the second block.</param>
        /// <param name="count">The number of samples of each block: 8, 4 or 2.</param>
        /// <param name="destination">The first sample of the destination.</param>
        /// <param name="index">The index of the first destination sample.</param>
        /// <param name="stride">The distance between two destination rows, in samples.</param>
        static abstract void Store<TVertical>(Vector512<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator;
    }

    /// <summary>
    /// Stores <paramref name="value"/> at the same index of each destination row.
    /// </summary>
    /// <remarks>
    /// The row count is a constant of <typeparamref name="TVertical"/>. Thus, the JIT keeps only the needed stores,
    /// at constant multiples of <paramref name="stride"/>.
    /// </remarks>
    /// <typeparam name="TVertical">The number of destination rows.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="index">The index in the first row.</param>
    /// <param name="stride">The distance between two destination rows, in samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRows<TVertical>(Vector64<float> value, ref float destination, nuint index, nuint stride)
        where TVertical : struct, IVerticalScaleOperator
    {
        value.StoreUnsafe(ref destination, index);
        if (TVertical.Factor > 1)
        {
            value.StoreUnsafe(ref destination, index + stride);
        }

        if (TVertical.Factor > 2)
        {
            value.StoreUnsafe(ref destination, index + (2 * stride));
        }

        if (TVertical.Factor > 3)
        {
            value.StoreUnsafe(ref destination, index + (3 * stride));
        }
    }

    /// <inheritdoc cref="StoreRows{TVertical}(Vector64{float}, ref float, nuint, nuint)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRows<TVertical>(Vector128<float> value, ref float destination, nuint index, nuint stride)
        where TVertical : struct, IVerticalScaleOperator
    {
        value.StoreUnsafe(ref destination, index);
        if (TVertical.Factor > 1)
        {
            value.StoreUnsafe(ref destination, index + stride);
        }

        if (TVertical.Factor > 2)
        {
            value.StoreUnsafe(ref destination, index + (2 * stride));
        }

        if (TVertical.Factor > 3)
        {
            value.StoreUnsafe(ref destination, index + (3 * stride));
        }
    }

    /// <inheritdoc cref="StoreRows{TVertical}(Vector64{float}, ref float, nuint, nuint)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRows<TVertical>(Vector256<float> value, ref float destination, nuint index, nuint stride)
        where TVertical : struct, IVerticalScaleOperator
    {
        value.StoreUnsafe(ref destination, index);
        if (TVertical.Factor > 1)
        {
            value.StoreUnsafe(ref destination, index + stride);
        }

        if (TVertical.Factor > 2)
        {
            value.StoreUnsafe(ref destination, index + (2 * stride));
        }

        if (TVertical.Factor > 3)
        {
            value.StoreUnsafe(ref destination, index + (3 * stride));
        }
    }

    /// <inheritdoc cref="StoreRows{TVertical}(Vector64{float}, ref float, nuint, nuint)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRows<TVertical>(Vector512<float> value, ref float destination, nuint index, nuint stride)
        where TVertical : struct, IVerticalScaleOperator
    {
        value.StoreUnsafe(ref destination, index);
        if (TVertical.Factor > 1)
        {
            value.StoreUnsafe(ref destination, index + stride);
        }

        if (TVertical.Factor > 2)
        {
            value.StoreUnsafe(ref destination, index + (2 * stride));
        }

        if (TVertical.Factor > 3)
        {
            value.StoreUnsafe(ref destination, index + (3 * stride));
        }
    }

    /// <summary>
    /// Writes one destination row for each sample.
    /// </summary>
    internal readonly struct VerticalScale1 : IVerticalScaleOperator
    {
        /// <inheritdoc/>
        public static int Factor => 1;
    }

    /// <summary>
    /// Writes two destination rows for each sample.
    /// </summary>
    internal readonly struct VerticalScale2 : IVerticalScaleOperator
    {
        /// <inheritdoc/>
        public static int Factor => 2;
    }

    /// <summary>
    /// Writes three destination rows for each sample.
    /// </summary>
    internal readonly struct VerticalScale3 : IVerticalScaleOperator
    {
        /// <inheritdoc/>
        public static int Factor => 3;
    }

    /// <summary>
    /// Writes four destination rows for each sample.
    /// </summary>
    internal readonly struct VerticalScale4 : IVerticalScaleOperator
    {
        /// <inheritdoc/>
        public static int Factor => 4;
    }

    /// <summary>
    /// Stores each sample once.
    /// </summary>
    internal readonly struct HorizontalScale1 : IHorizontalScaleOperator
    {
        /// <inheritdoc/>
        public static int Factor => 1;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(float sample, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            Unsafe.Add(ref destination, index) = sample;
            if (TVertical.Factor > 1)
            {
                Unsafe.Add(ref destination, index + stride) = sample;
            }

            if (TVertical.Factor > 2)
            {
                Unsafe.Add(ref destination, index + (2 * stride)) = sample;
            }

            if (TVertical.Factor > 3)
            {
                Unsafe.Add(ref destination, index + (3 * stride)) = sample;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector128<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            if (count == 4)
            {
                StoreRows<TVertical>(samples, ref destination, index, stride);
            }
            else
            {
                StoreRows<TVertical>(samples.GetLower(), ref destination, index, stride);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector256<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            if (count == 8)
            {
                StoreRows<TVertical>(samples, ref destination, index, stride);
            }
            else if (count == 4)
            {
                StoreRows<TVertical>(samples.GetLower(), ref destination, index, stride);
            }
            else
            {
                StoreRows<TVertical>(samples.GetLower().GetLower(), ref destination, index, stride);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector512<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            if (count == 8)
            {
                // The two blocks are already adjacent.
                StoreRows<TVertical>(samples, ref destination, index, stride);
            }
            else if (count == 4)
            {
                // Move the second block's four samples next to the first block's four samples.
                Vector512<float> packed = Vector512.ShuffleNative(samples, Vector512.Create(0, 1, 2, 3, 8, 9, 10, 11, 0, 0, 0, 0, 0, 0, 0, 0));
                StoreRows<TVertical>(packed.GetLower(), ref destination, index, stride);
            }
            else
            {
                Vector512<float> packed = Vector512.ShuffleNative(samples, Vector512.Create(0, 1, 8, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
                StoreRows<TVertical>(packed.GetLower().GetLower(), ref destination, index, stride);
            }
        }
    }

    /// <summary>
    /// Stores each sample two times.
    /// </summary>
    /// <remarks>
    /// Each shuffle repeats each source lane in two adjacent lanes: <c>[s0 s1 s2 s3]</c> becomes
    /// <c>[s0 s0 s1 s1]</c> and <c>[s2 s2 s3 s3]</c>.
    /// </remarks>
    internal readonly struct HorizontalScale2 : IHorizontalScaleOperator
    {
        /// <inheritdoc/>
        public static int Factor => 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(float sample, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
            => StoreRows<TVertical>(Vector64.Create(sample), ref destination, index, stride);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector128<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            // Samples 0 and 1 make the first four outputs. Samples 2 and 3 make the next four.
            StoreRows<TVertical>(Vector128.ShuffleNative(samples, Vector128.Create(0, 0, 1, 1)), ref destination, index, stride);
            if (count == 4)
            {
                StoreRows<TVertical>(Vector128.ShuffleNative(samples, Vector128.Create(2, 2, 3, 3)), ref destination, index + 4, stride);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector256<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            if (count == 2)
            {
                StoreRows<TVertical>(Vector128.ShuffleNative(samples.GetLower(), Vector128.Create(0, 0, 1, 1)), ref destination, index, stride);
                return;
            }

            // Samples 0 to 3 make the first eight outputs. Samples 4 to 7 make the next eight.
            StoreRows<TVertical>(Vector256.ShuffleNative(samples, Vector256.Create(0, 0, 1, 1, 2, 2, 3, 3)), ref destination, index, stride);
            if (count == 8)
            {
                StoreRows<TVertical>(Vector256.ShuffleNative(samples, Vector256.Create(4, 4, 5, 5, 6, 6, 7, 7)), ref destination, index + 8, stride);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector512<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            // Output sample p is lane p / 2 for p < 2 * count, then lane 8 + (p - 2 * count) / 2.
            if (count == 2)
            {
                Vector512<float> packed = Vector512.ShuffleNative(samples, Vector512.Create(0, 0, 1, 1, 8, 8, 9, 9, 0, 0, 0, 0, 0, 0, 0, 0));
                StoreRows<TVertical>(packed.GetLower(), ref destination, index, stride);
                return;
            }

            if (count == 4)
            {
                Vector512<float> packed = Vector512.ShuffleNative(samples, Vector512.Create(0, 0, 1, 1, 2, 2, 3, 3, 8, 8, 9, 9, 10, 10, 11, 11));
                StoreRows<TVertical>(packed, ref destination, index, stride);
                return;
            }

            Vector512<float> first = Vector512.ShuffleNative(samples, Vector512.Create(0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7));
            Vector512<float> second = Vector512.ShuffleNative(samples, Vector512.Create(8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13, 14, 14, 15, 15));
            StoreRows<TVertical>(first, ref destination, index, stride);
            StoreRows<TVertical>(second, ref destination, index + 16, stride);
        }
    }

    /// <summary>
    /// Stores each sample three times.
    /// </summary>
    /// <remarks>
    /// Output sample <c>p</c> of a block is source lane <c>p / 3</c>. Each shuffle takes the next run of output
    /// samples: <c>[s0 s1 s2 s3]</c> becomes <c>[s0 s0 s0 s1]</c>, <c>[s1 s1 s2 s2]</c> and <c>[s2 s3 s3 s3]</c>.
    /// </remarks>
    internal readonly struct HorizontalScale3 : IHorizontalScaleOperator
    {
        /// <inheritdoc/>
        public static int Factor => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(float sample, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            // The code stores two samples, then one sample. Each row gets three destination samples.
            StoreRows<TVertical>(Vector64.Create(sample), ref destination, index, stride);
            HorizontalScale1.Store<TVertical>(sample, ref destination, index + 2, stride);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector128<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            StoreRows<TVertical>(Vector128.ShuffleNative(samples, Vector128.Create(0, 0, 0, 1)), ref destination, index, stride);
            if (count == 2)
            {
                // Six samples: s0 s0 s0 s1, then s1 s1.
                StoreRows<TVertical>(Vector128.ShuffleNative(samples, Vector128.Create(1, 1, 1, 1)).GetLower(), ref destination, index + 4, stride);
                return;
            }

            // Twelve samples: s0 s0 s0 s1, s1 s1 s2 s2, s2 s3 s3 s3.
            StoreRows<TVertical>(Vector128.ShuffleNative(samples, Vector128.Create(1, 1, 2, 2)), ref destination, index + 4, stride);
            StoreRows<TVertical>(Vector128.ShuffleNative(samples, Vector128.Create(2, 3, 3, 3)), ref destination, index + 8, stride);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector256<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            if (count == 2)
            {
                Store<TVertical>(samples.GetLower(), 2, ref destination, index, stride);
                return;
            }

            StoreRows<TVertical>(Vector256.ShuffleNative(samples, Vector256.Create(0, 0, 0, 1, 1, 1, 2, 2)), ref destination, index, stride);
            if (count == 4)
            {
                // Twelve samples: s0 s0 s0 s1 s1 s1 s2 s2, then s2 s3 s3 s3.
                StoreRows<TVertical>(Vector128.ShuffleNative(samples.GetLower(), Vector128.Create(2, 3, 3, 3)), ref destination, index + 8, stride);
                return;
            }

            // Twenty-four samples in three registers.
            StoreRows<TVertical>(Vector256.ShuffleNative(samples, Vector256.Create(2, 3, 3, 3, 4, 4, 4, 5)), ref destination, index + 8, stride);
            StoreRows<TVertical>(Vector256.ShuffleNative(samples, Vector256.Create(5, 5, 6, 6, 6, 7, 7, 7)), ref destination, index + 16, stride);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector512<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            // Output sample p is lane p / 3 for p < 3 * count, then lane 8 + (p - 3 * count) / 3.
            if (count == 2)
            {
                // Twelve samples: s0 s0 s0 s1 s1 s1 s8 s8, then s8 s9 s9 s9.
                Vector512<float> packed = Vector512.ShuffleNative(samples, Vector512.Create(0, 0, 0, 1, 1, 1, 8, 8, 8, 9, 9, 9, 0, 0, 0, 0));
                StoreRows<TVertical>(packed.GetLower(), ref destination, index, stride);
                StoreRows<TVertical>(packed.GetUpper().GetLower(), ref destination, index + 8, stride);
                return;
            }

            if (count == 4)
            {
                // Twenty-four samples: sixteen, then eight.
                Vector512<float> first = Vector512.ShuffleNative(samples, Vector512.Create(0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 8, 8, 8, 9));
                Vector512<float> last = Vector512.ShuffleNative(samples, Vector512.Create(9, 9, 10, 10, 10, 11, 11, 11, 0, 0, 0, 0, 0, 0, 0, 0));
                StoreRows<TVertical>(first, ref destination, index, stride);
                StoreRows<TVertical>(last.GetLower(), ref destination, index + 16, stride);
                return;
            }

            // Forty-eight samples in three registers.
            Vector512<float> a = Vector512.ShuffleNative(samples, Vector512.Create(0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5));
            Vector512<float> b = Vector512.ShuffleNative(samples, Vector512.Create(5, 5, 6, 6, 6, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10, 10));
            Vector512<float> c = Vector512.ShuffleNative(samples, Vector512.Create(10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15));
            StoreRows<TVertical>(a, ref destination, index, stride);
            StoreRows<TVertical>(b, ref destination, index + 16, stride);
            StoreRows<TVertical>(c, ref destination, index + 32, stride);
        }
    }

    /// <summary>
    /// Stores each sample four times.
    /// </summary>
    /// <remarks>
    /// Each shuffle repeats each source lane in four adjacent lanes: <c>[s0 s1 ...]</c> becomes
    /// <c>[s0 s0 s0 s0 s1 s1 s1 s1]</c> and so on.
    /// </remarks>
    internal readonly struct HorizontalScale4 : IHorizontalScaleOperator
    {
        /// <inheritdoc/>
        public static int Factor => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(float sample, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
            => StoreRows<TVertical>(Vector128.Create(sample), ref destination, index, stride);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector128<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            // Each register is one sample repeated four times.
            StoreRows<TVertical>(Vector128.ShuffleNative(samples, Vector128.Create(0)), ref destination, index, stride);
            StoreRows<TVertical>(Vector128.ShuffleNative(samples, Vector128.Create(1)), ref destination, index + 4, stride);
            if (count == 4)
            {
                StoreRows<TVertical>(Vector128.ShuffleNative(samples, Vector128.Create(2)), ref destination, index + 8, stride);
                StoreRows<TVertical>(Vector128.ShuffleNative(samples, Vector128.Create(3)), ref destination, index + 12, stride);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector256<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            // Each register is two samples, each repeated four times.
            StoreRows<TVertical>(Vector256.ShuffleNative(samples, Vector256.Create(0, 0, 0, 0, 1, 1, 1, 1)), ref destination, index, stride);
            if (count == 2)
            {
                return;
            }

            StoreRows<TVertical>(Vector256.ShuffleNative(samples, Vector256.Create(2, 2, 2, 2, 3, 3, 3, 3)), ref destination, index + 8, stride);
            if (count == 8)
            {
                StoreRows<TVertical>(Vector256.ShuffleNative(samples, Vector256.Create(4, 4, 4, 4, 5, 5, 5, 5)), ref destination, index + 16, stride);
                StoreRows<TVertical>(Vector256.ShuffleNative(samples, Vector256.Create(6, 6, 6, 6, 7, 7, 7, 7)), ref destination, index + 24, stride);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store<TVertical>(Vector512<float> samples, int count, ref float destination, nuint index, nuint stride)
            where TVertical : struct, IVerticalScaleOperator
        {
            // Output sample p is lane p / 4 for p < 4 * count, then lane 8 + (p - 4 * count) / 4.
            if (count == 2)
            {
                Vector512<float> packed = Vector512.ShuffleNative(samples, Vector512.Create(0, 0, 0, 0, 1, 1, 1, 1, 8, 8, 8, 8, 9, 9, 9, 9));
                StoreRows<TVertical>(packed, ref destination, index, stride);
                return;
            }

            if (count == 4)
            {
                Vector512<float> first = Vector512.ShuffleNative(samples, Vector512.Create(0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3));
                Vector512<float> second = Vector512.ShuffleNative(samples, Vector512.Create(8, 8, 8, 8, 9, 9, 9, 9, 10, 10, 10, 10, 11, 11, 11, 11));
                StoreRows<TVertical>(first, ref destination, index, stride);
                StoreRows<TVertical>(second, ref destination, index + 16, stride);
                return;
            }

            Vector512<float> a = Vector512.ShuffleNative(samples, Vector512.Create(0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3));
            Vector512<float> b = Vector512.ShuffleNative(samples, Vector512.Create(4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7));
            Vector512<float> c = Vector512.ShuffleNative(samples, Vector512.Create(8, 8, 8, 8, 9, 9, 9, 9, 10, 10, 10, 10, 11, 11, 11, 11));
            Vector512<float> d = Vector512.ShuffleNative(samples, Vector512.Create(12, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 14, 15, 15, 15, 15));
            StoreRows<TVertical>(a, ref destination, index, stride);
            StoreRows<TVertical>(b, ref destination, index + 16, stride);
            StoreRows<TVertical>(c, ref destination, index + 32, stride);
            StoreRows<TVertical>(d, ref destination, index + 48, stride);
        }
    }
}
