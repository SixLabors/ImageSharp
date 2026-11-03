// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents an XYB color as defined by the JPEG XL core coding system, ISO/IEC 18181-1.
/// </summary>
/// <remarks>
/// <para>
/// X is half the difference between the transformed long- and medium-wavelength cone responses. Y is their average.
/// B is the transformed short-wavelength cone response. Component values can fall outside the interval from zero to one.
/// </para>
/// <para>
/// XYB originated in the Butteraugli perceptual model and was adopted by JPEG XL.
/// See the <see href="https://jpeg.org/jpegxl/workplan.html">JPEG XL specifications</see> and
/// <see href="https://arxiv.org/html/2506.05987v1">JPEG XL design paper</see>, sections 1.1.12 and 4.2.1.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Xyb : IColorProfile<Xyb, CieXyz>
{
    /// <summary>
    /// The IEEE single-precision mask that removes the sign bit before root normalization.
    /// </summary>
    private const uint MagnitudeMask = 0x7FFFFFFFU;

    /// <summary>
    /// The IEEE single-precision fraction bits retained when rebuilding the normalized mantissa.
    /// </summary>
    private const uint MantissaMask = 0x007FFFFFU;

    /// <summary>
    /// The IEEE bit pattern of the smallest positive normal float; smaller magnitudes need exponent normalization.
    /// </summary>
    private const uint MinimumNormalBits = 0x00800000U;

    /// <summary>
    /// The number of fraction bits below the IEEE single-precision exponent field.
    /// </summary>
    private const int MantissaBits = 23;

    /// <summary>
    /// The bias added to an IEEE single-precision normal exponent.
    /// </summary>
    private const int ExponentBias = 127;

    /// <summary>
    /// The exact power-of-two shift that makes every nonzero subnormal float normal.
    /// </summary>
    private const int SubnormalExponentShift = 24;

    /// <summary>
    /// The exact multiplier used before extracting a subnormal input's exponent.
    /// </summary>
    private const float SubnormalScale = 1 << SubnormalExponentShift;

    /// <summary>
    /// The exponent offset divisible by three that makes all finite input exponents positive.
    /// The smallest subnormal exponent is -149, so the adjusted exponent range is [1,277].
    /// </summary>
    private const int ExponentOffset = 150;

    /// <summary>
    /// The cube-root exponent offset removed after dividing the adjusted exponent by three.
    /// </summary>
    private const int ExponentQuotientOffset = ExponentOffset / 3;

    /// <summary>
    /// The fixed-point reciprocal shift used for exact division by three over the adjusted exponent range.
    /// </summary>
    private const int ThirdShift = 16;

    /// <summary>
    /// Ceil(2^16/3), which gives floor(v/3) after shifting for every integer v in [1,277].
    /// </summary>
    private const int ThirdMultiplier = 21846;

    /// <summary>
    /// The native bit-pattern bias used to seed the reciprocal cube root of a normalized mantissa.
    /// </summary>
    private const int ReciprocalRootSeedBias = 0x54800000;

    /// <summary>
    /// The native exponent multiplier used by the reciprocal-root seed.
    /// </summary>
    private const int ReciprocalRootExponentMultiplier = 0x002AAAAA;

    /// <summary>
    /// The rounded single-precision one-third coefficient used by native root refinement.
    /// </summary>
    private const float OneThird = 1F / 3F;

    /// <summary>
    /// The rounded single-precision four-thirds coefficient used by native root refinement.
    /// </summary>
    private const float FourThirds = 4F / 3F;

    /// <summary>
    /// The number of native reciprocal-root iterations before its final correction.
    /// </summary>
    private const int ReciprocalRootIterations = 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="Xyb"/> struct.
    /// </summary>
    /// <param name="x">The X opponent component.</param>
    /// <param name="y">The Y opponent component.</param>
    /// <param name="b">The blue absorbance component.</param>
    public Xyb(float x, float y, float b)
    {
        this.X = x;
        this.Y = y;
        this.B = b;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Xyb"/> struct from three components.
    /// </summary>
    /// <param name="source">The X, Y and B components.</param>
    public Xyb(Vector3 source)
        : this(source.X, source.Y, source.Z)
    {
    }

    /// <summary>
    /// Gets the X opponent component.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Gets the Y opponent component.
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the blue absorbance component.
    /// </summary>
    public float B { get; }

    /// <summary>
    /// Compares two XYB colors for equality.
    /// </summary>
    /// <param name="left">The first color.</param>
    /// <param name="right">The second color.</param>
    /// <returns>Whether all three components are equal.</returns>
    public static bool operator ==(Xyb left, Xyb right) => left.Equals(right);

    /// <summary>
    /// Compares two XYB colors for inequality.
    /// </summary>
    /// <param name="left">The first color.</param>
    /// <param name="right">The second color.</param>
    /// <returns>Whether any component differs.</returns>
    public static bool operator !=(Xyb left, Xyb right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4() => new(this.X, this.Y, this.B, 1F);

    /// <inheritdoc/>
    public static Xyb FromScaledVector4(Vector4 source) => new(source.AsVector3());

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<Xyb> source, Span<Vector4> destination)
    {
        // Both models store three consecutive floats. Reuse the existing bit-preserving layout conversion.
        Rgb.ToScaledVector4(MemoryMarshal.Cast<Xyb, Rgb>(source), destination);
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<Xyb> destination)
        => Rgb.FromScaledVector4(source, MemoryMarshal.Cast<Xyb, Rgb>(destination));

    /// <inheritdoc/>
    public CieXyz ToProfileConnectingSpace(ColorConversionOptions options)
    {
        XybTransform transform = options.PreparedXybTransform;
        InverseOperator operation = new(transform.Inverse, transform.Biases, transform.CubeRootBiases);
        operation.Invoke(this.X, this.Y, this.B, out float x, out float y, out float z);

        return new CieXyz(x, y, z);
    }

    /// <inheritdoc/>
    public static Xyb FromProfileConnectingSpace(ColorConversionOptions options, in CieXyz source)
    {
        XybTransform transform = options.PreparedXybTransform;
        ForwardOperator operation = new(transform.Forward, transform.CubeRootBiases);
        operation.Invoke(source.X, source.Y, source.Z, out float x, out float y, out float b);

        return new Xyb(x, y, b);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Xyb> source, Span<CieXyz> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        XybTransform transform = options.PreparedXybTransform;
        InverseOperator operation = new(transform.Inverse, transform.Biases, transform.CubeRootBiases);
        ReadOnlySpan<Vector3> packed = MemoryMarshal.Cast<Xyb, Vector3>(source);
        int i = 0;

        // Each register holds one component from consecutive colors. The composed matrix includes the
        // linear RGB/XYZ change of coordinates; no component transfer curve or synthetic alpha is applied.
        // Load all three inputs before storing the matching block, including exact in-place conversion.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = (source.Length - i).Vector512Count<float>();

            for (nuint block = 0; block < vectorCount; block++, i += Vector512<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2);
                operation.Invoke(c0, c1, c2, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[i]));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = (source.Length - i).Vector256Count<float>();

            for (nuint block = 0; block < vectorCount; block++, i += Vector256<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2);
                operation.Invoke(c0, c1, c2, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[i]));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = (source.Length - i).Vector128Count<float>();

            for (nuint block = 0; block < vectorCount; block++, i += Vector128<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2);
                operation.Invoke(c0, c1, c2, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<CieXyz, float>(ref destination[i]));
            }
        }

        // The remaining colors use the same equations and matrix. Read one complete triple before its
        // store so the scalar fallback preserves the SIMD path's layout and in-place behavior.
        for (; i < source.Length; i++)
        {
            Vector3 value = packed[i];
            operation.Invoke(value.X, value.Y, value.Z, out float x, out float y, out float z);
            destination[i] = new CieXyz(x, y, z);
        }
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<CieXyz> source, Span<Xyb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        XybTransform transform = options.PreparedXybTransform;
        ForwardOperator operation = new(transform.Forward, transform.CubeRootBiases);
        ReadOnlySpan<Vector3> packed = MemoryMarshal.Cast<CieXyz, Vector3>(source);
        int i = 0;

        // Each register holds one component from consecutive colors. The composed matrix includes the
        // linear RGB/XYZ change of coordinates; no component transfer curve or synthetic alpha is applied.
        // Load all three inputs before storing the matching block, including exact in-place conversion.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = (source.Length - i).Vector512Count<float>();

            for (nuint block = 0; block < vectorCount; block++, i += Vector512<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector512<float> c0, out Vector512<float> c1, out Vector512<float> c2);
                operation.Invoke(c0, c1, c2, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<Xyb, float>(ref destination[i]));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = (source.Length - i).Vector256Count<float>();

            for (nuint block = 0; block < vectorCount; block++, i += Vector256<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector256<float> c0, out Vector256<float> c1, out Vector256<float> c2);
                operation.Invoke(c0, c1, c2, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<Xyb, float>(ref destination[i]));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = (source.Length - i).Vector128Count<float>();

            for (nuint block = 0; block < vectorCount; block++, i += Vector128<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector128<float> c0, out Vector128<float> c1, out Vector128<float> c2);
                operation.Invoke(c0, c1, c2, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z);
                SimdUtils.Interleave3(x, y, z, ref Unsafe.As<Xyb, float>(ref destination[i]));
            }
        }

        // The remaining colors use the same equations and matrix. Read one complete triple before its
        // store so the scalar fallback preserves the SIMD path's layout and in-place behavior.
        for (; i < source.Length; i++)
        {
            Vector3 value = packed[i];
            operation.Invoke(value.X, value.Y, value.Z, out float x, out float y, out float z);
            destination[i] = new Xyb(x, y, z);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.RgbWorkingSpace;

    /// <inheritdoc/>
    public bool Equals(Xyb other) => this.X == other.X && this.Y == other.Y && this.B == other.B;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Xyb other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.X, this.Y, this.B);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"Xyb({this.X}, {this.Y}, {this.B})");

    /// <summary>
    /// Computes a nonnegative finite cube root plus its bias without overflowing reciprocal-root intermediates.
    /// </summary>
    /// <param name="value">The nonnegative finite absorbance, including zero and subnormal values.</param>
    /// <param name="add">The negative cube-root bias.</param>
    /// <returns>The biased root with the final addition performed as a multiply-add estimate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CubeRootAndAdd(float value, float add)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value) & MagnitudeMask;
        bool subnormal = bits < MinimumNormalBits;
        float normalized = BitConverter.UInt32BitsToSingle(bits) * (subnormal ? SubnormalScale : 1F);
        uint normalizedBits = BitConverter.SingleToUInt32Bits(normalized);

        // Standard subnormal preparation multiplies by 2^24, then corrects the extracted exponent.
        // Zero has no exponent: substitute exponent zero before division and use final scale zero.
        int exponent = bits == 0 ? 0 : (int)(normalizedBits >> MantissaBits) - ExponentBias - (subnormal ? SubnormalExponentShift : 0);
        int positiveExponent = exponent + ExponentOffset;

        // Adding 150 keeps the exponent in [1,277] and preserves its remainder modulo three.
        // Multiplication by ceil(2^16/3), then shifting 16 bits, gives the exact integer quotient throughout that interval.
        int quotient = (positiveExponent * ThirdMultiplier) >> ThirdShift;
        int remainder = positiveExponent - (3 * quotient);

        // Move the exponent remainder into z. Then value=z*2^(3k), where 1<=z<8 and k=quotient-ExponentQuotientOffset.
        float z = BitConverter.Int32BitsToSingle((int)(normalizedBits & MantissaMask) | ((ExponentBias + remainder) << MantissaBits));
        float scale = bits == 0 ? 0F : BitConverter.Int32BitsToSingle((quotient + ExponentBias - ExponentQuotientOffset) << MantissaBits);

        // Estimate 1/cbrt(z) from the float exponent. Refinement sees only z in [1,8), so its fourth powers cannot overflow.
        float reciprocalRoot = BitConverter.Int32BitsToSingle(ReciprocalRootSeedBias - ((ExponentBias + remainder) * ReciprocalRootExponentMultiplier));
        float thirdZ = OneThird * z;

        // Refine the reciprocal root with t = (4*t - z*t^4)/3. Preserve the native multiply-add evaluation order.
        for (int i = 0; i < ReciprocalRootIterations; i++)
        {
            float squared = reciprocalRoot * reciprocalRoot;
            reciprocalRoot = float.MultiplyAddEstimate(-thirdZ, squared * squared, FourThirds * reciprocalRoot);
        }

        // Apply the final correction before restoring the input exponent and adding the signed root bias.
        float squaredRoot = reciprocalRoot * reciprocalRoot;
        reciprocalRoot = float.MultiplyAddEstimate(OneThird, float.MultiplyAddEstimate(-z, squaredRoot * squaredRoot, reciprocalRoot), reciprocalRoot);

        squaredRoot = reciprocalRoot * reciprocalRoot;

        // Scaling a normal coefficient by 2^k is exact. Keep it inside the last multiply-add so bias cancellation gets no extra rounded root.
        return float.MultiplyAddEstimate(squaredRoot * scale, z, add);
    }

    /// <summary>
    /// Evaluates bounded reciprocal-root refinements independently in each nonnegative finite absorbance lane.
    /// </summary>
    /// <param name="value">The absorbance lanes, including zero and subnormal values.</param>
    /// <param name="add">The corresponding negative cube-root biases.</param>
    /// <returns>The biased roots, with no scalar lane calls or temporary sample storage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> CubeRootAndAdd(Vector128<float> value, Vector128<float> add)
    {
        // Work on magnitudes. Each lane contains one nonnegative absorbance value, not components of one pixel.
        Vector128<uint> bits = value.AsUInt32() & Vector128.Create(MagnitudeMask);
        Vector128<uint> zero = Vector128.Equals(bits, Vector128<uint>.Zero);
        Vector128<uint> subnormal = Vector128.LessThan(bits, Vector128.Create(MinimumNormalBits));

        // Multiplication by 2^24 normalizes subnormal lanes. Subtract that exponent shift after extracting the exponent.
        Vector128<float> normalization = Vector128.ConditionalSelect(subnormal.AsSingle(), Vector128.Create(SubnormalScale), Vector128<float>.One);
        Vector128<uint> normalizedBits = (bits.AsSingle() * normalization).AsUInt32();

        Vector128<int> exponent = Vector128.ShiftRightLogical(normalizedBits, MantissaBits).AsInt32() - Vector128.Create(ExponentBias);
        exponent -= Vector128.ConditionalSelect(subnormal.AsInt32(), Vector128.Create(SubnormalExponentShift), Vector128<int>.Zero);
        exponent = Vector128.ConditionalSelect(zero.AsInt32(), Vector128<int>.Zero, exponent);
        Vector128<int> positiveExponent = exponent + Vector128.Create(ExponentOffset);

        // For positiveExponent in [1,277], floor(v/3)=(v*21846)>>16. 21846=ceil(2^16/3).
        // The reciprocal error is below 1/3, so it cannot cross the next quotient. All products fit Int32 and remain SIMD.
        Vector128<int> quotient = Vector128.ShiftRightArithmetic(positiveExponent * Vector128.Create(ThirdMultiplier), ThirdShift);
        Vector128<int> remainder = positiveExponent - (quotient * Vector128.Create(3));

        // Rebuild z in [1,8) from the original fraction and the exponent remainder. The other exponent bits form 2^k.
        Vector128<int> normalExponent = remainder + Vector128.Create(ExponentBias);
        Vector128<uint> mantissa = normalizedBits & Vector128.Create(MantissaMask);
        Vector128<float> z = (mantissa | Vector128.ShiftLeft(normalExponent, MantissaBits).AsUInt32()).AsSingle();
        Vector128<float> scale = Vector128.ShiftLeft(quotient + Vector128.Create(ExponentBias - ExponentQuotientOffset), MantissaBits).AsSingle();
        scale = Vector128.ConditionalSelect(zero.AsSingle(), Vector128<float>.Zero, scale);

        // Zero lanes use z=1 and scale=0. They follow the same finite refinement and return only the added bias.
        Vector128<int> seed = Vector128.Create(ReciprocalRootSeedBias) - (normalExponent * Vector128.Create(ReciprocalRootExponentMultiplier));
        Vector128<float> reciprocalRoot = seed.AsSingle();
        Vector128<float> thirdZ = Vector128.Create(OneThird) * z;

        // Refine t=1/cbrt(z) with t=(4*t-z*t^4)/3. The bounded z range keeps all lanes finite.
        for (int i = 0; i < ReciprocalRootIterations; i++)
        {
            Vector128<float> squared = reciprocalRoot * reciprocalRoot;
            reciprocalRoot = Vector128.MultiplyAddEstimate(-thirdZ, squared * squared, Vector128.Create(FourThirds) * reciprocalRoot);
        }

        // Apply the scalar path's final correction, restore the input exponent and add the signed root bias.
        Vector128<float> squaredRoot = reciprocalRoot * reciprocalRoot;
        reciprocalRoot = Vector128.MultiplyAddEstimate(
            Vector128.Create(OneThird),
            Vector128.MultiplyAddEstimate(-z, squaredRoot * squaredRoot, reciprocalRoot),
            reciprocalRoot);

        squaredRoot = reciprocalRoot * reciprocalRoot;

        return Vector128.MultiplyAddEstimate(squaredRoot * scale, z, add);
    }

    /// <summary>
    /// Evaluates bounded reciprocal-root refinements independently in each nonnegative finite absorbance lane.
    /// </summary>
    /// <param name="value">The absorbance lanes, including zero and subnormal values.</param>
    /// <param name="add">The corresponding negative cube-root biases.</param>
    /// <returns>The biased roots, with no scalar lane calls or temporary sample storage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> CubeRootAndAdd(Vector256<float> value, Vector256<float> add)
    {
        // Work on magnitudes. Each lane contains one nonnegative absorbance value, not components of one pixel.
        Vector256<uint> bits = value.AsUInt32() & Vector256.Create(MagnitudeMask);
        Vector256<uint> zero = Vector256.Equals(bits, Vector256<uint>.Zero);
        Vector256<uint> subnormal = Vector256.LessThan(bits, Vector256.Create(MinimumNormalBits));

        // Multiplication by 2^24 normalizes subnormal lanes. Subtract that exponent shift after extracting the exponent.
        Vector256<float> normalization = Vector256.ConditionalSelect(subnormal.AsSingle(), Vector256.Create(SubnormalScale), Vector256<float>.One);
        Vector256<uint> normalizedBits = (bits.AsSingle() * normalization).AsUInt32();

        Vector256<int> exponent = Vector256.ShiftRightLogical(normalizedBits, MantissaBits).AsInt32() - Vector256.Create(ExponentBias);
        exponent -= Vector256.ConditionalSelect(subnormal.AsInt32(), Vector256.Create(SubnormalExponentShift), Vector256<int>.Zero);
        exponent = Vector256.ConditionalSelect(zero.AsInt32(), Vector256<int>.Zero, exponent);
        Vector256<int> positiveExponent = exponent + Vector256.Create(ExponentOffset);

        // For positiveExponent in [1,277], floor(v/3)=(v*21846)>>16. 21846=ceil(2^16/3).
        // The reciprocal error is below 1/3, so it cannot cross the next quotient. All products fit Int32 and remain SIMD.
        Vector256<int> quotient = Vector256.ShiftRightArithmetic(positiveExponent * Vector256.Create(ThirdMultiplier), ThirdShift);
        Vector256<int> remainder = positiveExponent - (quotient * Vector256.Create(3));

        // Rebuild z in [1,8) from the original fraction and the exponent remainder. The other exponent bits form 2^k.
        Vector256<int> normalExponent = remainder + Vector256.Create(ExponentBias);
        Vector256<uint> mantissa = normalizedBits & Vector256.Create(MantissaMask);
        Vector256<float> z = (mantissa | Vector256.ShiftLeft(normalExponent, MantissaBits).AsUInt32()).AsSingle();
        Vector256<float> scale = Vector256.ShiftLeft(quotient + Vector256.Create(ExponentBias - ExponentQuotientOffset), MantissaBits).AsSingle();
        scale = Vector256.ConditionalSelect(zero.AsSingle(), Vector256<float>.Zero, scale);

        // Zero lanes use z=1 and scale=0. They follow the same finite refinement and return only the added bias.
        Vector256<int> seed = Vector256.Create(ReciprocalRootSeedBias) - (normalExponent * Vector256.Create(ReciprocalRootExponentMultiplier));
        Vector256<float> reciprocalRoot = seed.AsSingle();
        Vector256<float> thirdZ = Vector256.Create(OneThird) * z;

        // Refine t=1/cbrt(z) with t=(4*t-z*t^4)/3. The bounded z range keeps all lanes finite.
        for (int i = 0; i < ReciprocalRootIterations; i++)
        {
            Vector256<float> squared = reciprocalRoot * reciprocalRoot;
            reciprocalRoot = Vector256.MultiplyAddEstimate(-thirdZ, squared * squared, Vector256.Create(FourThirds) * reciprocalRoot);
        }

        // Apply the scalar path's final correction, restore the input exponent and add the signed root bias.
        Vector256<float> squaredRoot = reciprocalRoot * reciprocalRoot;
        reciprocalRoot = Vector256.MultiplyAddEstimate(
            Vector256.Create(OneThird),
            Vector256.MultiplyAddEstimate(-z, squaredRoot * squaredRoot, reciprocalRoot),
            reciprocalRoot);

        squaredRoot = reciprocalRoot * reciprocalRoot;

        return Vector256.MultiplyAddEstimate(squaredRoot * scale, z, add);
    }

    /// <summary>
    /// Evaluates bounded reciprocal-root refinements independently in each nonnegative finite absorbance lane.
    /// </summary>
    /// <param name="value">The absorbance lanes, including zero and subnormal values.</param>
    /// <param name="add">The corresponding negative cube-root biases.</param>
    /// <returns>The biased roots, with no scalar lane calls or temporary sample storage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> CubeRootAndAdd(Vector512<float> value, Vector512<float> add)
    {
        // Work on magnitudes. Each lane contains one nonnegative absorbance value, not components of one pixel.
        Vector512<uint> bits = value.AsUInt32() & Vector512.Create(MagnitudeMask);
        Vector512<uint> zero = Vector512.Equals(bits, Vector512<uint>.Zero);
        Vector512<uint> subnormal = Vector512.LessThan(bits, Vector512.Create(MinimumNormalBits));

        // Multiplication by 2^24 normalizes subnormal lanes. Subtract that exponent shift after extracting the exponent.
        Vector512<float> normalization = Vector512.ConditionalSelect(subnormal.AsSingle(), Vector512.Create(SubnormalScale), Vector512<float>.One);
        Vector512<uint> normalizedBits = (bits.AsSingle() * normalization).AsUInt32();

        Vector512<int> exponent = Vector512.ShiftRightLogical(normalizedBits, MantissaBits).AsInt32() - Vector512.Create(ExponentBias);
        exponent -= Vector512.ConditionalSelect(subnormal.AsInt32(), Vector512.Create(SubnormalExponentShift), Vector512<int>.Zero);
        exponent = Vector512.ConditionalSelect(zero.AsInt32(), Vector512<int>.Zero, exponent);
        Vector512<int> positiveExponent = exponent + Vector512.Create(ExponentOffset);

        // For positiveExponent in [1,277], floor(v/3)=(v*21846)>>16. 21846=ceil(2^16/3).
        // The reciprocal error is below 1/3, so it cannot cross the next quotient. All products fit Int32 and remain SIMD.
        Vector512<int> quotient = Vector512.ShiftRightArithmetic(positiveExponent * Vector512.Create(ThirdMultiplier), ThirdShift);
        Vector512<int> remainder = positiveExponent - (quotient * Vector512.Create(3));

        // Rebuild z in [1,8) from the original fraction and the exponent remainder. The other exponent bits form 2^k.
        Vector512<int> normalExponent = remainder + Vector512.Create(ExponentBias);
        Vector512<uint> mantissa = normalizedBits & Vector512.Create(MantissaMask);
        Vector512<float> z = (mantissa | Vector512.ShiftLeft(normalExponent, MantissaBits).AsUInt32()).AsSingle();
        Vector512<float> scale = Vector512.ShiftLeft(quotient + Vector512.Create(ExponentBias - ExponentQuotientOffset), MantissaBits).AsSingle();
        scale = Vector512.ConditionalSelect(zero.AsSingle(), Vector512<float>.Zero, scale);

        // Zero lanes use z=1 and scale=0. They follow the same finite refinement and return only the added bias.
        Vector512<int> seed = Vector512.Create(ReciprocalRootSeedBias) - (normalExponent * Vector512.Create(ReciprocalRootExponentMultiplier));
        Vector512<float> reciprocalRoot = seed.AsSingle();
        Vector512<float> thirdZ = Vector512.Create(OneThird) * z;

        // Refine t=1/cbrt(z) with t=(4*t-z*t^4)/3. The bounded z range keeps all lanes finite.
        for (int i = 0; i < ReciprocalRootIterations; i++)
        {
            Vector512<float> squared = reciprocalRoot * reciprocalRoot;
            reciprocalRoot = Vector512.MultiplyAddEstimate(-thirdZ, squared * squared, Vector512.Create(FourThirds) * reciprocalRoot);
        }

        // Apply the scalar path's final correction, restore the input exponent and add the signed root bias.
        Vector512<float> squaredRoot = reciprocalRoot * reciprocalRoot;
        reciprocalRoot = Vector512.MultiplyAddEstimate(
            Vector512.Create(OneThird),
            Vector512.MultiplyAddEstimate(-z, squaredRoot * squaredRoot, reciprocalRoot),
            reciprocalRoot);

        squaredRoot = reciprocalRoot * reciprocalRoot;

        return Vector512.MultiplyAddEstimate(squaredRoot * scale, z, add);
    }

    /// <summary>
    /// Applies the prepared XYZ-to-absorbance matrix, roots and opponent mix.
    /// </summary>
    /// <remarks>
    /// The prepared rows contain negative bias, Z, Y and X coefficients. Input [1,Z,Y,X]
    /// retains the native bias-first order through the same homogeneous matrix operation used elsewhere.
    /// </remarks>
    internal readonly struct ForwardOperator
    {
        private readonly Matrix4x4 matrix;
        private readonly Vector3 cubeRootBiases;

        /// <summary>
        /// Initializes a new instance of the <see cref="ForwardOperator"/> struct.
        /// </summary>
        /// <param name="matrix">The prepared homogeneous coefficient rows for input [1,Z,Y,X].</param>
        /// <param name="cubeRootBiases">The signed roots added after the absorbance cube roots.</param>
        public ForwardOperator(in Matrix4x4 matrix, Vector3 cubeRootBiases)
        {
            this.matrix = matrix;
            this.cubeRootBiases = cubeRootBiases;
        }

        /// <summary>
        /// Converts one XYZ sample with the same absorbance, root and opponent order.
        /// </summary>
        /// <param name="cieX">The CIE X component.</param>
        /// <param name="cieY">The CIE Y component.</param>
        /// <param name="cieZ">The CIE Z component.</param>
        /// <param name="x">The X result.</param>
        /// <param name="y">The Y result.</param>
        /// <param name="blue">The B result.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            float cieX,
            float cieY,
            float cieZ,
            out float x,
            out float y,
            out float blue)
        {
            // The leading one selects the embedded negative bias. Z, Y and X then enter in native order.
            // Coefficient rows were permuted during preparation, not during sample traversal.
            Vector4 mixed = Vector4.Transform(new Vector4(1F, cieZ, cieY, cieX), this.matrix);
            float mixed0 = mixed.X;
            float mixed1 = mixed.Y;
            float mixed2 = mixed.Z;

            // Absorbance must be nonnegative before its cube root. Signed root biases follow the root;
            // they are part of XYB's response curve, not a clamp on stored pixel components.
            mixed0 = CubeRootAndAdd(MathF.Max(0F, mixed0), this.cubeRootBiases.X);
            mixed1 = CubeRootAndAdd(MathF.Max(0F, mixed1), this.cubeRootBiases.Y);
            mixed2 = CubeRootAndAdd(MathF.Max(0F, mixed2), this.cubeRootBiases.Z);

            // X is half the long/medium response difference; Y is their average. B retains its response.
            x = 0.5F * (mixed0 - mixed1);
            y = 0.5F * (mixed0 + mixed1);
            blue = mixed2;
        }

        /// <summary>
        /// Converts four XYZ samples with the same absorbance, root and opponent order.
        /// </summary>
        /// <param name="cieX">The CIE X component lanes.</param>
        /// <param name="cieY">The CIE Y component lanes.</param>
        /// <param name="cieZ">The CIE Z component lanes.</param>
        /// <param name="x">The X result lanes.</param>
        /// <param name="y">The Y result lanes.</param>
        /// <param name="blue">The B result lanes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector128<float> cieX,
            Vector128<float> cieY,
            Vector128<float> cieZ,
            out Vector128<float> x,
            out Vector128<float> y,
            out Vector128<float> blue)
        {
            // The leading one selects the embedded negative bias. Z, Y and X then enter in native order.
            // Coefficient rows were permuted during preparation, not during sample traversal.
            SimdUtils.Transform3(
                in this.matrix,
                Vector128.Create(1F),
                cieZ,
                cieY,
                cieX,
                out Vector128<float> mixed0,
                out Vector128<float> mixed1,
                out Vector128<float> mixed2);

            // Absorbance must be nonnegative before its cube root. Signed root biases follow the root;
            // they are part of XYB's response curve, not a clamp on stored pixel components.
            mixed0 = CubeRootAndAdd(Vector128.Max(Vector128<float>.Zero, mixed0), Vector128.Create(this.cubeRootBiases.X));
            mixed1 = CubeRootAndAdd(Vector128.Max(Vector128<float>.Zero, mixed1), Vector128.Create(this.cubeRootBiases.Y));
            mixed2 = CubeRootAndAdd(Vector128.Max(Vector128<float>.Zero, mixed2), Vector128.Create(this.cubeRootBiases.Z));

            // X is half the long/medium response difference; Y is their average. B retains its response.
            Vector128<float> half = Vector128.Create(0.5F);
            x = half * (mixed0 - mixed1);
            y = half * (mixed0 + mixed1);
            blue = mixed2;
        }

        /// <summary>
        /// Converts eight XYZ samples with the same absorbance, root and opponent order.
        /// </summary>
        /// <param name="cieX">The CIE X component lanes.</param>
        /// <param name="cieY">The CIE Y component lanes.</param>
        /// <param name="cieZ">The CIE Z component lanes.</param>
        /// <param name="x">The X result lanes.</param>
        /// <param name="y">The Y result lanes.</param>
        /// <param name="blue">The B result lanes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector256<float> cieX,
            Vector256<float> cieY,
            Vector256<float> cieZ,
            out Vector256<float> x,
            out Vector256<float> y,
            out Vector256<float> blue)
        {
            // The leading one selects the embedded negative bias. Z, Y and X then enter in native order.
            // Coefficient rows were permuted during preparation, not during sample traversal.
            SimdUtils.Transform3(
                in this.matrix,
                Vector256.Create(1F),
                cieZ,
                cieY,
                cieX,
                out Vector256<float> mixed0,
                out Vector256<float> mixed1,
                out Vector256<float> mixed2);

            // Absorbance must be nonnegative before its cube root. Signed root biases follow the root;
            // they are part of XYB's response curve, not a clamp on stored pixel components.
            mixed0 = CubeRootAndAdd(Vector256.Max(Vector256<float>.Zero, mixed0), Vector256.Create(this.cubeRootBiases.X));
            mixed1 = CubeRootAndAdd(Vector256.Max(Vector256<float>.Zero, mixed1), Vector256.Create(this.cubeRootBiases.Y));
            mixed2 = CubeRootAndAdd(Vector256.Max(Vector256<float>.Zero, mixed2), Vector256.Create(this.cubeRootBiases.Z));

            // X is half the long/medium response difference; Y is their average. B retains its response.
            Vector256<float> half = Vector256.Create(0.5F);
            x = half * (mixed0 - mixed1);
            y = half * (mixed0 + mixed1);
            blue = mixed2;
        }

        /// <summary>
        /// Converts sixteen XYZ samples with the same absorbance, root and opponent order.
        /// </summary>
        /// <param name="cieX">The CIE X component lanes.</param>
        /// <param name="cieY">The CIE Y component lanes.</param>
        /// <param name="cieZ">The CIE Z component lanes.</param>
        /// <param name="x">The X result lanes.</param>
        /// <param name="y">The Y result lanes.</param>
        /// <param name="blue">The B result lanes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector512<float> cieX,
            Vector512<float> cieY,
            Vector512<float> cieZ,
            out Vector512<float> x,
            out Vector512<float> y,
            out Vector512<float> blue)
        {
            // The leading one selects the embedded negative bias. Z, Y and X then enter in native order.
            // Coefficient rows were permuted during preparation, not during sample traversal.
            SimdUtils.Transform3(
                in this.matrix,
                Vector512.Create(1F),
                cieZ,
                cieY,
                cieX,
                out Vector512<float> mixed0,
                out Vector512<float> mixed1,
                out Vector512<float> mixed2);

            // Absorbance must be nonnegative before its cube root. Signed root biases follow the root;
            // they are part of XYB's response curve, not a clamp on stored pixel components.
            mixed0 = CubeRootAndAdd(Vector512.Max(Vector512<float>.Zero, mixed0), Vector512.Create(this.cubeRootBiases.X));
            mixed1 = CubeRootAndAdd(Vector512.Max(Vector512<float>.Zero, mixed1), Vector512.Create(this.cubeRootBiases.Y));
            mixed2 = CubeRootAndAdd(Vector512.Max(Vector512<float>.Zero, mixed2), Vector512.Create(this.cubeRootBiases.Z));

            // X is half the long/medium response difference; Y is their average. B retains its response.
            Vector512<float> half = Vector512.Create(0.5F);
            x = half * (mixed0 - mixed1);
            y = half * (mixed0 + mixed1);
            blue = mixed2;
        }
    }

    /// <summary>
    /// Restores linear components from XYB with a prepared absorbance matrix.
    /// </summary>
    /// <remarks>
    /// The matrix determines the output coordinates. Model conversion supplies its composed XYZ matrix;
    /// codec reconstruction supplies the intensity-scaled linear RGB matrix. Both use the same inverse equations.
    /// </remarks>
    internal readonly struct InverseOperator
    {
        private readonly Matrix4x4 matrix;
        private readonly Vector3 biases;
        private readonly Vector3 cubeRootBiases;

        /// <summary>
        /// Initializes a new instance of the <see cref="InverseOperator"/> struct.
        /// </summary>
        /// <param name="matrix">The intensity-scaled absorbance matrix in System.Numerics row-vector order.</param>
        /// <param name="biases">The signed biases added after cubing.</param>
        /// <param name="cubeRootBiases">The signed cube roots subtracted before cubing.</param>
        public InverseOperator(in Matrix4x4 matrix, Vector3 biases, Vector3 cubeRootBiases)
        {
            this.matrix = matrix;
            this.biases = biases;
            this.cubeRootBiases = cubeRootBiases;
        }

        /// <summary>
        /// Applies the inverse opsin equations to one pixel in the same order as each SIMD lane.
        /// </summary>
        /// <param name="x">The X sample.</param>
        /// <param name="y">The Y sample.</param>
        /// <param name="b">The B sample.</param>
        /// <param name="result0">The first transformed component.</param>
        /// <param name="result1">The second transformed component.</param>
        /// <param name="result2">The third transformed component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            float x,
            float y,
            float b,
            out float result0,
            out float result1,
            out float result2)
        {
            // Undo the opponent mix and the forward transform's cube-root offset before cubing.
            float gammaR = (y + x) - this.cubeRootBiases.X;
            float gammaG = (y - x) - this.cubeRootBiases.Y;
            float gammaB = b - this.cubeRootBiases.Z;

            Vector3 result = this.CubeAndTransform(new Vector3(gammaR, gammaG, gammaB));
            result0 = result.X;
            result1 = result.Y;
            result2 = result.Z;
        }

        /// <summary>
        /// Reconstructs 4 independent pixels; each register holds one component in pixel order.
        /// </summary>
        /// <param name="x">The X samples.</param>
        /// <param name="y">The Y samples.</param>
        /// <param name="b">The B samples.</param>
        /// <param name="result0">The first transformed components.</param>
        /// <param name="result1">The second transformed components.</param>
        /// <param name="result2">The third transformed components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector128<float> x,
            Vector128<float> y,
            Vector128<float> b,
            out Vector128<float> result0,
            out Vector128<float> result1,
            out Vector128<float> result2)
        {
            // Undo X=(L-M)/2 and Y=(L+M)/2, then remove the cube-root biases in the same order as the scalar path.
            Vector128<float> gammaR = (y + x) - Vector128.Create(this.cubeRootBiases.X);
            Vector128<float> gammaG = (y - x) - Vector128.Create(this.cubeRootBiases.Y);
            Vector128<float> gammaB = b - Vector128.Create(this.cubeRootBiases.Z);

            this.CubeAndTransform(gammaR, gammaG, gammaB, out result0, out result1, out result2);
        }

        /// <summary>
        /// Reconstructs 8 independent pixels; each register holds one component in pixel order.
        /// </summary>
        /// <param name="x">The X samples.</param>
        /// <param name="y">The Y samples.</param>
        /// <param name="b">The B samples.</param>
        /// <param name="result0">The first transformed components.</param>
        /// <param name="result1">The second transformed components.</param>
        /// <param name="result2">The third transformed components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector256<float> x,
            Vector256<float> y,
            Vector256<float> b,
            out Vector256<float> result0,
            out Vector256<float> result1,
            out Vector256<float> result2)
        {
            // Undo X=(L-M)/2 and Y=(L+M)/2, then remove the cube-root biases in the same order as the scalar path.
            Vector256<float> gammaR = (y + x) - Vector256.Create(this.cubeRootBiases.X);
            Vector256<float> gammaG = (y - x) - Vector256.Create(this.cubeRootBiases.Y);
            Vector256<float> gammaB = b - Vector256.Create(this.cubeRootBiases.Z);

            this.CubeAndTransform(gammaR, gammaG, gammaB, out result0, out result1, out result2);
        }

        /// <summary>
        /// Reconstructs 16 independent pixels; each register holds one component in pixel order.
        /// </summary>
        /// <param name="x">The X samples.</param>
        /// <param name="y">The Y samples.</param>
        /// <param name="b">The B samples.</param>
        /// <param name="result0">The first transformed components.</param>
        /// <param name="result1">The second transformed components.</param>
        /// <param name="result2">The third transformed components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            Vector512<float> x,
            Vector512<float> y,
            Vector512<float> b,
            out Vector512<float> result0,
            out Vector512<float> result1,
            out Vector512<float> result2)
        {
            // Undo X=(L-M)/2 and Y=(L+M)/2, then remove the cube-root biases in the same order as the scalar path.
            Vector512<float> gammaR = (y + x) - Vector512.Create(this.cubeRootBiases.X);
            Vector512<float> gammaG = (y - x) - Vector512.Create(this.cubeRootBiases.Y);
            Vector512<float> gammaB = b - Vector512.Create(this.cubeRootBiases.Z);

            this.CubeAndTransform(gammaR, gammaG, gammaB, out result0, out result1, out result2);
        }

        /// <summary>
        /// Restores linear components from the three cube-root absorbance components of one pixel.
        /// </summary>
        /// <param name="gamma">The cube-root absorbance components after the input encoding's offsets and domain rules.</param>
        /// <returns>The linear components in the coordinates selected by this operator's matrix.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 CubeAndTransform(Vector3 gamma)
        {
            // Cubing restores absorbance. The signed biases remove the forward transform's offsets.
            // Coding XYB retains signed roots; scaled device XYB applies its zero cutoff before this shared stage.
            Vector3 mixed = Vector3.MultiplyAddEstimate(gamma * gamma, gamma, this.biases);
            return Vector3.Transform(mixed, this.matrix);
        }

        /// <summary>
        /// Restores linear components from cube-root absorbance for 4 independent pixels.
        /// </summary>
        /// <param name="gammaR">The first cube-root absorbance components in pixel order.</param>
        /// <param name="gammaG">The second cube-root absorbance components in pixel order.</param>
        /// <param name="gammaB">The third cube-root absorbance components in pixel order.</param>
        /// <param name="result0">The first linear components in pixel order.</param>
        /// <param name="result1">The second linear components in pixel order.</param>
        /// <param name="result2">The third linear components in pixel order.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CubeAndTransform(
            Vector128<float> gammaR,
            Vector128<float> gammaG,
            Vector128<float> gammaB,
            out Vector128<float> result0,
            out Vector128<float> result1,
            out Vector128<float> result2)
        {
            // Each register holds one component from consecutive pixels. Cube, remove the signed biases,
            // then apply the prepared row-vector matrix in the same order as the scalar pixel operation.
            Vector128<float> mixedR = Vector128.MultiplyAddEstimate(gammaR * gammaR, gammaR, Vector128.Create(this.biases.X));
            Vector128<float> mixedG = Vector128.MultiplyAddEstimate(gammaG * gammaG, gammaG, Vector128.Create(this.biases.Y));
            Vector128<float> mixedB = Vector128.MultiplyAddEstimate(gammaB * gammaB, gammaB, Vector128.Create(this.biases.Z));
            SimdUtils.Transform3(in this.matrix, mixedR, mixedG, mixedB, out result0, out result1, out result2);
        }

        /// <summary>
        /// Restores linear components from cube-root absorbance for 8 independent pixels.
        /// </summary>
        /// <param name="gammaR">The first cube-root absorbance components in pixel order.</param>
        /// <param name="gammaG">The second cube-root absorbance components in pixel order.</param>
        /// <param name="gammaB">The third cube-root absorbance components in pixel order.</param>
        /// <param name="result0">The first linear components in pixel order.</param>
        /// <param name="result1">The second linear components in pixel order.</param>
        /// <param name="result2">The third linear components in pixel order.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CubeAndTransform(
            Vector256<float> gammaR,
            Vector256<float> gammaG,
            Vector256<float> gammaB,
            out Vector256<float> result0,
            out Vector256<float> result1,
            out Vector256<float> result2)
        {
            // Each register holds one component from consecutive pixels. Cube, remove the signed biases,
            // then apply the prepared row-vector matrix in the same order as the scalar pixel operation.
            Vector256<float> mixedR = Vector256.MultiplyAddEstimate(gammaR * gammaR, gammaR, Vector256.Create(this.biases.X));
            Vector256<float> mixedG = Vector256.MultiplyAddEstimate(gammaG * gammaG, gammaG, Vector256.Create(this.biases.Y));
            Vector256<float> mixedB = Vector256.MultiplyAddEstimate(gammaB * gammaB, gammaB, Vector256.Create(this.biases.Z));
            SimdUtils.Transform3(in this.matrix, mixedR, mixedG, mixedB, out result0, out result1, out result2);
        }

        /// <summary>
        /// Restores linear components from cube-root absorbance for 16 independent pixels.
        /// </summary>
        /// <param name="gammaR">The first cube-root absorbance components in pixel order.</param>
        /// <param name="gammaG">The second cube-root absorbance components in pixel order.</param>
        /// <param name="gammaB">The third cube-root absorbance components in pixel order.</param>
        /// <param name="result0">The first linear components in pixel order.</param>
        /// <param name="result1">The second linear components in pixel order.</param>
        /// <param name="result2">The third linear components in pixel order.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CubeAndTransform(
            Vector512<float> gammaR,
            Vector512<float> gammaG,
            Vector512<float> gammaB,
            out Vector512<float> result0,
            out Vector512<float> result1,
            out Vector512<float> result2)
        {
            // Each register holds one component from consecutive pixels. Cube, remove the signed biases,
            // then apply the prepared row-vector matrix in the same order as the scalar pixel operation.
            Vector512<float> mixedR = Vector512.MultiplyAddEstimate(gammaR * gammaR, gammaR, Vector512.Create(this.biases.X));
            Vector512<float> mixedG = Vector512.MultiplyAddEstimate(gammaG * gammaG, gammaG, Vector512.Create(this.biases.Y));
            Vector512<float> mixedB = Vector512.MultiplyAddEstimate(gammaB * gammaB, gammaB, Vector512.Create(this.biases.Z));
            SimdUtils.Transform3(in this.matrix, mixedR, mixedG, mixedB, out result0, out result1, out result2);
        }
    }
}
