// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <content>
/// Defines the sample-width-specific loads and stores of the temporal filter traversals.
/// </content>
internal static partial class Av1TemporalFilter
{
    /// <summary>
    /// Defines how the temporal filter reads and writes one sample storage type across hardware widths.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <remarks>
    /// <para>
    /// The filter, prediction and noise arithmetic does not depend on the storage type once the samples are widened, so it is
    /// written once in <see cref="Av1TemporalFilter"/>. This operator supplies only the parts that do depend on the type: the
    /// widening loads, the narrowing stores and the scalar conversions.
    /// </para>
    /// <para>
    /// Most kernels widen samples to thirty-two-bit lanes: a squared twelve-bit difference needs twenty-four bits and a filter
    /// accumulator needs twenty-six. The noise kernel is the exception. It keeps sixteen-bit lanes, because every intermediate
    /// Sobel and Laplacian value of a twelve-bit plane stays inside the signed sixteen-bit range.
    /// </para>
    /// </remarks>
    internal interface ITemporalFilterOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Loads four samples and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened samples in increasing order.</returns>
        public static abstract Vector128<uint> Load(ref TSample source, Vector128<uint> lanes);

        /// <summary>
        /// Loads eight samples and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened samples in increasing order.</returns>
        public static abstract Vector256<uint> Load(ref TSample source, Vector256<uint> lanes);

        /// <summary>
        /// Loads sixteen samples and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened samples in increasing order.</returns>
        public static abstract Vector512<uint> Load(ref TSample source, Vector512<uint> lanes);

        /// <summary>
        /// Loads eight samples into signed sixteen-bit lanes.
        /// </summary>
        /// <param name="source">The first sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The samples in increasing order.</returns>
        public static abstract Vector128<short> Load(ref TSample source, Vector128<short> lanes);

        /// <summary>
        /// Loads sixteen samples into signed sixteen-bit lanes.
        /// </summary>
        /// <param name="source">The first sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The samples in increasing order.</returns>
        public static abstract Vector256<short> Load(ref TSample source, Vector256<short> lanes);

        /// <summary>
        /// Loads thirty-two samples into signed sixteen-bit lanes.
        /// </summary>
        /// <param name="source">The first sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The samples in increasing order.</returns>
        public static abstract Vector512<short> Load(ref TSample source, Vector512<short> lanes);

        /// <summary>
        /// Stores four values as samples.
        /// </summary>
        /// <param name="values">The values, each inside the sample range.</param>
        /// <param name="destination">The first sample to write.</param>
        public static abstract void Store(Vector128<uint> values, ref TSample destination);

        /// <summary>
        /// Stores eight values as samples.
        /// </summary>
        /// <param name="values">The values, each inside the sample range.</param>
        /// <param name="destination">The first sample to write.</param>
        public static abstract void Store(Vector256<uint> values, ref TSample destination);

        /// <summary>
        /// Stores sixteen values as samples.
        /// </summary>
        /// <param name="values">The values, each inside the sample range.</param>
        /// <param name="destination">The first sample to write.</param>
        public static abstract void Store(Vector512<uint> values, ref TSample destination);

        /// <summary>
        /// Returns the value of one sample.
        /// </summary>
        /// <param name="sample">The sample.</param>
        /// <returns>The sample value.</returns>
        public static abstract int ToInt32(TSample sample);

        /// <summary>
        /// Creates one sample from its value.
        /// </summary>
        /// <param name="value">The value, inside the sample range.</param>
        /// <returns>The sample.</returns>
        public static abstract TSample CreateSample(int value);
    }

    /// <summary>
    /// Carries the terms that are constant across one sixteen-by-sixteen sub-block of one plane in the weight kernel.
    /// </summary>
    internal readonly struct WeightTerms
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WeightTerms"/> struct.
        /// </summary>
        /// <param name="inverseReferenceCount">The reciprocal of the number of squared errors in a window.</param>
        /// <param name="blockError">The sub-block motion search error, already multiplied by the normalization factor.</param>
        /// <param name="firstFactor">The first multiplier of the combined error.</param>
        /// <param name="secondFactor">The second multiplier of the combined error.</param>
        /// <param name="level">The weight calculation level: zero for the exponential, one for its approximation.</param>
        public WeightTerms(double inverseReferenceCount, double blockError, double firstFactor, double secondFactor, int level)
        {
            this.InverseReferenceCount = inverseReferenceCount;
            this.BlockError = blockError;
            this.FirstFactor = firstFactor;
            this.SecondFactor = secondFactor;
            this.Level = level;
        }

        /// <summary>
        /// Gets the reciprocal of the number of squared errors in a window: 25 on luma, 25 plus the covered luma
        /// samples on chroma.
        /// </summary>
        public double InverseReferenceCount { get; }

        /// <summary>
        /// Gets the sub-block motion search error multiplied by the normalization factor.
        /// </summary>
        public double BlockError { get; }

        /// <summary>
        /// Gets the first multiplier of the combined error.
        /// </summary>
        /// <remarks>
        /// Most frames pass the product of the distance factor and the decay factor here, and one as <see cref="SecondFactor"/>.
        /// A multiplication by one is exact. High-bit-depth frames with 4:2:2 subsampling pass the distance factor here and the decay
        /// factor as <see cref="SecondFactor"/>, so the error rounds twice.
        /// </remarks>
        public double FirstFactor { get; }

        /// <summary>
        /// Gets the second multiplier of the combined error.
        /// </summary>
        public double SecondFactor { get; }

        /// <summary>
        /// Gets the weight calculation level. Zero uses the exact exponential. One uses the approximated exponential.
        /// </summary>
        public int Level { get; }
    }

    /// <summary>
    /// Carries the per-plane constants of the noise estimation kernel.
    /// </summary>
    internal readonly struct NoiseTerms
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NoiseTerms"/> struct.
        /// </summary>
        /// <param name="edgeThreshold">The gradient magnitude below which a sample counts as smooth.</param>
        /// <param name="bitDepth">The sample bit depth.</param>
        public NoiseTerms(int edgeThreshold, int bitDepth)
        {
            this.EdgeThreshold = edgeThreshold;
            this.Shift = bitDepth - 8;
            this.Bias = (1 << this.Shift) >> 1;
        }

        /// <summary>
        /// Gets the gradient magnitude below which a sample counts as smooth.
        /// </summary>
        public int EdgeThreshold { get; }

        /// <summary>
        /// Gets the shift that scales high-bit-depth magnitudes to the eight-bit domain.
        /// </summary>
        public int Shift { get; }

        /// <summary>
        /// Gets the rounding bias of <see cref="Shift"/>: half the divisor, or zero for a zero shift.
        /// </summary>
        public int Bias { get; }
    }

    /// <summary>
    /// Loads and stores eight-bit samples for the shared temporal filter lane arithmetic.
    /// </summary>
    internal readonly struct ByteOperator : ITemporalFilterOperator<byte>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> Load(ref byte source, Vector128<uint> lanes)
        {
            // Exactly four bytes are read, so the last group of a row never reads the next row.
            Vector128<byte> bytes = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<uint>(ref source)).AsByte();
            return Vector128.WidenLower(Vector128.WidenLower(bytes));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> Load(ref byte source, Vector256<uint> lanes)
        {
            // Exactly eight bytes are read. The first widening makes eight words, and the second makes eight thirty-two-bit lanes.
            Vector128<ushort> words = Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref source)).AsByte());
            return Vector256.WidenLower(words.ToVector256Unsafe());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> Load(ref byte source, Vector512<uint> lanes)
        {
            Vector256<ushort> words = Vector256.WidenLower(Vector128.LoadUnsafe(ref source).ToVector256Unsafe());
            return Vector512.WidenLower(words.ToVector512Unsafe());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Load(ref byte source, Vector128<short> lanes)
            => Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref source)).AsByte()).AsInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Load(ref byte source, Vector256<short> lanes)
            => Vector256.WidenLower(Vector128.LoadUnsafe(ref source).ToVector256Unsafe()).AsInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Load(ref byte source, Vector512<short> lanes)
            => Vector512.WidenLower(Vector256.LoadUnsafe(ref source).ToVector512Unsafe()).AsInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Vector128<uint> values, ref byte destination)
        {
            // Each value is below 256, so the two truncating narrows keep it. The low four bytes hold the four samples.
            Vector128<ushort> words = Vector128.Narrow(values, values);
            Unsafe.WriteUnaligned(ref destination, Vector128.Narrow(words, words).AsUInt32().ToScalar());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Vector256<uint> values, ref byte destination)
        {
            Vector128<ushort> words = Vector128.Narrow(values.GetLower(), values.GetUpper());
            Unsafe.WriteUnaligned(ref destination, Vector128.Narrow(words, words).AsUInt64().ToScalar());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Vector512<uint> values, ref byte destination)
        {
            Vector256<ushort> words = Vector256.Narrow(values.GetLower(), values.GetUpper());
            Vector128.Narrow(words.GetLower(), words.GetUpper()).StoreUnsafe(ref destination);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt32(byte sample) => sample;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte CreateSample(int value) => (byte)value;
    }

    /// <summary>
    /// Loads and stores high-bit-depth samples for the shared temporal filter lane arithmetic.
    /// </summary>
    internal readonly struct UInt16Operator : ITemporalFilterOperator<ushort>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> Load(ref ushort source, Vector128<uint> lanes)
            => Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref source))).AsUInt16());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> Load(ref ushort source, Vector256<uint> lanes)
            => Vector256.WidenLower(Vector128.LoadUnsafe(ref source).ToVector256Unsafe());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> Load(ref ushort source, Vector512<uint> lanes)
            => Vector512.WidenLower(Vector256.LoadUnsafe(ref source).ToVector512Unsafe());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Load(ref ushort source, Vector128<short> lanes)
        {
            // Twelve-bit samples fit the signed sixteen-bit lanes directly, so the load needs no widening.
            return Vector128.LoadUnsafe(ref source).AsInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Load(ref ushort source, Vector256<short> lanes)
            => Vector256.LoadUnsafe(ref source).AsInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Load(ref ushort source, Vector512<short> lanes)
            => Vector512.LoadUnsafe(ref source).AsInt16();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Vector128<uint> values, ref ushort destination)
        {
            // Each value is below 4096, so the truncating narrow keeps it. The low eight bytes hold the four samples.
            Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref destination), Vector128.Narrow(values, values).AsUInt64().ToScalar());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Vector256<uint> values, ref ushort destination)
            => Vector128.Narrow(values.GetLower(), values.GetUpper()).StoreUnsafe(ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Vector512<uint> values, ref ushort destination)
            => Vector256.Narrow(values.GetLower(), values.GetUpper()).StoreUnsafe(ref destination);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt32(ushort sample) => sample;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort CreateSample(int value) => (ushort)value;
    }
}
