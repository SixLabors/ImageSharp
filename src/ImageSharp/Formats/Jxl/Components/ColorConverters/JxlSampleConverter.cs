// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

/// <summary>
/// Converts reconstructed modular integer carriers to normalized integer samples or floating-point samples.
/// </summary>
/// <remarks>
/// The image header owns precision validation. Conversion preserves signed zero, nonfinite floating encodings and finite HDR values.
/// Pixel conversion applies destination bounds later. The operators can also join a codec's existing planar traversal without extra rows.
/// Equations follow libjxl revision 91837342b24034078780b687f57f670a7def8153, lib/jxl/dec_modular.cc,
/// SingleFromSingle, SingleFromSingleAccurate and int_to_float. The original jxl-rs render/stages/convert.rs uses the same float layout.
/// </remarks>
internal static class JxlSampleConverter
{
    /// <summary>
    /// Defines exact conversion for scalar carriers and the populated lanes of a sample register.
    /// </summary>
    /// <typeparam name="TSample">The integer bit carrier or already-restored float sample.</typeparam>
    public interface IConversionOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Converts one carrier.
        /// </summary>
        /// <param name="value">The encoded carrier or already-restored sample.</param>
        /// <returns>The normalized or decoded float sample.</returns>
        float Invoke(TSample value);

        /// <summary>
        /// Converts the low four carriers without changing lane order. Short storage uses half the input register.
        /// </summary>
        /// <param name="value">The encoded carriers or already-restored samples.</param>
        /// <returns>The decoded float samples in the same lanes.</returns>
        Vector128<float> Invoke(Vector128<TSample> value);

        /// <summary>
        /// Converts the low eight carriers without changing lane order. Short storage uses half the input register.
        /// </summary>
        /// <param name="value">The encoded carriers or already-restored samples.</param>
        /// <returns>The decoded float samples in the same lanes.</returns>
        Vector256<float> Invoke(Vector256<TSample> value);

        /// <summary>
        /// Converts the low sixteen carriers without changing lane order. Short storage uses half the input register.
        /// </summary>
        /// <param name="value">The encoded carriers or already-restored samples.</param>
        /// <returns>The decoded float samples in the same lanes.</returns>
        Vector512<float> Invoke(Vector512<TSample> value);
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
    private static void Convert<TSample, TOperator>(ReadOnlySpan<TSample> source, Span<float> destination, TOperator operation)
        where TSample : unmanaged
        where TOperator : struct, IConversionOperator<TSample>
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
    /// Widens signed 16-bit modular storage before applying an existing 32-bit carrier conversion.
    /// </summary>
    /// <typeparam name="TOperator">The existing integer normalization or floating-point carrier operation.</typeparam>
    public readonly struct Int16Operator<TOperator> : IConversionOperator<short>
        where TOperator : struct, IConversionOperator<int>
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
    public readonly struct IdentityOperator : IConversionOperator<float>
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
    public readonly struct IntegerOperator : IConversionOperator<int>
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
    public readonly struct AccurateIntegerOperator : IConversionOperator<int>
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
    /// Reinterprets complete IEEE single-precision bit patterns without arithmetic or NaN canonicalization.
    /// </summary>
    public readonly struct Binary32Operator : IConversionOperator<int>
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
    }

    /// <summary>
    /// Expands the mantissa of a floating-point sample that already has the IEEE eight-bit exponent and its bias.
    /// </summary>
    public readonly struct Exponent8Operator : IConversionOperator<int>
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
    public readonly struct CustomFloatOperator : IConversionOperator<int>
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
