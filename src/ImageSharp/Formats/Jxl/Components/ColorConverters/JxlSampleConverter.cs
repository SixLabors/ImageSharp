// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

/// <summary>
/// Converts between modular integer carriers and their declared integer or floating-point samples.
/// </summary>
/// <remarks>
/// The image header owns precision validation. Floating-point decode preserves signed zero, nonfinite encodings and finite HDR values.
/// Encoding rounds samples to the declared precision. Integer encoding bounds samples to the unsigned interval [0,1].
/// Pixel conversion applies destination bounds after decoding. The operators can also join an existing planar traversal without extra rows.
/// </remarks>
internal static class JxlSampleConverter
{
    /// <summary>
    /// Converts scalar samples and populated SIMD lanes between the declared source and destination representations.
    /// </summary>
    /// <typeparam name="TSource">The source component or modular carrier type.</typeparam>
    /// <typeparam name="TDestination">The destination component or modular carrier type.</typeparam>
    public interface IConversionOperator<TSource, TDestination>
        where TSource : unmanaged
        where TDestination : unmanaged
    {
        /// <summary>
        /// Converts one source value.
        /// </summary>
        /// <param name="value">The source value in the operator's declared units.</param>
        /// <returns>The converted value.</returns>
        TDestination Invoke(TSource value);

        /// <summary>
        /// Converts populated source lanes without changing their order.
        /// </summary>
        /// <param name="value">Source values in the low lanes. A narrower carrier uses only the lanes needed by the destination.</param>
        /// <returns>The converted values in the corresponding destination lanes.</returns>
        Vector128<TDestination> Invoke(Vector128<TSource> value);

        /// <summary>
        /// Converts populated source lanes without changing their order.
        /// </summary>
        /// <param name="value">Source values in the low lanes. A narrower carrier uses only the lanes needed by the destination.</param>
        /// <returns>The converted values in the corresponding destination lanes.</returns>
        Vector256<TDestination> Invoke(Vector256<TSource> value);

        /// <summary>
        /// Converts populated source lanes without changing their order.
        /// </summary>
        /// <param name="value">Source values in the low lanes. A narrower carrier uses only the lanes needed by the destination.</param>
        /// <returns>The converted values in the corresponding destination lanes.</returns>
        Vector512<TDestination> Invoke(Vector512<TSource> value);
    }

    /// <summary>
    /// Packs normalized integer samples or floating-point bit patterns into their declared modular carriers.
    /// </summary>
    /// <param name="source">The component samples in their encoded color space.</param>
    /// <param name="destination">Signed modular carrier storage with at least the source length.</param>
    /// <param name="bitDepth">The validated precision and floating-point layout for this component.</param>
    public static void Encode(ReadOnlySpan<float> source, Span<int> destination, JxlBitDepth bitDepth)
    {
        if (bitDepth.ExponentBits == 0)
        {
            // From 23 declared bits onward, widen the multiplication to retain the integer endpoint and rounding precision.
            // The storage carrier remains int; its size does not select the encoded precision.
            if (bitDepth.BitsPerSample < 23)
            {
                Encode(source, destination, new IntegerEncodingOperator(bitDepth.BitsPerSample));
            }
            else
            {
                Encode(source, destination, new AccurateIntegerEncodingOperator(bitDepth.BitsPerSample));
            }
        }
        else if (bitDepth.BitsPerSample == 32)
        {
            // A binary32 carrier is its complete IEEE bit pattern. Span copying preserves signed zero and NaN payloads.
            MemoryMarshal.Cast<float, int>(source).CopyTo(destination);
        }
        else if (bitDepth.BitsPerSample == 16 && bitDepth.ExponentBits == 5)
        {
            Encode(source, destination, default(HalfEncodingOperator));
        }
        else if (bitDepth.ExponentBits == 8)
        {
            Encode(source, destination, new Exponent8EncodingOperator(bitDepth.BitsPerSample));
        }
        else
        {
            Encode(source, destination, new CustomFloatEncodingOperator(bitDepth));
        }
    }

    /// <summary>
    /// Converts a complete component row with one sample representation selected before traversal.
    /// </summary>
    /// <param name="source">Reconstructed signed integer carriers, including raw bits for floating-point samples.</param>
    /// <param name="destination">Float storage with at least the source length.</param>
    /// <param name="bitDepth">The validated integer or floating-point sample layout.</param>
    /// <param name="imageBitDepth">The main image precision that selects the integer normalization arithmetic.</param>
    public static void Convert(ReadOnlySpan<int> source, Span<float> destination, JxlBitDepth bitDepth, JxlBitDepth imageBitDepth)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        if (bitDepth.ExponentBits == 0)
        {
            // dec_modular.cc selects normalization from the main image precision, including for extra
            // channels with a different depth. The channel depth still determines its normalization factor.
            // From 23 main-image bits onward, multiply by the double factor before rounding once to float.
            if (imageBitDepth.BitsPerSample < 23)
            {
                Convert(source, destination, new IntegerOperator(bitDepth.BitsPerSample));
            }
            else
            {
                Convert(source, destination, new AccurateIntegerOperator(bitDepth.BitsPerSample));
            }
        }
        else if (bitDepth.BitsPerSample == 32)
        {
            Convert(source, destination, default(Binary32Operator));
        }
        else if (bitDepth.ExponentBits == 8)
        {
            Convert(source, destination, new Exponent8Operator(bitDepth.BitsPerSample));
        }
        else
        {
            Convert(source, destination, new CustomFloatOperator(bitDepth));
        }
    }

    /// <summary>
    /// Converts a component row retained in signed 16-bit modular storage.
    /// </summary>
    /// <param name="source">The short carriers promised by the image's modular storage flag.</param>
    /// <param name="destination">Float storage with at least the source length.</param>
    /// <param name="bitDepth">The validated integer or floating-point sample layout.</param>
    /// <param name="imageBitDepth">The main image precision that selects the integer normalization arithmetic.</param>
    public static void Convert(ReadOnlySpan<short> source, Span<float> destination, JxlBitDepth bitDepth, JxlBitDepth imageBitDepth)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        if (bitDepth.ExponentBits == 0)
        {
            // Short storage uses the same main-image precision rule as int storage. In particular,
            // a narrow extra channel in a 32-bit image must retain the accurate factor multiplication.
            if (imageBitDepth.BitsPerSample < 23)
            {
                Convert(source, destination, new Int16Operator<IntegerOperator>(new IntegerOperator(bitDepth.BitsPerSample)));
            }
            else
            {
                Convert(source, destination, new Int16Operator<AccurateIntegerOperator>(new AccurateIntegerOperator(bitDepth.BitsPerSample)));
            }
        }
        else if (bitDepth.BitsPerSample == 32)
        {
            Convert(source, destination, default(Int16Operator<Binary32Operator>));
        }
        else if (bitDepth.ExponentBits == 8)
        {
            Convert(source, destination, new Int16Operator<Exponent8Operator>(new Exponent8Operator(bitDepth.BitsPerSample)));
        }
        else
        {
            Convert(source, destination, new Int16Operator<CustomFloatOperator>(new CustomFloatOperator(bitDepth)));
        }
    }

    /// <summary>
    /// Traverses independent carriers with a prepared representation operator.
    /// </summary>
    /// <typeparam name="TSample">The signed short or int carrier type.</typeparam>
    /// <typeparam name="TOperator">The selected sample arithmetic.</typeparam>
    /// <param name="source">The reconstructed carriers.</param>
    /// <param name="destination">The validated destination row.</param>
    /// <param name="operation">The prepared factors or floating-point bit fields.</param>
    public static void Convert<TSample, TOperator>(ReadOnlySpan<TSample> source, Span<float> destination, TOperator operation)
        where TSample : unmanaged
        where TOperator : struct, IConversionOperator<TSample, float>
    {
        ref float output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // Each register produces one float per column. Short loads populate only the low half;
        // the storage operator widens those samples before applying the same integer or float equations.
        // Counts therefore follow float lanes, not the number of shorts in a full input register.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                operation.Invoke(Load512(source, i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                operation.Invoke(Load256(source, i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                operation.Invoke(Load128(source, i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        // The same operator handles the remaining samples, including a complete scalar fallback.
        for (; i < source.Length; i++)
        {
            destination[i] = operation.Invoke(source[i]);
        }
    }

    /// <summary>
    /// Loads the carriers for 4 consecutive float outputs without reading beyond the source row.
    /// </summary>
    /// <typeparam name="TSample">Short, int or float, selected by the calling conversion worker.</typeparam>
    /// <param name="source">The ready source row.</param>
    /// <param name="index">The first of 4 validated columns.</param>
    /// <returns>A register with 4 populated carriers in its low lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<TSample> Load128<TSample>(ReadOnlySpan<TSample> source, int index)
        where TSample : unmanaged
    {
        ref TSample first = ref MemoryMarshal.GetReference(source);
        if (typeof(TSample) == typeof(short))
        {
            // A short occupies half a float's storage. The matching operator consumes only these
            // low 4 lanes, so a full output block reads exactly its 4 source samples.
            return Vector64.LoadUnsafe(ref first, (nuint)index).ToVector128();
        }

        return Vector128.LoadUnsafe(ref first, (nuint)index);
    }

    /// <summary>
    /// Loads the carriers for 8 consecutive float outputs without reading beyond the source row.
    /// </summary>
    /// <typeparam name="TSample">Short, int or float, selected by the calling conversion worker.</typeparam>
    /// <param name="source">The ready source row.</param>
    /// <param name="index">The first of 8 validated columns.</param>
    /// <returns>A register with 8 populated carriers in its low lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<TSample> Load256<TSample>(ReadOnlySpan<TSample> source, int index)
        where TSample : unmanaged
    {
        ref TSample first = ref MemoryMarshal.GetReference(source);
        if (typeof(TSample) == typeof(short))
        {
            // A short occupies half a float's storage. The matching operator consumes only these
            // low 8 lanes, so a full output block reads exactly its 8 source samples.
            return Vector128.LoadUnsafe(ref first, (nuint)index).ToVector256();
        }

        return Vector256.LoadUnsafe(ref first, (nuint)index);
    }

    /// <summary>
    /// Loads the carriers for 16 consecutive float outputs without reading beyond the source row.
    /// </summary>
    /// <typeparam name="TSample">Short, int or float, selected by the calling conversion worker.</typeparam>
    /// <param name="source">The ready source row.</param>
    /// <param name="index">The first of 16 validated columns.</param>
    /// <returns>A register with 16 populated carriers in its low lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<TSample> Load512<TSample>(ReadOnlySpan<TSample> source, int index)
        where TSample : unmanaged
    {
        ref TSample first = ref MemoryMarshal.GetReference(source);
        if (typeof(TSample) == typeof(short))
        {
            // A short occupies half a float's storage. The matching operator consumes only these
            // low 16 lanes, so a full output block reads exactly its 16 source samples.
            return Vector256.LoadUnsafe(ref first, (nuint)index).ToVector512();
        }

        return Vector512.LoadUnsafe(ref first, (nuint)index);
    }

    /// <summary>
    /// Traverses one component row with its precision-dependent packing selected before the loop.
    /// </summary>
    /// <typeparam name="TOperator">The integer quantization or floating-point bit-packing operation.</typeparam>
    /// <param name="source">The source component samples.</param>
    /// <param name="destination">The validated carrier storage.</param>
    /// <param name="operation">The prepared scale or floating-point layout.</param>
    public static void Encode<TOperator>(ReadOnlySpan<float> source, Span<int> destination, TOperator operation)
        where TOperator : struct, IConversionOperator<float, int>
    {
        ref float input = ref MemoryMarshal.GetReference(source);
        ref int output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // Each input float lane produces one int carrier. Unaligned loads preserve row order and do not need padding.
        // The same prepared operation handles every vector width and the final scalar remainder.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector512Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                operation.Invoke(Vector512.LoadUnsafe(ref input, (nuint)i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector256Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                operation.Invoke(Vector256.LoadUnsafe(ref input, (nuint)i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector128Count<float>();

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                operation.Invoke(Vector128.LoadUnsafe(ref input, (nuint)i)).StoreUnsafe(ref output, (nuint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            destination[i] = operation.Invoke(source[i]);
        }
    }

    /// <summary>
    /// Quantizes signed coding components with the scale declared by their reconstruction factors.
    /// </summary>
    public readonly struct ScaledIntegerEncodingOperator : IConversionOperator<float, int>
    {
        /// <summary>
        /// The reciprocal reconstruction step, prepared from the serialized quantizer.
        /// </summary>
        private readonly float factor;

        /// <summary>
        /// The source sign bit for signed midpoint rounding, or zero for a fixed positive half offset.
        /// </summary>
        private readonly uint signMask;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScaledIntegerEncodingOperator"/> struct.
        /// </summary>
        /// <param name="factor">The reciprocal reconstruction step for this component.</param>
        /// <param name="signedRounding">Whether the half offset follows the source sign.</param>
        public ScaledIntegerEncodingOperator(float factor, bool signedRounding)
        {
            this.factor = factor;
            this.signMask = signedRounding ? 0x80000000U : 0;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Invoke(float value)
        {
            // XYB X and Y round away from zero at a midpoint. B uses a positive half before its Y residual is removed.
            // The caller selects that component rule during setup. A sign mask keeps both cases free of sample branches.
            uint half = 0x3F000000U | (BitConverter.SingleToUInt32Bits(value) & this.signMask);

            return (int)((value * this.factor) + BitConverter.UInt32BitsToSingle(half));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector128<float> value)
        {
            // Each lane contains one component from a consecutive pixel. Scale first, add the selected half,
            // then truncate exactly as the scalar operation does. Lane order remains unchanged.
            Vector128<uint> half = Vector128.Create(0x3F000000U) | (value.AsUInt32() & Vector128.Create(this.signMask));

            return Vector128.ConvertToInt32((value * Vector128.Create(this.factor)) + half.AsSingle());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> Invoke(Vector256<float> value)
        {
            // Each lane contains one component from a consecutive pixel. Scale first, add the selected half,
            // then truncate exactly as the scalar operation does. Lane order remains unchanged.
            Vector256<uint> half = Vector256.Create(0x3F000000U) | (value.AsUInt32() & Vector256.Create(this.signMask));

            return Vector256.ConvertToInt32((value * Vector256.Create(this.factor)) + half.AsSingle());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<int> Invoke(Vector512<float> value)
        {
            // Each lane contains one component from a consecutive pixel. Scale first, add the selected half,
            // then truncate exactly as the scalar operation does. Lane order remains unchanged.
            Vector512<uint> half = Vector512.Create(0x3F000000U) | (value.AsUInt32() & Vector512.Create(this.signMask));

            return Vector512.ConvertToInt32((value * Vector512.Create(this.factor)) + half.AsSingle());
        }
    }

    /// <summary>
    /// Quantizes signed coding components with double-precision multiplication and a final integer conversion.
    /// </summary>
    /// <remarks>
    /// Native modular XYB uses this precision for X and Y at metadata depths of 23 bits or more, and for B at every depth.
    /// The scale still comes from the serialized single-precision reconstruction step. Widen it exactly before sample traversal.
    /// </remarks>
    public readonly struct AccurateScaledIntegerEncodingOperator : IConversionOperator<float, int>
    {
        /// <summary>
        /// The single-precision reciprocal reconstruction step, widened exactly during setup.
        /// </summary>
        private readonly double factor;

        /// <summary>
        /// The binary64 sign bit for signed midpoint rounding, or zero for a fixed positive half offset.
        /// </summary>
        private readonly ulong signMask;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccurateScaledIntegerEncodingOperator"/> struct.
        /// </summary>
        /// <param name="factor">The positive reciprocal reconstruction step, whose scaled samples fit signed integer carriers.</param>
        /// <param name="signedRounding">Whether the half offset follows the source sign.</param>
        public AccurateScaledIntegerEncodingOperator(float factor, bool signedRounding)
        {
            this.factor = factor;
            this.signMask = signedRounding ? 0x8000000000000000UL : 0;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Invoke(float value)
        {
            // Widen the sample before multiplication. A signed half rounds X and Y away from zero at midpoints;
            // B uses a positive half at every sign. Truncation occurs only after the double-precision addition.
            ulong half = 0x3FE0000000000000UL | (BitConverter.DoubleToUInt64Bits(value) & this.signMask);

            return (int)((value * this.factor) + BitConverter.UInt64BitsToDouble(half));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector128<float> value)
        {
            // Each float lane is one component from a consecutive pixel. Widen both halves before multiplication;
            // the prepared mask selects the scalar half-offset rule independently for every source lane.
            (Vector128<double> lower, Vector128<double> upper) = Vector128.Widen(value);
            Vector128<double> multiplier = Vector128.Create(this.factor);
            Vector128<ulong> sign = Vector128.Create(this.signMask);
            Vector128<ulong> half = Vector128.Create(0x3FE0000000000000UL);
            Vector128<double> lowerHalf = (half | (lower.AsUInt64() & sign)).AsDouble();
            Vector128<double> upperHalf = (half | (upper.AsUInt64() & sign)).AsDouble();
            lower = (lower * multiplier) + lowerHalf;
            upper = (upper * multiplier) + upperHalf;

            // Int64 conversion truncates the completed double result. The setup bounds ensure that the codes fit int;
            // narrowing then rejoins the original lane order without another numeric rounding step.
            return Vector128.Narrow(Vector128.ConvertToInt64(lower), Vector128.ConvertToInt64(upper));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> Invoke(Vector256<float> value)
        {
            // Each float lane is one component from a consecutive pixel. Widen both halves before multiplication;
            // the prepared mask selects the scalar half-offset rule independently for every source lane.
            (Vector256<double> lower, Vector256<double> upper) = Vector256.Widen(value);
            Vector256<double> multiplier = Vector256.Create(this.factor);
            Vector256<ulong> sign = Vector256.Create(this.signMask);
            Vector256<ulong> half = Vector256.Create(0x3FE0000000000000UL);
            Vector256<double> lowerHalf = (half | (lower.AsUInt64() & sign)).AsDouble();
            Vector256<double> upperHalf = (half | (upper.AsUInt64() & sign)).AsDouble();
            lower = (lower * multiplier) + lowerHalf;
            upper = (upper * multiplier) + upperHalf;

            // Int64 conversion truncates the completed double result. The setup bounds ensure that the codes fit int;
            // narrowing then rejoins the original lane order without another numeric rounding step.
            return Vector256.Narrow(Vector256.ConvertToInt64(lower), Vector256.ConvertToInt64(upper));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<int> Invoke(Vector512<float> value)
        {
            // Each float lane is one component from a consecutive pixel. Widen both halves before multiplication;
            // the prepared mask selects the scalar half-offset rule independently for every source lane.
            (Vector512<double> lower, Vector512<double> upper) = Vector512.Widen(value);
            Vector512<double> multiplier = Vector512.Create(this.factor);
            Vector512<ulong> sign = Vector512.Create(this.signMask);
            Vector512<ulong> half = Vector512.Create(0x3FE0000000000000UL);
            Vector512<double> lowerHalf = (half | (lower.AsUInt64() & sign)).AsDouble();
            Vector512<double> upperHalf = (half | (upper.AsUInt64() & sign)).AsDouble();
            lower = (lower * multiplier) + lowerHalf;
            upper = (upper * multiplier) + upperHalf;

            // Int64 conversion truncates the completed double result. The setup bounds ensure that the codes fit int;
            // narrowing then rejoins the original lane order without another numeric rounding step.
            return Vector512.Narrow(Vector512.ConvertToInt64(lower), Vector512.ConvertToInt64(upper));
        }
    }

    /// <summary>
    /// Quantizes integer samples with native single-precision scaling below 23 declared bits.
    /// </summary>
    public readonly struct IntegerEncodingOperator : IConversionOperator<float, int>
    {
        /// <summary>
        /// The largest unsigned sample code at the declared precision.
        /// </summary>
        private readonly float factor;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegerEncodingOperator"/> struct.
        /// </summary>
        /// <param name="bitsPerSample">The validated integer precision from one through 22 bits.</param>
        public IntegerEncodingOperator(int bitsPerSample) => this.factor = (1U << bitsPerSample) - 1U;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Invoke(float value) => (int)((ColorNumerics.ClampToFinite(value, 0F, 1F) * this.factor) + .5F);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector128<float> value)
        {
            // Integer samples use the unsigned interval [0,1]. Bound extended RGB before scaling so all carriers
            // fit the declared precision. Add before truncation to retain the scalar midpoint rule.
            value = ColorNumerics.ClampToFinite(value, Vector128<float>.Zero, Vector128<float>.One);
            Vector128<float> scaled = value * Vector128.Create(this.factor);

            return Vector128.ConvertToInt32(scaled + Vector128.Create(.5F));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> Invoke(Vector256<float> value)
        {
            // The bounds and add-before-truncation rule match the scalar and four-lane operations.
            value = ColorNumerics.ClampToFinite(value, Vector256<float>.Zero, Vector256<float>.One);
            Vector256<float> scaled = value * Vector256.Create(this.factor);

            return Vector256.ConvertToInt32(scaled + Vector256.Create(.5F));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<int> Invoke(Vector512<float> value)
        {
            // The bounds and add-before-truncation rule match the scalar and narrower operations.
            value = ColorNumerics.ClampToFinite(value, Vector512<float>.Zero, Vector512<float>.One);
            Vector512<float> scaled = value * Vector512.Create(this.factor);

            return Vector512.ConvertToInt32(scaled + Vector512.Create(.5F));
        }
    }

    /// <summary>
    /// Quantizes integer samples with native double-precision scaling from 23 through 31 declared bits.
    /// </summary>
    public readonly struct AccurateIntegerEncodingOperator : IConversionOperator<float, int>
    {
        /// <summary>
        /// The exact largest unsigned sample code at the declared precision.
        /// </summary>
        private readonly double factor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccurateIntegerEncodingOperator"/> struct.
        /// </summary>
        /// <param name="bitsPerSample">The validated integer precision from 23 through 31 bits.</param>
        public AccurateIntegerEncodingOperator(int bitsPerSample) => this.factor = (1U << bitsPerSample) - 1U;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Invoke(float value) => (int)((ColorNumerics.ClampToFinite(value, 0F, 1F) * this.factor) + .5D);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector128<float> value)
        {
            // Widen both halves before multiplication. The 31-bit maximum remains 2147483647 in double;
            // scaling in float would round that endpoint to 2147483648 before the integer conversion.
            value = ColorNumerics.ClampToFinite(value, Vector128<float>.Zero, Vector128<float>.One);
            (Vector128<double> lower, Vector128<double> upper) = Vector128.Widen(value);
            Vector128<double> multiplier = Vector128.Create(this.factor);
            lower *= multiplier;
            upper *= multiplier;
            Vector128<double> half = Vector128.Create(.5D);

            // Bounded inputs produce unsigned sample codes. Int64 conversion holds the maximum plus one half;
            // truncation removes the fraction before narrowing rejoins the original lane order.
            lower += half;
            upper += half;
            return Vector128.Narrow(Vector128.ConvertToInt64(lower), Vector128.ConvertToInt64(upper));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> Invoke(Vector256<float> value)
        {
            // Widen both halves before multiplication. The 31-bit maximum remains 2147483647 in double;
            // scaling in float would round that endpoint to 2147483648 before the integer conversion.
            value = ColorNumerics.ClampToFinite(value, Vector256<float>.Zero, Vector256<float>.One);
            (Vector256<double> lower, Vector256<double> upper) = Vector256.Widen(value);
            Vector256<double> multiplier = Vector256.Create(this.factor);
            lower *= multiplier;
            upper *= multiplier;
            Vector256<double> half = Vector256.Create(.5D);

            // Bounded inputs keep the truncated codes in int range. Narrowing retains the original lane order.
            lower += half;
            upper += half;
            return Vector256.Narrow(Vector256.ConvertToInt64(lower), Vector256.ConvertToInt64(upper));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<int> Invoke(Vector512<float> value)
        {
            // Widen both halves before multiplication. The 31-bit maximum remains 2147483647 in double;
            // scaling in float would round that endpoint to 2147483648 before the integer conversion.
            value = ColorNumerics.ClampToFinite(value, Vector512<float>.Zero, Vector512<float>.One);
            (Vector512<double> lower, Vector512<double> upper) = Vector512.Widen(value);
            Vector512<double> multiplier = Vector512.Create(this.factor);
            lower *= multiplier;
            upper *= multiplier;
            Vector512<double> half = Vector512.Create(.5D);

            // Bounded inputs keep the truncated codes in int range. Narrowing retains the original lane order.
            lower += half;
            upper += half;
            return Vector512.Narrow(Vector512.ConvertToInt64(lower), Vector512.ConvertToInt64(upper));
        }
    }

    /// <summary>
    /// Packs the IEEE binary16 layout through the existing half-precision storage API.
    /// </summary>
    public readonly struct HalfEncodingOperator : IConversionOperator<float, int>
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Invoke(float value) => HalfTypeHelper.Pack(value);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector128<float> value)
        {
            // Pack applies the shared binary16 rounding and special-value rules. The lower half contains
            // these input lanes. Widen only that half back to the modular int carriers in their original order.
            Vector128<ushort> packed = HalfTypeHelper.Pack(value, Vector128<float>.Zero);

            return Vector128.WidenLower(packed).AsInt32();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> Invoke(Vector256<float> value)
        {
            // Pack applies the shared binary16 rounding and special-value rules. The lower half contains
            // these input lanes. Widen only that half back to the modular int carriers in their original order.
            Vector256<ushort> packed = HalfTypeHelper.Pack(value, Vector256<float>.Zero);

            return Vector256.WidenLower(packed).AsInt32();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<int> Invoke(Vector512<float> value)
        {
            // Pack applies the shared binary16 rounding and special-value rules. The lower half contains
            // these input lanes. Widen only that half back to the modular int carriers in their original order.
            Vector512<ushort> packed = HalfTypeHelper.Pack(value, Vector512<float>.Zero);

            return Vector512.WidenLower(packed).AsInt32();
        }
    }

    /// <summary>
    /// Rounds to a shortened IEEE exponent-eight layout without changing its exponent bias or subnormal scale.
    /// </summary>
    public readonly struct Exponent8EncodingOperator : IConversionOperator<float, int>
    {
        /// <summary>
        /// The number of low binary32 mantissa bits omitted by the declared layout.
        /// </summary>
        private readonly int mantissaShift;

        /// <summary>
        /// The sign-bit position in the declared carrier.
        /// </summary>
        private readonly int signShift;

        /// <summary>
        /// One less than half a unit in the retained mantissa's lowest bit position.
        /// </summary>
        private readonly uint roundingBias;

        /// <summary>
        /// The declared layout's positive-infinity magnitude.
        /// </summary>
        private readonly uint infinity;

        /// <summary>
        /// Initializes a new instance of the <see cref="Exponent8EncodingOperator"/> struct.
        /// </summary>
        /// <param name="bitsPerSample">The validated total width with eight exponent bits and fewer than 32 total bits.</param>
        public Exponent8EncodingOperator(int bitsPerSample)
        {
            this.mantissaShift = 32 - bitsPerSample;
            this.signShift = bitsPerSample - 1;
            this.roundingBias = (1U << (this.mantissaShift - 1)) - 1U;
            this.infinity = 0x7F800000U >> this.mantissaShift;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Invoke(float value)
        {
            uint source = BitConverter.SingleToUInt32Bits(value);
            uint magnitude = source & 0x7FFFFFFFU;
            uint packed = magnitude >> this.mantissaShift;

            if (magnitude < 0x7F800000U)
            {
                // Add half an omitted unit minus one, then the retained low bit. This rounds to nearest,
                // with an exact midpoint rounded to even. A mantissa carry advances the exponent or produces infinity.
                packed = (magnitude + this.roundingBias + (packed & 1U)) >> this.mantissaShift;
            }
            else if (magnitude > 0x7F800000U && packed == this.infinity)
            {
                // Keep a nonzero NaN payload when precision reduction removes all original payload bits.
                packed |= 1U;
            }

            return (int)(packed | ((source >> 31) << this.signShift));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector128<float> value)
        {
            // Each lane holds one source bit pattern. Round finite magnitudes before moving their sign bits.
            // Special encodings retain their truncated payload, with a nonzero payload for every NaN.
            Vector128<uint> source = value.AsUInt32();
            Vector128<uint> magnitude = source & Vector128.Create(0x7FFFFFFFU);
            Vector128<uint> packed = magnitude >> this.mantissaShift;
            Vector128<uint> finite = Vector128.LessThan(magnitude, Vector128.Create(0x7F800000U));
            Vector128<uint> rounded = magnitude + Vector128.Create(this.roundingBias) + (packed & Vector128.Create(1U));
            packed = Vector128.ConditionalSelect(finite, rounded >> this.mantissaShift, packed);
            Vector128<uint> nan = Vector128.GreaterThan(magnitude, Vector128.Create(0x7F800000U));
            Vector128<uint> emptyPayload = Vector128.Equals(packed, Vector128.Create(this.infinity));
            packed |= nan & emptyPayload & Vector128.Create(1U);

            return (packed | ((source >> 31) << this.signShift)).AsInt32();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> Invoke(Vector256<float> value)
        {
            // Each lane holds one source bit pattern. Round finite magnitudes before moving their sign bits.
            // Special encodings retain their truncated payload, with a nonzero payload for every NaN.
            Vector256<uint> source = value.AsUInt32();
            Vector256<uint> magnitude = source & Vector256.Create(0x7FFFFFFFU);
            Vector256<uint> packed = magnitude >> this.mantissaShift;
            Vector256<uint> finite = Vector256.LessThan(magnitude, Vector256.Create(0x7F800000U));
            Vector256<uint> rounded = magnitude + Vector256.Create(this.roundingBias) + (packed & Vector256.Create(1U));
            packed = Vector256.ConditionalSelect(finite, rounded >> this.mantissaShift, packed);
            Vector256<uint> nan = Vector256.GreaterThan(magnitude, Vector256.Create(0x7F800000U));
            Vector256<uint> emptyPayload = Vector256.Equals(packed, Vector256.Create(this.infinity));
            packed |= nan & emptyPayload & Vector256.Create(1U);

            return (packed | ((source >> 31) << this.signShift)).AsInt32();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<int> Invoke(Vector512<float> value)
        {
            // Each lane holds one source bit pattern. Round finite magnitudes before moving their sign bits.
            // Special encodings retain their truncated payload, with a nonzero payload for every NaN.
            Vector512<uint> source = value.AsUInt32();
            Vector512<uint> magnitude = source & Vector512.Create(0x7FFFFFFFU);
            Vector512<uint> packed = magnitude >> this.mantissaShift;
            Vector512<uint> finite = Vector512.LessThan(magnitude, Vector512.Create(0x7F800000U));
            Vector512<uint> rounded = magnitude + Vector512.Create(this.roundingBias) + (packed & Vector512.Create(1U));
            packed = Vector512.ConditionalSelect(finite, rounded >> this.mantissaShift, packed);
            Vector512<uint> nan = Vector512.GreaterThan(magnitude, Vector512.Create(0x7F800000U));
            Vector512<uint> emptyPayload = Vector512.Equals(packed, Vector512.Create(this.infinity));
            packed |= nan & emptyPayload & Vector512.Create(1U);

            return (packed | ((source >> 31) << this.signShift)).AsInt32();
        }
    }

    /// <summary>
    /// Rounds floating-point layouts with a two- through seven-bit exponent and an explicit custom mantissa width.
    /// </summary>
    public readonly struct CustomFloatEncodingOperator : IConversionOperator<float, int>
    {
        /// <summary>
        /// The sign-bit position in the declared carrier.
        /// </summary>
        private readonly int signShift;

        /// <summary>
        /// The declared mantissa width in bits.
        /// </summary>
        private readonly int mantissaBits;

        /// <summary>
        /// The low binary32 mantissa width omitted by the declared layout.
        /// </summary>
        private readonly int mantissaShift;

        /// <summary>
        /// The custom exponent bias minus the binary32 bias.
        /// </summary>
        private readonly int exponentOffset;

        /// <summary>
        /// The exponent bias difference shifted to its declared carrier position.
        /// </summary>
        private readonly uint exponentAdjustment;

        /// <summary>
        /// One less than half an omitted mantissa unit, or zero when the full mantissa fits.
        /// </summary>
        private readonly uint roundingBias;

        /// <summary>
        /// One for a reduced mantissa, or zero when rounding must retain the full mantissa unchanged.
        /// </summary>
        private readonly uint roundingTieMask;

        /// <summary>
        /// The declared layout's positive-infinity magnitude.
        /// </summary>
        private readonly uint infinity;

        /// <summary>
        /// The power-of-two scale from a custom subnormal value to its integer mantissa.
        /// </summary>
        private readonly float subnormalScale;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomFloatEncodingOperator"/> struct.
        /// </summary>
        /// <param name="bitDepth">The validated floating-point layout with fewer than eight exponent bits.</param>
        public CustomFloatEncodingOperator(JxlBitDepth bitDepth)
        {
            int exponentBias = (1 << (bitDepth.ExponentBits - 1)) - 1;
            this.signShift = bitDepth.BitsPerSample - 1;
            this.mantissaBits = bitDepth.BitsPerSample - bitDepth.ExponentBits - 1;
            this.mantissaShift = 23 - this.mantissaBits;
            this.exponentOffset = exponentBias - 127;
            this.exponentAdjustment = (uint)this.exponentOffset << this.mantissaBits;
            this.roundingBias = this.mantissaShift == 0 ? 0U : (1U << (this.mantissaShift - 1)) - 1U;
            this.roundingTieMask = this.mantissaShift == 0 ? 0U : 1U;
            this.infinity = (uint)((1 << bitDepth.ExponentBits) - 1) << this.mantissaBits;

            // A subnormal equals mantissa * 2^(1-bias-mantissaBits). Its inverse power-of-two scale is exact.
            // Bias<=63 and mantissaBits<=23 bound the scale to 2^85 and the subnormal product to at most 2^23.
            this.subnormalScale = MathF.ScaleB(1F, exponentBias + this.mantissaBits - 1);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Invoke(float value)
        {
            uint source = BitConverter.SingleToUInt32Bits(value);
            uint magnitude = source & 0x7FFFFFFFU;
            uint sign = (source >> 31) << this.signShift;

            if (magnitude >= 0x7F800000U)
            {
                uint mantissa = magnitude & 0x007FFFFFU;
                uint payload = mantissa >> this.mantissaShift;
                payload |= mantissa != 0 && payload == 0 ? 1U : 0U;

                return (int)(sign | this.infinity | payload);
            }

            int exponent = (int)(magnitude >> 23) + this.exponentOffset;

            if (exponent <= 0)
            {
                // Subnormals have no implicit leading one. Scale to mantissa units and round to nearest even.
                // Underflow becomes signed zero. A carry into bit mantissaBits becomes the smallest normal value.
                float scaled = MathF.Abs(value) * this.subnormalScale;

                return (int)(sign | (uint)MathF.Round(scaled, MidpointRounding.ToEven));
            }

            // Round the binary32 magnitude before changing its exponent bias. Keep the carry into the exponent.
            // Finite overflow becomes infinity. Adding the shifted negative bias uses unsigned modular arithmetic.
            uint rounded = magnitude + this.roundingBias + ((magnitude >> this.mantissaShift) & this.roundingTieMask);
            uint packed = (rounded >> this.mantissaShift) + this.exponentAdjustment;

            return (int)(sign | Math.Min(packed, this.infinity));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector128<float> value)
        {
            // Each lane rounds one independent sample. Round normal magnitudes before changing the exponent bias,
            // so a mantissa carry advances the exponent. Clamp only the packed magnitude at the infinity encoding.
            Vector128<uint> source = value.AsUInt32();
            Vector128<uint> magnitude = source & Vector128.Create(0x7FFFFFFFU);
            Vector128<uint> mantissa = magnitude & Vector128.Create(0x007FFFFFU);
            Vector128<uint> zero = Vector128.Equals(magnitude, Vector128<uint>.Zero);
            Vector128<uint> special = Vector128.GreaterThanOrEqual(magnitude, Vector128.Create(0x7F800000U));
            Vector128<int> exponent = (magnitude >> 23).AsInt32() + Vector128.Create(this.exponentOffset);
            Vector128<uint> subnormal = Vector128.LessThanOrEqual(exponent, Vector128<int>.Zero).AsUInt32() & ~zero & ~special;
            Vector128<uint> tie = (magnitude >> this.mantissaShift) & Vector128.Create(this.roundingTieMask);
            Vector128<uint> rounded = magnitude + Vector128.Create(this.roundingBias) + tie;
            Vector128<uint> packed = (rounded >> this.mantissaShift) + Vector128.Create(this.exponentAdjustment);
            packed = Vector128.Min(packed, Vector128.Create(this.infinity));

            if (!Vector128.EqualsAll(subnormal, Vector128<uint>.Zero))
            {
                // Only subnormal lanes use this power-of-two product. Their rounded mantissas fit in 24 bits.
                // Other lanes can overflow the temporary product, but the mask excludes their converted codes.
                Vector128<float> scaled = Vector128.Abs(value) * Vector128.Create(this.subnormalScale);
                Vector128<uint> subnormalCodes = Vector128_.ConvertToInt32RoundToEven(scaled).AsUInt32();
                packed = Vector128.ConditionalSelect(subnormal, subnormalCodes, packed);
            }

            // Zero retains its sign. Infinity and NaN retain their special exponent; a narrow NaN keeps a nonzero payload.
            Vector128<uint> payload = mantissa >> this.mantissaShift;
            Vector128<uint> emptyNan = Vector128.GreaterThan(mantissa, Vector128<uint>.Zero) & Vector128.Equals(payload, Vector128<uint>.Zero);
            payload |= emptyNan & Vector128.Create(1U);
            Vector128<uint> specialCodes = Vector128.Create(this.infinity) | payload;
            packed = Vector128.ConditionalSelect(special, specialCodes, packed);
            packed = Vector128.ConditionalSelect(zero, Vector128<uint>.Zero, packed);

            return (packed | ((source >> 31) << this.signShift)).AsInt32();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> Invoke(Vector256<float> value)
        {
            // Each lane rounds one independent sample. Round normal magnitudes before changing the exponent bias,
            // so a mantissa carry advances the exponent. Clamp only the packed magnitude at the infinity encoding.
            Vector256<uint> source = value.AsUInt32();
            Vector256<uint> magnitude = source & Vector256.Create(0x7FFFFFFFU);
            Vector256<uint> mantissa = magnitude & Vector256.Create(0x007FFFFFU);
            Vector256<uint> zero = Vector256.Equals(magnitude, Vector256<uint>.Zero);
            Vector256<uint> special = Vector256.GreaterThanOrEqual(magnitude, Vector256.Create(0x7F800000U));
            Vector256<int> exponent = (magnitude >> 23).AsInt32() + Vector256.Create(this.exponentOffset);
            Vector256<uint> subnormal = Vector256.LessThanOrEqual(exponent, Vector256<int>.Zero).AsUInt32() & ~zero & ~special;
            Vector256<uint> tie = (magnitude >> this.mantissaShift) & Vector256.Create(this.roundingTieMask);
            Vector256<uint> rounded = magnitude + Vector256.Create(this.roundingBias) + tie;
            Vector256<uint> packed = (rounded >> this.mantissaShift) + Vector256.Create(this.exponentAdjustment);
            packed = Vector256.Min(packed, Vector256.Create(this.infinity));

            if (!Vector256.EqualsAll(subnormal, Vector256<uint>.Zero))
            {
                // Only subnormal lanes use this power-of-two product. Their rounded mantissas fit in 24 bits.
                // Other lanes can overflow the temporary product, but the mask excludes their converted codes.
                Vector256<float> scaled = Vector256.Abs(value) * Vector256.Create(this.subnormalScale);
                Vector256<uint> subnormalCodes = Vector256_.ConvertToInt32RoundToEven(scaled).AsUInt32();
                packed = Vector256.ConditionalSelect(subnormal, subnormalCodes, packed);
            }

            // Zero retains its sign. Infinity and NaN retain their special exponent; a narrow NaN keeps a nonzero payload.
            Vector256<uint> payload = mantissa >> this.mantissaShift;
            Vector256<uint> emptyNan = Vector256.GreaterThan(mantissa, Vector256<uint>.Zero) & Vector256.Equals(payload, Vector256<uint>.Zero);
            payload |= emptyNan & Vector256.Create(1U);
            Vector256<uint> specialCodes = Vector256.Create(this.infinity) | payload;
            packed = Vector256.ConditionalSelect(special, specialCodes, packed);
            packed = Vector256.ConditionalSelect(zero, Vector256<uint>.Zero, packed);

            return (packed | ((source >> 31) << this.signShift)).AsInt32();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<int> Invoke(Vector512<float> value)
        {
            // Each lane rounds one independent sample. Round normal magnitudes before changing the exponent bias,
            // so a mantissa carry advances the exponent. Clamp only the packed magnitude at the infinity encoding.
            Vector512<uint> source = value.AsUInt32();
            Vector512<uint> magnitude = source & Vector512.Create(0x7FFFFFFFU);
            Vector512<uint> mantissa = magnitude & Vector512.Create(0x007FFFFFU);
            Vector512<uint> zero = Vector512.Equals(magnitude, Vector512<uint>.Zero);
            Vector512<uint> special = Vector512.GreaterThanOrEqual(magnitude, Vector512.Create(0x7F800000U));
            Vector512<int> exponent = (magnitude >> 23).AsInt32() + Vector512.Create(this.exponentOffset);
            Vector512<uint> subnormal = Vector512.LessThanOrEqual(exponent, Vector512<int>.Zero).AsUInt32() & ~zero & ~special;
            Vector512<uint> tie = (magnitude >> this.mantissaShift) & Vector512.Create(this.roundingTieMask);
            Vector512<uint> rounded = magnitude + Vector512.Create(this.roundingBias) + tie;
            Vector512<uint> packed = (rounded >> this.mantissaShift) + Vector512.Create(this.exponentAdjustment);
            packed = Vector512.Min(packed, Vector512.Create(this.infinity));

            if (!Vector512.EqualsAll(subnormal, Vector512<uint>.Zero))
            {
                // Only subnormal lanes use this power-of-two product. Their rounded mantissas fit in 24 bits.
                // Other lanes can overflow the temporary product, but the mask excludes their converted codes.
                Vector512<float> scaled = Vector512.Abs(value) * Vector512.Create(this.subnormalScale);
                Vector512<uint> subnormalCodes = Vector512_.ConvertToInt32RoundToEven(scaled).AsUInt32();
                packed = Vector512.ConditionalSelect(subnormal, subnormalCodes, packed);
            }

            // Zero retains its sign. Infinity and NaN retain their special exponent; a narrow NaN keeps a nonzero payload.
            Vector512<uint> payload = mantissa >> this.mantissaShift;
            Vector512<uint> emptyNan = Vector512.GreaterThan(mantissa, Vector512<uint>.Zero) & Vector512.Equals(payload, Vector512<uint>.Zero);
            payload |= emptyNan & Vector512.Create(1U);
            Vector512<uint> specialCodes = Vector512.Create(this.infinity) | payload;
            packed = Vector512.ConditionalSelect(special, specialCodes, packed);
            packed = Vector512.ConditionalSelect(zero, Vector512<uint>.Zero, packed);

            return (packed | ((source >> 31) << this.signShift)).AsInt32();
        }
    }

    /// <summary>
    /// Widens signed 16-bit modular storage before applying an existing 32-bit carrier conversion.
    /// </summary>
    /// <typeparam name="TOperator">The existing integer normalization or floating-point carrier operation.</typeparam>
    public readonly struct Int16Operator<TOperator> : IConversionOperator<short, float>
        where TOperator : struct, IConversionOperator<int, float>
    {
        private readonly TOperator operation;

        /// <summary>
        /// Initializes a new instance of the <see cref="Int16Operator{TOperator}"/> struct.
        /// </summary>
        /// <param name="operation">The prepared operation over signed 32-bit carriers.</param>
        public Int16Operator(TOperator operation) => this.operation = operation;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(short value) => this.operation.Invoke(value);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<short> value) => this.operation.Invoke(Vector128.WidenLower(value));

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<short> value) => this.operation.Invoke(Vector256.WidenLower(value));

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<short> value) => this.operation.Invoke(Vector512.WidenLower(value));
    }

    /// <summary>
    /// Retains already-restored float samples without changing their bits or applying pixel bounds.
    /// </summary>
    public readonly struct IdentityOperator : IConversionOperator<float, float>
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value) => value;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value) => value;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value) => value;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value) => value;
    }

    /// <summary>
    /// Normalizes integer samples with single-precision multiplication when the main image has fewer than 23 bits per sample.
    /// </summary>
    public readonly struct IntegerOperator : IConversionOperator<int, float>
    {
        private readonly float factor;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegerOperator"/> struct.
        /// </summary>
        /// <param name="bitsPerSample">The channel's validated integer precision from 1 through 31 bits.</param>
        public IntegerOperator(int bitsPerSample) => this.factor = (float)(1D / ((1U << bitsPerSample) - 1U));

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(int value) => value * this.factor;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<int> value) => Vector128.ConvertToSingle(value) * Vector128.Create(this.factor);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<int> value) => Vector256.ConvertToSingle(value) * Vector256.Create(this.factor);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<int> value) => Vector512.ConvertToSingle(value) * Vector512.Create(this.factor);
    }

    /// <summary>
    /// Normalizes integer samples with one final rounding to float when the main image has at least 23 bits per sample.
    /// </summary>
    public readonly struct AccurateIntegerOperator : IConversionOperator<int, float>
    {
        private readonly double factor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccurateIntegerOperator"/> struct.
        /// </summary>
        /// <param name="bitsPerSample">The channel's validated integer precision from 1 through 31 bits.</param>
        public AccurateIntegerOperator(int bitsPerSample) => this.factor = 1D / ((1U << bitsPerSample) - 1U);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(int value) => (float)(value * this.factor);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<int> value)
        {
            // Both halves widen exactly. Narrow rejoins the products in their original lane order
            // and rounds once, matching the scalar native high-precision reconstruction boundary.
            (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(value);
            Vector128<double> multiplier = Vector128.Create(this.factor);
            return Vector128.Narrow(Vector128.ConvertToDouble(lower) * multiplier, Vector128.ConvertToDouble(upper) * multiplier);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<int> value)
        {
            // Both halves widen exactly. Narrow rejoins the products in their original lane order
            // and rounds once, matching the scalar native high-precision reconstruction boundary.
            (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(value);
            Vector256<double> multiplier = Vector256.Create(this.factor);
            return Vector256.Narrow(Vector256.ConvertToDouble(lower) * multiplier, Vector256.ConvertToDouble(upper) * multiplier);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<int> value)
        {
            // Both halves widen exactly. Narrow rejoins the products in their original lane order
            // and rounds once, matching the scalar native high-precision reconstruction boundary.
            (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(value);
            Vector512<double> multiplier = Vector512.Create(this.factor);
            return Vector512.Narrow(Vector512.ConvertToDouble(lower) * multiplier, Vector512.ConvertToDouble(upper) * multiplier);
        }
    }

    /// <summary>
    /// Reinterprets complete IEEE single-precision bit patterns in either direction without arithmetic or NaN canonicalization.
    /// </summary>
    public readonly struct Binary32Operator : IConversionOperator<int, float>, IConversionOperator<float, int>
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(int value) => BitConverter.Int32BitsToSingle(value);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<int> value) => value.AsSingle();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<int> value) => value.AsSingle();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<int> value) => value.AsSingle();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Invoke(float value) => BitConverter.SingleToInt32Bits(value);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector128<float> value) => value.AsInt32();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> Invoke(Vector256<float> value) => value.AsInt32();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<int> Invoke(Vector512<float> value) => value.AsInt32();
    }

    /// <summary>
    /// Expands the mantissa of a floating-point sample that already has the IEEE eight-bit exponent and its bias.
    /// </summary>
    public readonly struct Exponent8Operator : IConversionOperator<int, float>
    {
        private readonly uint magnitudeMask;
        private readonly int mantissaShift;

        /// <summary>
        /// Initializes a new instance of the <see cref="Exponent8Operator"/> struct.
        /// </summary>
        /// <param name="bitsPerSample">The validated total floating precision with an eight-bit exponent.</param>
        public Exponent8Operator(int bitsPerSample)
        {
            // One sign bit and eight exponent bits leave 2..22 mantissa bits in this shortened layout.
            // The magnitude mask excludes the sign and all higher carrier bits. Shifts are bounded to 1..21.
            this.magnitudeMask = (1U << (bitsPerSample - 1)) - 1U;
            this.mantissaShift = 32 - bitsPerSample;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(int value)
        {
            uint carrier = (uint)value;
            uint sign = carrier > this.magnitudeMask ? 0x80000000U : 0U;
            return BitConverter.UInt32BitsToSingle(((carrier & this.magnitudeMask) << this.mantissaShift) | sign);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<int> value)
        {
            // Exponent, subnormal and NaN encodings retain their IEEE meaning. Move the complete magnitude
            // to its binary32 position and attach the sign. Native treats any high carrier bits as a negative sign.
            Vector128<uint> carrier = value.AsUInt32();
            Vector128<uint> mask = Vector128.Create(this.magnitudeMask);
            Vector128<uint> sign = Vector128.GreaterThan(carrier, mask) & Vector128.Create(0x80000000U);
            return (((carrier & mask) << this.mantissaShift) | sign).AsSingle();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<int> value)
        {
            // Exponent, subnormal and NaN encodings retain their IEEE meaning. Move the complete magnitude
            // to its binary32 position and attach the sign. Native treats any high carrier bits as a negative sign.
            Vector256<uint> carrier = value.AsUInt32();
            Vector256<uint> mask = Vector256.Create(this.magnitudeMask);
            Vector256<uint> sign = Vector256.GreaterThan(carrier, mask) & Vector256.Create(0x80000000U);
            return (((carrier & mask) << this.mantissaShift) | sign).AsSingle();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<int> value)
        {
            // Exponent, subnormal and NaN encodings retain their IEEE meaning. Move the complete magnitude
            // to its binary32 position and attach the sign. Native treats any high carrier bits as a negative sign.
            Vector512<uint> carrier = value.AsUInt32();
            Vector512<uint> mask = Vector512.Create(this.magnitudeMask);
            Vector512<uint> sign = Vector512.GreaterThan(carrier, mask) & Vector512.Create(0x80000000U);
            return (((carrier & mask) << this.mantissaShift) | sign).AsSingle();
        }
    }

    /// <summary>
    /// Expands floating-point samples with a two- through seven-bit exponent and a custom exponent bias.
    /// </summary>
    public readonly struct CustomFloatOperator : IConversionOperator<int, float>
    {
        private readonly uint magnitudeMask;
        private readonly uint mantissaMask;
        private readonly uint exponentMask;
        private readonly uint exponentOffset;
        private readonly int mantissaShift;
        private readonly float subnormalScale;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomFloatOperator"/> struct.
        /// </summary>
        /// <param name="bitDepth">The validated floating-point layout with fewer than eight exponent bits.</param>
        public CustomFloatOperator(JxlBitDepth bitDepth)
        {
            int exponentBits = bitDepth.ExponentBits;
            int mantissaBits = bitDepth.BitsPerSample - exponentBits - 1;
            int exponentBias = (1 << (exponentBits - 1)) - 1;

            // Mantissas contain 2..23 bits; the left shift is 0..21. The sign is at bit 4..30,
            // so all masks fit uint and neither a 32-bit shift nor signed mask overflow is possible.
            this.magnitudeMask = (1U << (bitDepth.BitsPerSample - 1)) - 1U;
            this.mantissaMask = (1U << mantissaBits) - 1U;
            this.exponentMask = this.magnitudeMask ^ this.mantissaMask;
            this.exponentOffset = (uint)(127 - exponentBias) << 23;
            this.mantissaShift = 23 - mantissaBits;

            // A subnormal is mantissa * 2^(1-bias-mantissaBits). The integer mantissa converts exactly.
            // Here bias <=63 and mantissaBits <=23: the minimum nonzero result is at least 2^-85,
            // which is normal in binary32. Power-of-two scaling adds no rounding or denormal-flush dependency.
            this.subnormalScale = MathF.ScaleB(1F, 1 - exponentBias - mantissaBits);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(int value)
        {
            uint carrier = (uint)value;
            uint sign = carrier > this.magnitudeMask ? 0x80000000U : 0U;
            uint magnitude = carrier & this.magnitudeMask;
            uint exponent = magnitude & this.exponentMask;
            uint result = (magnitude << this.mantissaShift) + this.exponentOffset;
            if (exponent == this.exponentMask)
            {
                result = (magnitude << this.mantissaShift) | 0x7F800000U;
            }
            else if (exponent == 0)
            {
                result = BitConverter.SingleToUInt32Bits((magnitude & this.mantissaMask) * this.subnormalScale);
            }

            return BitConverter.UInt32BitsToSingle(result | sign);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<int> value)
        {
            // Bit assembly handles normal values and special exponent encodings. Two lane masks then select
            // the exact subnormal product or the original Inf/NaN mantissa. Attaching sign last retains negative zero.
            Vector128<uint> carrier = value.AsUInt32();
            Vector128<uint> magnitudeMask = Vector128.Create(this.magnitudeMask);
            Vector128<uint> sign = Vector128.GreaterThan(carrier, magnitudeMask) & Vector128.Create(0x80000000U);
            Vector128<uint> magnitude = carrier & magnitudeMask;
            Vector128<uint> exponentMask = Vector128.Create(this.exponentMask);
            Vector128<uint> exponent = magnitude & exponentMask;
            Vector128<uint> shifted = magnitude << this.mantissaShift;
            Vector128<uint> normal = shifted + Vector128.Create(this.exponentOffset);
            Vector128<uint> special = shifted | Vector128.Create(0x7F800000U);
            Vector128<float> subnormal = Vector128.ConvertToSingle(magnitude & Vector128.Create(this.mantissaMask))
                * Vector128.Create(this.subnormalScale);

            Vector128<uint> result = Vector128.ConditionalSelect(Vector128.Equals(exponent, exponentMask), special, normal);
            result = Vector128.ConditionalSelect(Vector128.Equals(exponent, Vector128<uint>.Zero), subnormal.AsUInt32(), result);
            return (result | sign).AsSingle();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<int> value)
        {
            // Bit assembly handles normal values and special exponent encodings. Two lane masks then select
            // the exact subnormal product or the original Inf/NaN mantissa. Attaching sign last retains negative zero.
            Vector256<uint> carrier = value.AsUInt32();
            Vector256<uint> magnitudeMask = Vector256.Create(this.magnitudeMask);
            Vector256<uint> sign = Vector256.GreaterThan(carrier, magnitudeMask) & Vector256.Create(0x80000000U);
            Vector256<uint> magnitude = carrier & magnitudeMask;
            Vector256<uint> exponentMask = Vector256.Create(this.exponentMask);
            Vector256<uint> exponent = magnitude & exponentMask;
            Vector256<uint> shifted = magnitude << this.mantissaShift;
            Vector256<uint> normal = shifted + Vector256.Create(this.exponentOffset);
            Vector256<uint> special = shifted | Vector256.Create(0x7F800000U);
            Vector256<float> subnormal = Vector256.ConvertToSingle(magnitude & Vector256.Create(this.mantissaMask))
                * Vector256.Create(this.subnormalScale);

            Vector256<uint> result = Vector256.ConditionalSelect(Vector256.Equals(exponent, exponentMask), special, normal);
            result = Vector256.ConditionalSelect(Vector256.Equals(exponent, Vector256<uint>.Zero), subnormal.AsUInt32(), result);
            return (result | sign).AsSingle();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<int> value)
        {
            // Bit assembly handles normal values and special exponent encodings. Two lane masks then select
            // the exact subnormal product or the original Inf/NaN mantissa. Attaching sign last retains negative zero.
            Vector512<uint> carrier = value.AsUInt32();
            Vector512<uint> magnitudeMask = Vector512.Create(this.magnitudeMask);
            Vector512<uint> sign = Vector512.GreaterThan(carrier, magnitudeMask) & Vector512.Create(0x80000000U);
            Vector512<uint> magnitude = carrier & magnitudeMask;
            Vector512<uint> exponentMask = Vector512.Create(this.exponentMask);
            Vector512<uint> exponent = magnitude & exponentMask;
            Vector512<uint> shifted = magnitude << this.mantissaShift;
            Vector512<uint> normal = shifted + Vector512.Create(this.exponentOffset);
            Vector512<uint> special = shifted | Vector512.Create(0x7F800000U);
            Vector512<float> subnormal = Vector512.ConvertToSingle(magnitude & Vector512.Create(this.mantissaMask))
                * Vector512.Create(this.subnormalScale);

            Vector512<uint> result = Vector512.ConditionalSelect(Vector512.Equals(exponent, exponentMask), special, normal);
            result = Vector512.ConditionalSelect(Vector512.Equals(exponent, Vector512<uint>.Zero), subnormal.AsUInt32(), result);
            return (result | sign).AsSingle();
        }
    }
}
