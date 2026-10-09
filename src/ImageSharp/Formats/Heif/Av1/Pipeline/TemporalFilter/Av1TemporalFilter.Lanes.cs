// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <content>
/// Defines the lane arithmetic shared by the eight-bit and high-bit-depth temporal filter operators.
/// </content>
internal static partial class Av1TemporalFilter
{
    /// <summary>
    /// Holds the lane arithmetic of the temporal filter kernels, independent of the sample storage type.
    /// </summary>
    private static class TemporalFilterLanes
    {
        /// <summary>
        /// The bit pattern of 2^52 as a <see cref="double"/>. A thirty-two-bit integer placed in the low mantissa
        /// bits of this value produces 2^52 plus the integer, exactly.
        /// </summary>
        private const ulong DoubleExponentBits = 0x4330000000000000UL;

        /// <summary>
        /// The value 2^52, subtracted after <see cref="DoubleExponentBits"/> is applied to recover the integer.
        /// </summary>
        private const double DoubleExponentValue = 4503599627370496.0;

        /// <summary>
        /// The filter weight given to a sample of the frame to filter and the scale of every other weight.
        /// </summary>
        private const int WeightScale = 1000;

        /// <summary>
        /// The largest scaled error. Larger errors give the same smallest weight.
        /// </summary>
        private const float MaximumScaledError = 7f;

        /// <summary>
        /// The multiplier of the approximated exponential: (1 &lt;&lt; 23) / ln(2) in single precision. The single-precision value
        /// keeps the weights the same as other AV1 encoders.
        /// </summary>
        private const float ExponentMultiplier = (1 << 23) / 0.69314718056f;

        /// <summary>
        /// The integer offset of the approximated exponential: the IEEE single-precision exponent bias 127 shifted into the exponent
        /// field, less the accuracy constant 60801.
        /// </summary>
        private const int ExponentOffset = (127 << 23) - 60801;

        /// <summary>
        /// Loads four bytes and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> LoadBytes(ref byte source, Vector128<uint> lanes)
        {
            // Exactly four bytes are read, so the last group of a row never reads the next row.
            Vector128<byte> bytes = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<uint>(ref source)).AsByte();
            return Vector128.WidenLower(Vector128.WidenLower(bytes));
        }

        /// <summary>
        /// Loads eight bytes and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> LoadBytes(ref byte source, Vector256<uint> lanes)
        {
            Vector128<ushort> words = Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref source)).AsByte());
            return Vector256.WidenLower(words.ToVector256Unsafe());
        }

        /// <summary>
        /// Loads sixteen bytes and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> LoadBytes(ref byte source, Vector512<uint> lanes)
        {
            Vector256<ushort> words = Vector256.WidenLower(Vector128.LoadUnsafe(ref source).ToVector256Unsafe());
            return Vector512.WidenLower(words.ToVector512Unsafe());
        }

        /// <summary>
        /// Loads eight bytes and widens them to signed sixteen-bit lanes.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> LoadBytes(ref byte source, Vector128<short> lanes)
            => Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref source)).AsByte()).AsInt16();

        /// <summary>
        /// Loads sixteen bytes and widens them to signed sixteen-bit lanes.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> LoadBytes(ref byte source, Vector256<short> lanes)
            => Vector256.WidenLower(Vector128.LoadUnsafe(ref source).ToVector256Unsafe()).AsInt16();

        /// <summary>
        /// Loads thirty-two bytes and widens them to signed sixteen-bit lanes.
        /// </summary>
        /// <param name="source">The first byte.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> LoadBytes(ref byte source, Vector512<short> lanes)
            => Vector512.WidenLower(Vector256.LoadUnsafe(ref source).ToVector512Unsafe()).AsInt16();

        /// <summary>
        /// Loads four words and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first word.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> LoadWords(ref ushort source, Vector128<uint> lanes)
            => Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref source))).AsUInt16());

        /// <summary>
        /// Loads eight words and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first word.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> LoadWords(ref ushort source, Vector256<uint> lanes)
            => Vector256.WidenLower(Vector128.LoadUnsafe(ref source).ToVector256Unsafe());

        /// <summary>
        /// Loads sixteen words and widens them to thirty-two-bit lanes.
        /// </summary>
        /// <param name="source">The first word.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The widened values in increasing order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> LoadWords(ref ushort source, Vector512<uint> lanes)
            => Vector512.WidenLower(Vector256.LoadUnsafe(ref source).ToVector512Unsafe());

        /// <summary>
        /// Returns the squared differences of four lanes.
        /// </summary>
        /// <param name="frame">The widened samples of the frame to filter.</param>
        /// <param name="prediction">The widened predicted samples.</param>
        /// <returns>The squared differences.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> SquaredErrors(Vector128<uint> frame, Vector128<uint> prediction)
        {
            // A twelve-bit difference squares to less than 2^24, so the low thirty-two bits of the product are the whole square.
            // An eight-bit square is below 2^16, so a sixteen-bit lane also holds it exactly and gives the same result.
            Vector128<int> difference = frame.AsInt32() - prediction.AsInt32();
            return (difference * difference).AsUInt32();
        }

        /// <summary>
        /// Returns the squared differences of eight lanes.
        /// </summary>
        /// <param name="frame">The widened samples of the frame to filter.</param>
        /// <param name="prediction">The widened predicted samples.</param>
        /// <returns>The squared differences.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> SquaredErrors(Vector256<uint> frame, Vector256<uint> prediction)
        {
            Vector256<int> difference = frame.AsInt32() - prediction.AsInt32();
            return (difference * difference).AsUInt32();
        }

        /// <summary>
        /// Returns the squared differences of sixteen lanes.
        /// </summary>
        /// <param name="frame">The widened samples of the frame to filter.</param>
        /// <param name="prediction">The widened predicted samples.</param>
        /// <returns>The squared differences.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> SquaredErrors(Vector512<uint> frame, Vector512<uint> prediction)
        {
            Vector512<int> difference = frame.AsInt32() - prediction.AsInt32();
            return (difference * difference).AsUInt32();
        }

        /// <summary>
        /// Returns the squared difference of one sample pair.
        /// </summary>
        /// <param name="frame">The sample of the frame to filter.</param>
        /// <param name="prediction">The predicted sample.</param>
        /// <returns>The squared difference.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint SquaredError(int frame, int prediction)
        {
            int difference = frame - prediction;
            return (uint)(difference * difference);
        }

        /// <summary>
        /// Adds four samples at the full weight.
        /// </summary>
        /// <param name="samples">The widened samples.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(Vector128<uint> samples, ref uint accumulator, ref ushort count)
        {
            (Vector128.LoadUnsafe(ref accumulator) + (samples * (uint)WeightScale)).StoreUnsafe(ref accumulator);

            // Four sixteen-bit totals occupy one sixty-four-bit word. The lane addition keeps them separate.
            ref byte countBytes = ref Unsafe.As<ushort, byte>(ref count);
            Vector128<ushort> counts = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref countBytes)).AsUInt16();
            Unsafe.WriteUnaligned(ref countBytes, (counts + Vector128.Create((ushort)WeightScale)).AsUInt64().ToScalar());
        }

        /// <summary>
        /// Adds eight samples at the full weight.
        /// </summary>
        /// <param name="samples">The widened samples.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(Vector256<uint> samples, ref uint accumulator, ref ushort count)
        {
            (Vector256.LoadUnsafe(ref accumulator) + (samples * (uint)WeightScale)).StoreUnsafe(ref accumulator);
            (Vector128.LoadUnsafe(ref count) + Vector128.Create((ushort)WeightScale)).StoreUnsafe(ref count);
        }

        /// <summary>
        /// Adds sixteen samples at the full weight.
        /// </summary>
        /// <param name="samples">The widened samples.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(Vector512<uint> samples, ref uint accumulator, ref ushort count)
        {
            (Vector512.LoadUnsafe(ref accumulator) + (samples * (uint)WeightScale)).StoreUnsafe(ref accumulator);
            (Vector256.LoadUnsafe(ref count) + Vector256.Create((ushort)WeightScale)).StoreUnsafe(ref count);
        }

        /// <summary>
        /// Adds one sample at the full weight.
        /// </summary>
        /// <param name="sample">The sample.</param>
        /// <param name="accumulator">The weighted sum to update.</param>
        /// <param name="count">The weight total to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateSelf(int sample, ref uint accumulator, ref ushort count)
        {
            accumulator += (uint)(WeightScale * sample);
            count += WeightScale;
        }

        /// <summary>
        /// Weighs four samples with two 128-bit double registers.
        /// </summary>
        /// <param name="errors">The four window errors.</param>
        /// <param name="predictions">The four widened predicted samples.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        /// <param name="terms">The sub-block error terms.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(Vector128<uint> errors, Vector128<uint> predictions, ref uint accumulator, ref ushort count, in WeightTerms terms, Vector128<double> lanes)
        {
            Vector128<double> lower = ScaleErrors(ToDouble(Vector128.WidenLower(errors)), in terms);
            Vector128<double> upper = ScaleErrors(ToDouble(Vector128.WidenUpper(errors)), in terms);

            // Narrow() rounds each double to the nearest float before the single-precision exponential, as the wider overloads do.
            AddWeights(GetWeights(Vector128.Narrow(lower, upper)), predictions, ref accumulator, ref count);
        }

        /// <summary>
        /// Weighs four samples with one 256-bit double register.
        /// </summary>
        /// <param name="errors">The four window errors.</param>
        /// <param name="predictions">The four widened predicted samples.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        /// <param name="terms">The sub-block error terms.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(Vector128<uint> errors, Vector128<uint> predictions, ref uint accumulator, ref ushort count, in WeightTerms terms, Vector256<double> lanes)
        {
            Vector256<double> scaled = ScaleErrors(ToDouble(Vector256.WidenLower(errors.ToVector256Unsafe())), in terms);
            AddWeights(GetWeights(Vector128.Narrow(scaled.GetLower(), scaled.GetUpper())), predictions, ref accumulator, ref count);
        }

        /// <summary>
        /// Weighs eight samples with one 512-bit double register.
        /// </summary>
        /// <param name="errors">The eight window errors.</param>
        /// <param name="predictions">The eight widened predicted samples.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        /// <param name="terms">The sub-block error terms.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(Vector256<uint> errors, Vector256<uint> predictions, ref uint accumulator, ref ushort count, in WeightTerms terms)
        {
            Vector512<double> scaled = ScaleErrors(ToDouble(Vector512.WidenLower(errors.ToVector512Unsafe())), in terms);
            Vector256<int> weights = GetWeights(Vector256.Narrow(scaled.GetLower(), scaled.GetUpper()));

            // A weight is from 0 to WeightScale, so the truncating narrow to sixteen bits gives the same value as a saturating narrow.
            (Vector128.LoadUnsafe(ref count) + Vector128.Narrow(weights.GetLower().AsUInt32(), weights.GetUpper().AsUInt32())).StoreUnsafe(ref count);
            (Vector256.LoadUnsafe(ref accumulator) + (weights.AsUInt32() * predictions)).StoreUnsafe(ref accumulator);
        }

        /// <summary>
        /// Weighs one sample and adds it to the accumulators.
        /// </summary>
        /// <param name="windowError">The window error.</param>
        /// <param name="prediction">The predicted sample.</param>
        /// <param name="accumulator">The weighted sum to update.</param>
        /// <param name="count">The weight total to update.</param>
        /// <param name="terms">The sub-block error terms.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateWeights(uint windowError, int prediction, ref uint accumulator, ref ushort count, in WeightTerms terms)
        {
            // Every product and sum is a separately rounded double operation in a fixed order. The vector overloads use the same
            // order, so all paths give the same weights. This order also keeps the weights the same as other AV1 encoders.
            double windowErrorScaled = windowError * terms.InverseReferenceCount;
            double combinedError = (CombinedWindowWeight * windowErrorScaled) + terms.BlockError;
            double scaledError = combinedError * terms.FirstFactor * terms.SecondFactor;
            scaledError = scaledError < MaximumScaledError ? scaledError : MaximumScaledError;
            int weight;
            if (terms.Level == 0)
            {
                weight = (int)(Math.Exp(-scaledError) * WeightScale);
            }
            else
            {
                // The approximated exponential builds the float bit pattern from the scaled exponent. The weight then rounds half up.
                float exponent = BitConverter.Int32BitsToSingle((int)((float)-scaledError * ExponentMultiplier) + ExponentOffset);
                weight = (int)((exponent * WeightScale) + 0.5f);
            }

            count += (ushort)weight;
            accumulator += (uint)(weight * prediction);
        }

        /// <summary>
        /// Divides four accumulators by their totals with two 128-bit double registers.
        /// </summary>
        /// <param name="accumulator">The first weighted sum.</param>
        /// <param name="count">The first weight total.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The four quotients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> Normalize(ref uint accumulator, ref ushort count, Vector128<double> lanes)
        {
            Vector128<uint> counts = Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref count))).AsUInt16());
            Vector128<uint> numerators = Vector128.LoadUnsafe(ref accumulator) + Vector128.ShiftRightLogical(counts, 1);
            Vector128<ulong> lower = Divide(ToDouble(Vector128.WidenLower(numerators)), ToDouble(Vector128.WidenLower(counts)));
            Vector128<ulong> upper = Divide(ToDouble(Vector128.WidenUpper(numerators)), ToDouble(Vector128.WidenUpper(counts)));
            return Vector128.Narrow(lower, upper);
        }

        /// <summary>
        /// Divides four accumulators by their totals with one 256-bit double register.
        /// </summary>
        /// <param name="accumulator">The first weighted sum.</param>
        /// <param name="count">The first weight total.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The four quotients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> Normalize(ref uint accumulator, ref ushort count, Vector256<double> lanes)
        {
            Vector128<uint> counts = Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref count))).AsUInt16());
            Vector128<uint> numerators = Vector128.LoadUnsafe(ref accumulator) + Vector128.ShiftRightLogical(counts, 1);
            Vector256<ulong> quotients = Divide(
                ToDouble(Vector256.WidenLower(numerators.ToVector256Unsafe())),
                ToDouble(Vector256.WidenLower(counts.ToVector256Unsafe())));

            return Vector128.Narrow(quotients.GetLower(), quotients.GetUpper());
        }

        /// <summary>
        /// Divides eight accumulators by their totals with one 512-bit double register.
        /// </summary>
        /// <param name="accumulator">The first weighted sum.</param>
        /// <param name="count">The first weight total.</param>
        /// <returns>The eight quotients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> Normalize(ref uint accumulator, ref ushort count)
        {
            Vector256<uint> counts = Vector256.WidenLower(Vector128.LoadUnsafe(ref count).ToVector256Unsafe());
            Vector256<uint> numerators = Vector256.LoadUnsafe(ref accumulator) + Vector256.ShiftRightLogical(counts, 1);
            Vector512<ulong> quotients = Divide(
                ToDouble(Vector512.WidenLower(numerators.ToVector512Unsafe())),
                ToDouble(Vector512.WidenLower(counts.ToVector512Unsafe())));

            return Vector256.Narrow(quotients.GetLower(), quotients.GetUpper());
        }

        /// <summary>
        /// Divides one accumulator by its total with rounding.
        /// </summary>
        /// <param name="accumulator">The weighted sum.</param>
        /// <param name="count">The weight total.</param>
        /// <returns>The quotient.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Normalize(uint accumulator, ushort count)
            => (accumulator + (uint)(count >> 1)) / count;

        /// <summary>
        /// Stores the low byte of four quotients.
        /// </summary>
        /// <param name="values">The quotients, each below 256.</param>
        /// <param name="destination">The first byte to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreBytes(Vector128<uint> values, ref byte destination)
        {
            Vector128<ushort> words = Vector128.Narrow(values, values);
            Unsafe.WriteUnaligned(ref destination, Vector128.Narrow(words, words).AsUInt32().ToScalar());
        }

        /// <summary>
        /// Stores the low byte of eight quotients.
        /// </summary>
        /// <param name="values">The quotients, each below 256.</param>
        /// <param name="destination">The first byte to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreBytes(Vector256<uint> values, ref byte destination)
        {
            Vector128<ushort> words = Vector128.Narrow(values.GetLower(), values.GetUpper());
            Unsafe.WriteUnaligned(ref destination, Vector128.Narrow(words, words).AsUInt64().ToScalar());
        }

        /// <summary>
        /// Stores the low word of four quotients.
        /// </summary>
        /// <param name="values">The quotients, each below 4096.</param>
        /// <param name="destination">The first word to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreWords(Vector128<uint> values, ref ushort destination)
            => Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref destination), Vector128.Narrow(values, values).AsUInt64().ToScalar());

        /// <summary>
        /// Stores the low word of eight quotients.
        /// </summary>
        /// <param name="values">The quotients, each below 4096.</param>
        /// <param name="destination">The first word to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreWords(Vector256<uint> values, ref ushort destination)
            => Vector128.Narrow(values.GetLower(), values.GetUpper()).StoreUnsafe(ref destination);

        /// <summary>
        /// Adds the Laplacian magnitudes of the smooth samples of eight columns.
        /// </summary>
        /// <param name="a">The above-left samples.</param>
        /// <param name="b">The above samples.</param>
        /// <param name="c">The above-right samples.</param>
        /// <param name="d">The left samples.</param>
        /// <param name="e">The center samples.</param>
        /// <param name="f">The right samples.</param>
        /// <param name="g">The below-left samples.</param>
        /// <param name="h">The below samples.</param>
        /// <param name="i">The below-right samples.</param>
        /// <param name="terms">The edge threshold and the bit-depth rounding.</param>
        /// <param name="sum">The running Laplacian total.</param>
        /// <param name="count">The running smooth-sample total.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(
            Vector128<short> a,
            Vector128<short> b,
            Vector128<short> c,
            Vector128<short> d,
            Vector128<short> e,
            Vector128<short> f,
            Vector128<short> g,
            Vector128<short> h,
            Vector128<short> i,
            in NoiseTerms terms,
            ref Vector128<int> sum,
            ref Vector128<int> count)
        {
            // Each Sobel gradient of a twelve-bit plane is at most 4 * 4095 in magnitude, so their sum is at most
            // 32760 and fits a signed lane. The rounding bias can carry that sum to 32768, so the rounding shift
            // treats the lanes as unsigned. The Laplacian 4E - 2(B+D+F+H) + (A+C+G+I) also stays within +/-32760
            // when evaluated in this order.
            Vector128<ushort> bias = Vector128.Create((ushort)terms.Bias);
            Vector128<short> gx = (a - c) + (g - i) + ((d - f) << 1);
            Vector128<short> gy = (a - g) + (c - i) + ((b - h) << 1);
            Vector128<ushort> gradient = Vector128.ShiftRightLogical((Vector128.Abs(gx) + Vector128.Abs(gy)).AsUInt16() + bias, terms.Shift);
            Vector128<short> smooth = Vector128.LessThan(gradient.AsInt16(), Vector128.Create((short)terms.EdgeThreshold));
            Vector128<short> laplacian = (e << 2) - ((b + d + f + h) << 1) + (a + c + g + i);
            Vector128<short> magnitude = Vector128.ShiftRightLogical(Vector128.Abs(laplacian).AsUInt16() + bias, terms.Shift).AsInt16() & smooth;

            // A smooth lane is all ones, minus one after sign extension, so subtracting it counts the lane.
            (Vector128<int> magnitudeLower, Vector128<int> magnitudeUpper) = Vector128.Widen(magnitude);
            (Vector128<int> smoothLower, Vector128<int> smoothUpper) = Vector128.Widen(smooth);
            sum += magnitudeLower + magnitudeUpper;
            count -= smoothLower + smoothUpper;
        }

        /// <summary>
        /// Adds the Laplacian magnitudes of the smooth samples of sixteen columns.
        /// </summary>
        /// <param name="a">The above-left samples.</param>
        /// <param name="b">The above samples.</param>
        /// <param name="c">The above-right samples.</param>
        /// <param name="d">The left samples.</param>
        /// <param name="e">The center samples.</param>
        /// <param name="f">The right samples.</param>
        /// <param name="g">The below-left samples.</param>
        /// <param name="h">The below samples.</param>
        /// <param name="i">The below-right samples.</param>
        /// <param name="terms">The edge threshold and the bit-depth rounding.</param>
        /// <param name="sum">The running Laplacian total.</param>
        /// <param name="count">The running smooth-sample total.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(
            Vector256<short> a,
            Vector256<short> b,
            Vector256<short> c,
            Vector256<short> d,
            Vector256<short> e,
            Vector256<short> f,
            Vector256<short> g,
            Vector256<short> h,
            Vector256<short> i,
            in NoiseTerms terms,
            ref Vector256<int> sum,
            ref Vector256<int> count)
        {
            Vector256<ushort> bias = Vector256.Create((ushort)terms.Bias);
            Vector256<short> gx = (a - c) + (g - i) + ((d - f) << 1);
            Vector256<short> gy = (a - g) + (c - i) + ((b - h) << 1);
            Vector256<ushort> gradient = Vector256.ShiftRightLogical((Vector256.Abs(gx) + Vector256.Abs(gy)).AsUInt16() + bias, terms.Shift);
            Vector256<short> smooth = Vector256.LessThan(gradient.AsInt16(), Vector256.Create((short)terms.EdgeThreshold));
            Vector256<short> laplacian = (e << 2) - ((b + d + f + h) << 1) + (a + c + g + i);
            Vector256<short> magnitude = Vector256.ShiftRightLogical(Vector256.Abs(laplacian).AsUInt16() + bias, terms.Shift).AsInt16() & smooth;
            (Vector256<int> magnitudeLower, Vector256<int> magnitudeUpper) = Vector256.Widen(magnitude);
            (Vector256<int> smoothLower, Vector256<int> smoothUpper) = Vector256.Widen(smooth);
            sum += magnitudeLower + magnitudeUpper;
            count -= smoothLower + smoothUpper;
        }

        /// <summary>
        /// Adds the Laplacian magnitudes of the smooth samples of thirty-two columns.
        /// </summary>
        /// <param name="a">The above-left samples.</param>
        /// <param name="b">The above samples.</param>
        /// <param name="c">The above-right samples.</param>
        /// <param name="d">The left samples.</param>
        /// <param name="e">The center samples.</param>
        /// <param name="f">The right samples.</param>
        /// <param name="g">The below-left samples.</param>
        /// <param name="h">The below samples.</param>
        /// <param name="i">The below-right samples.</param>
        /// <param name="terms">The edge threshold and the bit-depth rounding.</param>
        /// <param name="sum">The running Laplacian total.</param>
        /// <param name="count">The running smooth-sample total.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(
            Vector512<short> a,
            Vector512<short> b,
            Vector512<short> c,
            Vector512<short> d,
            Vector512<short> e,
            Vector512<short> f,
            Vector512<short> g,
            Vector512<short> h,
            Vector512<short> i,
            in NoiseTerms terms,
            ref Vector512<int> sum,
            ref Vector512<int> count)
        {
            Vector512<ushort> bias = Vector512.Create((ushort)terms.Bias);
            Vector512<short> gx = (a - c) + (g - i) + ((d - f) << 1);
            Vector512<short> gy = (a - g) + (c - i) + ((b - h) << 1);
            Vector512<ushort> gradient = Vector512.ShiftRightLogical((Vector512.Abs(gx) + Vector512.Abs(gy)).AsUInt16() + bias, terms.Shift);
            Vector512<short> smooth = Vector512.LessThan(gradient.AsInt16(), Vector512.Create((short)terms.EdgeThreshold));
            Vector512<short> laplacian = (e << 2) - ((b + d + f + h) << 1) + (a + c + g + i);
            Vector512<short> magnitude = Vector512.ShiftRightLogical(Vector512.Abs(laplacian).AsUInt16() + bias, terms.Shift).AsInt16() & smooth;
            (Vector512<int> magnitudeLower, Vector512<int> magnitudeUpper) = Vector512.Widen(magnitude);
            (Vector512<int> smoothLower, Vector512<int> smoothUpper) = Vector512.Widen(smooth);
            sum += magnitudeLower + magnitudeUpper;
            count -= smoothLower + smoothUpper;
        }

        /// <summary>
        /// Adds the Laplacian magnitude of one sample when its gradient marks it as smooth.
        /// </summary>
        /// <param name="a">The above-left sample.</param>
        /// <param name="b">The above sample.</param>
        /// <param name="c">The above-right sample.</param>
        /// <param name="d">The left sample.</param>
        /// <param name="e">The center sample.</param>
        /// <param name="f">The right sample.</param>
        /// <param name="g">The below-left sample.</param>
        /// <param name="h">The below sample.</param>
        /// <param name="i">The below-right sample.</param>
        /// <param name="terms">The edge threshold and the bit-depth rounding.</param>
        /// <param name="sum">The running Laplacian total.</param>
        /// <param name="count">The running smooth-sample total.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AccumulateNoise(int a, int b, int c, int d, int e, int f, int g, int h, int i, in NoiseTerms terms, ref int sum, ref int count)
        {
            int gx = (a - c) + (g - i) + (2 * (d - f));
            int gy = (a - g) + (c - i) + (2 * (b - h));
            int gradient = (Math.Abs(gx) + Math.Abs(gy) + terms.Bias) >> terms.Shift;
            if (gradient < terms.EdgeThreshold)
            {
                int laplacian = (4 * e) - (2 * (b + d + f + h)) + (a + c + g + i);
                sum += (Math.Abs(laplacian) + terms.Bias) >> terms.Shift;
                count++;
            }
        }

        /// <summary>
        /// Stores the sums of five rows of four values.
        /// </summary>
        /// <param name="row0">The first value of the top row.</param>
        /// <param name="row1">The first value of the second row.</param>
        /// <param name="row2">The first value of the center row.</param>
        /// <param name="row3">The first value of the fourth row.</param>
        /// <param name="row4">The first value of the bottom row.</param>
        /// <param name="destination">The first sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector128<uint> lanes)
            => (Vector128.LoadUnsafe(ref row0) + Vector128.LoadUnsafe(ref row1) + Vector128.LoadUnsafe(ref row2)
                + Vector128.LoadUnsafe(ref row3) + Vector128.LoadUnsafe(ref row4)).StoreUnsafe(ref destination);

        /// <summary>
        /// Stores the sums of five rows of eight values.
        /// </summary>
        /// <param name="row0">The first value of the top row.</param>
        /// <param name="row1">The first value of the second row.</param>
        /// <param name="row2">The first value of the center row.</param>
        /// <param name="row3">The first value of the fourth row.</param>
        /// <param name="row4">The first value of the bottom row.</param>
        /// <param name="destination">The first sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector256<uint> lanes)
            => (Vector256.LoadUnsafe(ref row0) + Vector256.LoadUnsafe(ref row1) + Vector256.LoadUnsafe(ref row2)
                + Vector256.LoadUnsafe(ref row3) + Vector256.LoadUnsafe(ref row4)).StoreUnsafe(ref destination);

        /// <summary>
        /// Stores the sums of five rows of sixteen values.
        /// </summary>
        /// <param name="row0">The first value of the top row.</param>
        /// <param name="row1">The first value of the second row.</param>
        /// <param name="row2">The first value of the center row.</param>
        /// <param name="row3">The first value of the fourth row.</param>
        /// <param name="row4">The first value of the bottom row.</param>
        /// <param name="destination">The first sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumRows(ref uint row0, ref uint row1, ref uint row2, ref uint row3, ref uint row4, ref uint destination, Vector512<uint> lanes)
            => (Vector512.LoadUnsafe(ref row0) + Vector512.LoadUnsafe(ref row1) + Vector512.LoadUnsafe(ref row2)
                + Vector512.LoadUnsafe(ref row3) + Vector512.LoadUnsafe(ref row4)).StoreUnsafe(ref destination);

        /// <summary>
        /// Stores four window errors from five overlapping column-sum loads.
        /// </summary>
        /// <param name="columns">The column sum two columns left of the first output.</param>
        /// <param name="luma">The first luma error.</param>
        /// <param name="shift">The high-bit-depth scaling shift.</param>
        /// <param name="destination">The first window error to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector128<uint> lanes)
        {
            // Lane k adds columns k-2 to k+2 of the edge-padded row, which is the clamped five-column window.
            // A twelve-bit window of 29 squares stays below 2^29, so no lane wraps before the shift.
            Vector128<uint> window = Vector128.LoadUnsafe(ref columns) + Vector128.LoadUnsafe(ref Unsafe.Add(ref columns, 1))
                + Vector128.LoadUnsafe(ref Unsafe.Add(ref columns, 2)) + Vector128.LoadUnsafe(ref Unsafe.Add(ref columns, 3))
                + Vector128.LoadUnsafe(ref Unsafe.Add(ref columns, 4));

            Vector128.ShiftRightLogical(window + Vector128.LoadUnsafe(ref luma), shift).StoreUnsafe(ref destination);
        }

        /// <summary>
        /// Stores eight window errors from five overlapping column-sum loads.
        /// </summary>
        /// <param name="columns">The column sum two columns left of the first output.</param>
        /// <param name="luma">The first luma error.</param>
        /// <param name="shift">The high-bit-depth scaling shift.</param>
        /// <param name="destination">The first window error to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector256<uint> lanes)
        {
            Vector256<uint> window = Vector256.LoadUnsafe(ref columns) + Vector256.LoadUnsafe(ref Unsafe.Add(ref columns, 1))
                + Vector256.LoadUnsafe(ref Unsafe.Add(ref columns, 2)) + Vector256.LoadUnsafe(ref Unsafe.Add(ref columns, 3))
                + Vector256.LoadUnsafe(ref Unsafe.Add(ref columns, 4));

            Vector256.ShiftRightLogical(window + Vector256.LoadUnsafe(ref luma), shift).StoreUnsafe(ref destination);
        }

        /// <summary>
        /// Stores sixteen window errors from five overlapping column-sum loads.
        /// </summary>
        /// <param name="columns">The column sum two columns left of the first output.</param>
        /// <param name="luma">The first luma error.</param>
        /// <param name="shift">The high-bit-depth scaling shift.</param>
        /// <param name="destination">The first window error to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, ref uint luma, int shift, ref uint destination, Vector512<uint> lanes)
        {
            Vector512<uint> window = Vector512.LoadUnsafe(ref columns) + Vector512.LoadUnsafe(ref Unsafe.Add(ref columns, 1))
                + Vector512.LoadUnsafe(ref Unsafe.Add(ref columns, 2)) + Vector512.LoadUnsafe(ref Unsafe.Add(ref columns, 3))
                + Vector512.LoadUnsafe(ref Unsafe.Add(ref columns, 4));

            Vector512.ShiftRightLogical(window + Vector512.LoadUnsafe(ref luma), shift).StoreUnsafe(ref destination);
        }

        /// <summary>
        /// Stores one window error from five adjacent column sums.
        /// </summary>
        /// <param name="columns">The column sum two columns left of the output.</param>
        /// <param name="luma">The luma error.</param>
        /// <param name="shift">The high-bit-depth scaling shift.</param>
        /// <param name="destination">The window error to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumWindow(ref uint columns, uint luma, int shift, ref uint destination)
            => destination = (columns + Unsafe.Add(ref columns, 1) + Unsafe.Add(ref columns, 2) + Unsafe.Add(ref columns, 3)
                + Unsafe.Add(ref columns, 4) + luma) >> shift;

        /// <summary>
        /// Stores four sums of adjacent value pairs.
        /// </summary>
        /// <param name="row">The first value.</param>
        /// <param name="destination">The first pair sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaPairs(ref uint row, ref uint destination, Vector128<uint> lanes)
            => SumPairs(Vector128.LoadUnsafe(ref row), Vector128.LoadUnsafe(ref Unsafe.Add(ref row, 4))).StoreUnsafe(ref destination);

        /// <summary>
        /// Stores eight sums of adjacent value pairs.
        /// </summary>
        /// <param name="row">The first value.</param>
        /// <param name="destination">The first pair sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaPairs(ref uint row, ref uint destination, Vector256<uint> lanes)
            => SumPairs(Vector256.LoadUnsafe(ref row), Vector256.LoadUnsafe(ref Unsafe.Add(ref row, 8))).StoreUnsafe(ref destination);

        /// <summary>
        /// Stores sixteen sums of adjacent value pairs.
        /// </summary>
        /// <param name="row">The first value.</param>
        /// <param name="destination">The first pair sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaPairs(ref uint row, ref uint destination, Vector512<uint> lanes)
            => SumPairs(Vector512.LoadUnsafe(ref row), Vector512.LoadUnsafe(ref Unsafe.Add(ref row, 16))).StoreUnsafe(ref destination);

        /// <summary>
        /// Stores four sums of two-by-two value blocks.
        /// </summary>
        /// <param name="upper">The first value of the upper row.</param>
        /// <param name="lower">The first value of the lower row.</param>
        /// <param name="destination">The first block sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector128<uint> lanes)
            => SumPairs(
                Vector128.LoadUnsafe(ref upper) + Vector128.LoadUnsafe(ref lower),
                Vector128.LoadUnsafe(ref Unsafe.Add(ref upper, 4)) + Vector128.LoadUnsafe(ref Unsafe.Add(ref lower, 4))).StoreUnsafe(ref destination);

        /// <summary>
        /// Stores eight sums of two-by-two value blocks.
        /// </summary>
        /// <param name="upper">The first value of the upper row.</param>
        /// <param name="lower">The first value of the lower row.</param>
        /// <param name="destination">The first block sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector256<uint> lanes)
            => SumPairs(
                Vector256.LoadUnsafe(ref upper) + Vector256.LoadUnsafe(ref lower),
                Vector256.LoadUnsafe(ref Unsafe.Add(ref upper, 8)) + Vector256.LoadUnsafe(ref Unsafe.Add(ref lower, 8))).StoreUnsafe(ref destination);

        /// <summary>
        /// Stores sixteen sums of two-by-two value blocks.
        /// </summary>
        /// <param name="upper">The first value of the upper row.</param>
        /// <param name="lower">The first value of the lower row.</param>
        /// <param name="destination">The first block sum to write.</param>
        /// <param name="lanes">The overload-selection value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SumLumaQuads(ref uint upper, ref uint lower, ref uint destination, Vector512<uint> lanes)
            => SumPairs(
                Vector512.LoadUnsafe(ref upper) + Vector512.LoadUnsafe(ref lower),
                Vector512.LoadUnsafe(ref Unsafe.Add(ref upper, 16)) + Vector512.LoadUnsafe(ref Unsafe.Add(ref lower, 16))).StoreUnsafe(ref destination);

        /// <summary>
        /// Returns the ordered sums of adjacent lane pairs of two vectors.
        /// </summary>
        /// <param name="first">The first eight values: four pairs.</param>
        /// <param name="second">The next eight values: four pairs.</param>
        /// <returns>The four pair sums of <paramref name="first"/> followed by those of <paramref name="second"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<uint> SumPairs(Vector128<uint> first, Vector128<uint> second)
        {
            // Viewed as sixty-four-bit lanes, a pair is (odd << 32) | even on a little-endian machine. The sum of the
            // two halves is below 2^32 because a four-sample twelve-bit luma sum is below 2^26, so the truncating
            // narrow returns it.
            Vector128<ulong> low = Vector128.Create(0xFFFFFFFFUL);
            Vector128<ulong> firstPairs = (first.AsUInt64() & low) + (first.AsUInt64() >>> 32);
            Vector128<ulong> secondPairs = (second.AsUInt64() & low) + (second.AsUInt64() >>> 32);
            return Vector128.Narrow(firstPairs, secondPairs);
        }

        /// <summary>
        /// Returns the ordered sums of adjacent lane pairs of two vectors.
        /// </summary>
        /// <param name="first">The first sixteen values: eight pairs.</param>
        /// <param name="second">The next sixteen values: eight pairs.</param>
        /// <returns>The eight pair sums of <paramref name="first"/> followed by those of <paramref name="second"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<uint> SumPairs(Vector256<uint> first, Vector256<uint> second)
        {
            Vector256<ulong> low = Vector256.Create(0xFFFFFFFFUL);
            Vector256<ulong> firstPairs = (first.AsUInt64() & low) + (first.AsUInt64() >>> 32);
            Vector256<ulong> secondPairs = (second.AsUInt64() & low) + (second.AsUInt64() >>> 32);
            return Vector256.Narrow(firstPairs, secondPairs);
        }

        /// <summary>
        /// Returns the ordered sums of adjacent lane pairs of two vectors.
        /// </summary>
        /// <param name="first">The first thirty-two values: sixteen pairs.</param>
        /// <param name="second">The next thirty-two values: sixteen pairs.</param>
        /// <returns>The sixteen pair sums of <paramref name="first"/> followed by those of <paramref name="second"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<uint> SumPairs(Vector512<uint> first, Vector512<uint> second)
        {
            Vector512<ulong> low = Vector512.Create(0xFFFFFFFFUL);
            Vector512<ulong> firstPairs = (first.AsUInt64() & low) + (first.AsUInt64() >>> 32);
            Vector512<ulong> secondPairs = (second.AsUInt64() & low) + (second.AsUInt64() >>> 32);
            return Vector512.Narrow(firstPairs, secondPairs);
        }

        /// <summary>
        /// Converts two zero-extended thirty-two-bit integers to <see cref="double"/> exactly.
        /// </summary>
        /// <param name="values">The integers, each below 2^32.</param>
        /// <returns>The converted values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<double> ToDouble(Vector128<ulong> values)
            => (values | Vector128.Create(DoubleExponentBits)).AsDouble() - Vector128.Create(DoubleExponentValue);

        /// <summary>
        /// Converts four zero-extended thirty-two-bit integers to <see cref="double"/> exactly.
        /// </summary>
        /// <param name="values">The integers, each below 2^32.</param>
        /// <returns>The converted values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<double> ToDouble(Vector256<ulong> values)
            => (values | Vector256.Create(DoubleExponentBits)).AsDouble() - Vector256.Create(DoubleExponentValue);

        /// <summary>
        /// Converts eight zero-extended thirty-two-bit integers to <see cref="double"/> exactly.
        /// </summary>
        /// <param name="values">The integers, each below 2^32.</param>
        /// <returns>The converted values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<double> ToDouble(Vector512<ulong> values)
            => (values | Vector512.Create(DoubleExponentBits)).AsDouble() - Vector512.Create(DoubleExponentValue);

        /// <summary>
        /// Returns the scaled errors of two samples: the window error and the block error combined, then multiplied by the sub-block factors.
        /// </summary>
        /// <param name="windowErrors">The window errors.</param>
        /// <param name="terms">The sub-block error terms.</param>
        /// <returns>The scaled errors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<double> ScaleErrors(Vector128<double> windowErrors, in WeightTerms terms)
        {
            // Each multiplication and addition rounds separately. .NET does not contract them into fused multiply-adds, so the
            // result is the same as the scalar overload.
            Vector128<double> combined = (windowErrors * terms.InverseReferenceCount * CombinedWindowWeight) + Vector128.Create(terms.BlockError);
            return combined * terms.FirstFactor * terms.SecondFactor;
        }

        /// <summary>
        /// Returns the scaled errors of four samples.
        /// </summary>
        /// <param name="windowErrors">The window errors.</param>
        /// <param name="terms">The sub-block error terms.</param>
        /// <returns>The scaled errors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<double> ScaleErrors(Vector256<double> windowErrors, in WeightTerms terms)
        {
            Vector256<double> combined = (windowErrors * terms.InverseReferenceCount * CombinedWindowWeight) + Vector256.Create(terms.BlockError);
            return combined * terms.FirstFactor * terms.SecondFactor;
        }

        /// <summary>
        /// Returns the scaled errors of eight samples.
        /// </summary>
        /// <param name="windowErrors">The window errors.</param>
        /// <param name="terms">The sub-block error terms.</param>
        /// <returns>The scaled errors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<double> ScaleErrors(Vector512<double> windowErrors, in WeightTerms terms)
        {
            Vector512<double> combined = (windowErrors * terms.InverseReferenceCount * CombinedWindowWeight) + Vector512.Create(terms.BlockError);
            return combined * terms.FirstFactor * terms.SecondFactor;
        }

        /// <summary>
        /// Returns the filter weights of four scaled errors with the approximated exponential.
        /// </summary>
        /// <param name="scaledErrors">The scaled errors in single precision.</param>
        /// <returns>The integer weights, each at most <see cref="WeightScale"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> GetWeights(Vector128<float> scaledErrors)
        {
            // The approximated exponential multiplies the negated error by 2^23 / ln(2), truncates, adds the biased exponent and
            // reinterprets the integer as a float. The weight then truncates after the addition of one half. For the nonnegative
            // exponential, this rounds half up. Every value is far inside the int32 range, so the native conversion truncates
            // in the same way as the scalar casts.
            Vector128<float> negated = Vector128<float>.Zero - Vector128.Min(scaledErrors, Vector128.Create(MaximumScaledError));
            Vector128<int> exponent = Vector128.ConvertToInt32Native(negated * ExponentMultiplier) + Vector128.Create(ExponentOffset);
            return Vector128.ConvertToInt32Native((exponent.AsSingle() * WeightScale) + Vector128.Create(0.5f));
        }

        /// <summary>
        /// Returns the filter weights of eight scaled errors with the approximated exponential.
        /// </summary>
        /// <param name="scaledErrors">The scaled errors in single precision.</param>
        /// <returns>The integer weights, each at most <see cref="WeightScale"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> GetWeights(Vector256<float> scaledErrors)
        {
            Vector256<float> negated = Vector256<float>.Zero - Vector256.Min(scaledErrors, Vector256.Create(MaximumScaledError));
            Vector256<int> exponent = Vector256.ConvertToInt32Native(negated * ExponentMultiplier) + Vector256.Create(ExponentOffset);
            return Vector256.ConvertToInt32Native((exponent.AsSingle() * WeightScale) + Vector256.Create(0.5f));
        }

        /// <summary>
        /// Adds four weights and weighted predictions to the accumulators.
        /// </summary>
        /// <param name="weights">The integer weights.</param>
        /// <param name="predictions">The widened predicted samples.</param>
        /// <param name="accumulator">The first weighted sum to update.</param>
        /// <param name="count">The first weight total to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddWeights(Vector128<int> weights, Vector128<uint> predictions, ref uint accumulator, ref ushort count)
        {
            ref byte countBytes = ref Unsafe.As<ushort, byte>(ref count);
            Vector128<ushort> counts = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref countBytes)).AsUInt16();
            Vector128<ushort> narrowed = Vector128.Narrow(weights.AsUInt32(), weights.AsUInt32());
            Unsafe.WriteUnaligned(ref countBytes, (counts + narrowed).AsUInt64().ToScalar());
            (Vector128.LoadUnsafe(ref accumulator) + (weights.AsUInt32() * predictions)).StoreUnsafe(ref accumulator);
        }

        /// <summary>
        /// Returns the rounded-down quotients of two exactly represented integer vectors.
        /// </summary>
        /// <param name="numerators">The numerators, each below 2^32.</param>
        /// <param name="denominators">The denominators, each nonzero and below 2^16.</param>
        /// <returns>The integer quotients in the low thirty-two bits of each lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<ulong> Divide(Vector128<double> numerators, Vector128<double> denominators)
        {
            // A non-integer quotient n / d lies at least 1/d >= 2^-16 from the nearest integers, while the rounding
            // error of the division is below 2^-21 for quotients below 2^32, so the floor of the rounded quotient is
            // the integer quotient. Adding 2^52 then places that integer in the low mantissa bits.
            return (Vector128.Floor(numerators / denominators) + Vector128.Create(DoubleExponentValue)).AsUInt64();
        }

        /// <summary>
        /// Returns the rounded-down quotients of two exactly represented integer vectors.
        /// </summary>
        /// <param name="numerators">The numerators, each below 2^32.</param>
        /// <param name="denominators">The denominators, each nonzero and below 2^16.</param>
        /// <returns>The integer quotients in the low thirty-two bits of each lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<ulong> Divide(Vector256<double> numerators, Vector256<double> denominators)
            => (Vector256.Floor(numerators / denominators) + Vector256.Create(DoubleExponentValue)).AsUInt64();

        /// <summary>
        /// Returns the rounded-down quotients of two exactly represented integer vectors.
        /// </summary>
        /// <param name="numerators">The numerators, each below 2^32.</param>
        /// <param name="denominators">The denominators, each nonzero and below 2^16.</param>
        /// <returns>The integer quotients in the low thirty-two bits of each lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<ulong> Divide(Vector512<double> numerators, Vector512<double> denominators)
            => (Vector512.Floor(numerators / denominators) + Vector512.Create(DoubleExponentValue)).AsUInt64();
    }
}
