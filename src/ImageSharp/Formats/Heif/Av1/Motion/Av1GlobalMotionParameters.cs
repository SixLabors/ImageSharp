// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Stores one AV1 global-motion model in the codec's fixed-point affine matrix domain.
/// </summary>
internal struct Av1GlobalMotionParameters
{
    /// <summary>
    /// The number of fractional bits carried by every stored matrix parameter.
    /// </summary>
    public const int ModelPrecisionBits = 16;

    /// <summary>
    /// The fixed-point representation of one in the global-motion matrix domain.
    /// </summary>
    public const int ModelScale = 1 << ModelPrecisionBits;

    /// <summary>
    /// The number of low-order bits removed from the derived shear parameters.
    /// </summary>
    private const int ShearParameterReductionBits = 6;

    /// <summary>
    /// The number of fractional bits carried by entries in <see cref="ReciprocalTable"/>.
    /// </summary>
    private const int ReciprocalPrecisionBits = 14;

    /// <summary>
    /// The number of divisor-fraction bits used to index <see cref="ReciprocalTable"/>.
    /// </summary>
    private const int ReciprocalIndexBits = 8;

    /// <summary>
    /// The six parameters ordered as horizontal translation, vertical translation, and the four affine coefficients.
    /// </summary>
    private InlineArray6<int> matrix;

    /// <summary>
    /// Gets an identity global-motion model.
    /// </summary>
    public static Av1GlobalMotionParameters Identity
    {
        get
        {
            Av1GlobalMotionParameters result = default;
            result.matrix[2] = ModelScale;
            result.matrix[5] = ModelScale;
            return result;
        }
    }

    /// <summary>
    /// Gets or sets the geometric model represented by the matrix parameters.
    /// </summary>
    public Av1GlobalMotionType Type { get; set; }

    /// <summary>
    /// Gets the reduced horizontal scale delta used by warped prediction.
    /// </summary>
    public short Alpha { get; private set; }

    /// <summary>
    /// Gets the reduced horizontal shear used by warped prediction.
    /// </summary>
    public short Beta { get; private set; }

    /// <summary>
    /// Gets the reduced vertical shear used by warped prediction.
    /// </summary>
    public short Gamma { get; private set; }

    /// <summary>
    /// Gets the reduced vertical scale delta used by warped prediction.
    /// </summary>
    public short Delta { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the affine model violates AV1's permitted shear bounds.
    /// </summary>
    public bool IsInvalid { get; private set; }

    /// <summary>
    /// Gets the fixed-point reciprocal lookup used by AV1's affine shear derivation.
    /// </summary>
    private static ReadOnlySpan<ushort> ReciprocalTable =>
    [
        16384, 16320, 16257, 16194, 16132, 16070, 16009, 15948, 15888, 15828, 15768,
        15709, 15650, 15592, 15534, 15477, 15420, 15364, 15308, 15252, 15197, 15142,
        15087, 15033, 14980, 14926, 14873, 14821, 14769, 14717, 14665, 14614, 14564,
        14513, 14463, 14413, 14364, 14315, 14266, 14218, 14170, 14122, 14075, 14028,
        13981, 13935, 13888, 13843, 13797, 13752, 13707, 13662, 13618, 13574, 13530,
        13487, 13443, 13400, 13358, 13315, 13273, 13231, 13190, 13148, 13107, 13066,
        13026, 12985, 12945, 12906, 12866, 12827, 12788, 12749, 12710, 12672, 12633,
        12596, 12558, 12520, 12483, 12446, 12409, 12373, 12336, 12300, 12264, 12228,
        12193, 12157, 12122, 12087, 12053, 12018, 11984, 11950, 11916, 11882, 11848,
        11815, 11782, 11749, 11716, 11683, 11651, 11619, 11586, 11555, 11523, 11491,
        11460, 11429, 11398, 11367, 11336, 11305, 11275, 11245, 11215, 11185, 11155,
        11125, 11096, 11067, 11038, 11009, 10980, 10951, 10923, 10894, 10866, 10838,
        10810, 10782, 10755, 10727, 10700, 10673, 10645, 10618, 10592, 10565, 10538,
        10512, 10486, 10460, 10434, 10408, 10382, 10356, 10331, 10305, 10280, 10255,
        10230, 10205, 10180, 10156, 10131, 10107, 10082, 10058, 10034, 10010, 9986,
        9963, 9939, 9916, 9892, 9869, 9846, 9823, 9800, 9777, 9754, 9732, 9709, 9687,
        9664, 9642, 9620, 9598, 9576, 9554, 9533, 9511, 9489, 9468, 9447, 9425, 9404,
        9383, 9362, 9341, 9321, 9300, 9279, 9259, 9239, 9218, 9198, 9178, 9158, 9138,
        9118, 9098, 9079, 9059, 9039, 9020, 9001, 8981, 8962, 8943, 8924, 8905, 8886,
        8867, 8849, 8830, 8812, 8793, 8775, 8756, 8738, 8720, 8702, 8684, 8666, 8648,
        8630, 8613, 8595, 8577, 8560, 8542, 8525, 8508, 8490, 8473, 8456, 8439, 8422,
        8405, 8389, 8372, 8355, 8339, 8322, 8306, 8289, 8273, 8257, 8240, 8224, 8208,
        8192,
    ];

    /// <summary>
    /// Gets or sets a matrix parameter in AV1 affine-transform order.
    /// </summary>
    /// <param name="index">The zero-based matrix parameter index.</param>
    /// <returns>The fixed-point matrix parameter.</returns>
    public int this[int index]
    {
        get => this.matrix[index];
        set => this.matrix[index] = value;
    }

    /// <summary>
    /// Derives the reduced shear parameters and records whether the complete affine model is valid.
    /// </summary>
    public void UpdateShearParameters()
    {
        Span<int> values = this.matrix;
        this.Alpha = 0;
        this.Beta = 0;
        this.Gamma = 0;
        this.Delta = 0;

        if (values[2] <= 0)
        {
            this.IsInvalid = true;
            return;
        }

        this.Alpha = (short)Math.Clamp(values[2] - ModelScale, short.MinValue, short.MaxValue);
        this.Beta = (short)Math.Clamp(values[3], short.MinValue, short.MaxValue);

        // AV1 derives gamma and delta by multiplying with a fixed-precision reciprocal of the horizontal scale.
        // The reciprocal lookup is normative; integer division would produce different warped sample positions.
        int reciprocal = ResolveDivisor((uint)values[2], out int reciprocalShift);
        long scaledVerticalCoefficient = (long)values[4] * ModelScale * reciprocal;
        this.Gamma = (short)Math.Clamp(RoundPowerOf2Signed(scaledVerticalCoefficient, reciprocalShift), short.MinValue, short.MaxValue);

        long scaledCrossCoefficient = (long)values[3] * values[4] * reciprocal;
        long verticalScaleDelta = values[5] - RoundPowerOf2Signed(scaledCrossCoefficient, reciprocalShift) - ModelScale;
        this.Delta = (short)Math.Clamp(verticalScaleDelta, short.MinValue, short.MaxValue);

        // Warped filtering addresses a coarser parameter grid than the stored affine matrix. Symmetric rounding is
        // required here so negative shear values are quantized identically to their positive counterparts.
        this.Alpha = ReduceShearParameter(this.Alpha);
        this.Beta = ReduceShearParameter(this.Beta);
        this.Gamma = ReduceShearParameter(this.Gamma);
        this.Delta = ReduceShearParameter(this.Delta);

        // These weighted L1 bounds are the AV1 validity test for the two shear axes. Equality is invalid because the
        // warped-filter footprint would no longer remain inside the permitted affine sampling envelope.
        this.IsInvalid =
            ((4 * Math.Abs((int)this.Alpha)) + (7 * Math.Abs((int)this.Beta)) >= ModelScale) ||
            ((4 * Math.Abs((int)this.Gamma)) + (4 * Math.Abs((int)this.Delta)) >= ModelScale);
    }

    /// <summary>
    /// Quantizes one signed shear parameter to AV1's warped-filter precision.
    /// </summary>
    /// <param name="value">The full-precision shear parameter.</param>
    /// <returns>The reduced shear parameter.</returns>
    private static short ReduceShearParameter(short value)
        => (short)(RoundPowerOf2Signed(value, ShearParameterReductionBits) * (1 << ShearParameterReductionBits));

    /// <summary>
    /// Resolves a positive divisor into AV1's fixed-point reciprocal representation.
    /// </summary>
    /// <param name="divisor">The positive divisor.</param>
    /// <param name="shift">Receives the reciprocal's binary scale.</param>
    /// <returns>The fixed-point reciprocal multiplier.</returns>
    private static int ResolveDivisor(uint divisor, out int shift)
    {
        // Normalize the divisor around its highest set bit, then quantize the remaining fraction to the normative
        // eight-bit table index. Adding the table's fourteen fractional bits yields the scale used by the caller's
        // rounded multiply instead of a platform-dependent integer division.
        shift = BitOperations.Log2(divisor);
        int remainder = (int)(divisor - (1U << shift));
        int reciprocalIndex = shift > ReciprocalIndexBits
            ? RoundPowerOf2(remainder, shift - ReciprocalIndexBits)
            : remainder << (ReciprocalIndexBits - shift);

        shift += ReciprocalPrecisionBits;
        return ReciprocalTable[reciprocalIndex];
    }

    /// <summary>
    /// Divides a nonnegative integer by a power of two with nearest-integer rounding.
    /// </summary>
    /// <param name="value">The nonnegative value.</param>
    /// <param name="bitCount">The base-two divisor exponent.</param>
    /// <returns>The rounded quotient.</returns>
    private static int RoundPowerOf2(int value, int bitCount)
        => (value + ((1 << bitCount) >> 1)) >> bitCount;

    /// <summary>
    /// Divides a signed integer by a power of two with symmetric nearest-integer rounding.
    /// </summary>
    /// <param name="value">The signed value.</param>
    /// <param name="bitCount">The base-two divisor exponent.</param>
    /// <returns>The rounded quotient.</returns>
    private static long RoundPowerOf2Signed(long value, int bitCount)
        => value < 0
            ? -(((-value) + ((1L << bitCount) >> 1)) >> bitCount)
            : (value + ((1L << bitCount) >> 1)) >> bitCount;

    /// <summary>
    /// Provides inline storage for the six parameters in an AV1 affine matrix.
    /// </summary>
    /// <typeparam name="T">The stored parameter type.</typeparam>
    [InlineArray(6)]
    private struct InlineArray6<T>
    {
        /// <summary>
        /// The first element in the compiler-expanded inline buffer.
        /// </summary>
        private T element;
    }
}
