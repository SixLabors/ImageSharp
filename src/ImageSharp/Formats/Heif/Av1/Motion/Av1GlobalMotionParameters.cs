// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

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
    /// The initial finite-subexponential group width used by every global-motion parameter.
    /// </summary>
    public const int SubexponentialGroupBitCount = 3;

    /// <summary>
    /// The finite signed-domain size parameter for coded affine coefficients.
    /// </summary>
    public const int AlphaValueMagnitude = (1 << 12) + 1;

    /// <summary>
    /// The number of fractional bits carried by coded affine coefficients.
    /// </summary>
    public const int AlphaPrecisionBits = 15;

    /// <summary>
    /// The precision increase from a coded affine coefficient to the stored matrix.
    /// </summary>
    public const int AlphaPrecisionDifference = ModelPrecisionBits - AlphaPrecisionBits;

    /// <summary>
    /// The scale factor that restores a coded affine coefficient to the stored matrix precision.
    /// </summary>
    public const int AlphaDecodeFactor = 1 << AlphaPrecisionDifference;

    /// <summary>
    /// The signed magnitude bit count of a general affine model's translation components.
    /// </summary>
    public const int AbsoluteTranslationBits = 12;

    /// <summary>
    /// The signed magnitude bit count of a translation-only model before precision adjustment.
    /// </summary>
    public const int AbsoluteTranslationOnlyBits = 9;

    /// <summary>
    /// The number of fractional bits carried by general affine translation components.
    /// </summary>
    public const int TranslationPrecisionBits = 6;

    /// <summary>
    /// The number of fractional bits carried by translation-only components.
    /// </summary>
    public const int TranslationOnlyPrecisionBits = 3;

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
    /// The largest difference between a retained neighbor motion vector and the current block motion vector.
    /// </summary>
    private const int LocalProjectionMotionVectorLimit = 256;

    /// <summary>
    /// The maximum magnitude of a non-diagonal affine coefficient relative to the identity matrix.
    /// </summary>
    private const int NonDiagonalAffineClamp = 1 << (ModelPrecisionBits - 3);

    /// <summary>
    /// The exclusive upper magnitude of either translation coefficient.
    /// </summary>
    private const int TranslationClamp = 128 << ModelPrecisionBits;

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
    /// Gets the translational motion vector represented by this model at the center of a coding block.
    /// </summary>
    /// <param name="allowHighPrecisionMotionVector">
    /// A value indicating whether motion vectors may retain one-eighth-sample precision.
    /// </param>
    /// <param name="blockSize">The coding block size.</param>
    /// <param name="modeInfoPosition">The block origin in 4x4 mode-information units.</param>
    /// <param name="forceIntegerMotionVector">
    /// A value indicating whether the result is rounded to an integer-sample displacement.
    /// </param>
    /// <returns>The global motion vector in one-eighth-sample units.</returns>
    public readonly Av1MotionVector GetMotionVector(
        bool allowHighPrecisionMotionVector,
        Av1BlockSize blockSize,
        Point modeInfoPosition,
        bool forceIntegerMotionVector)
    {
        if (this.Type == Av1GlobalMotionType.Identity)
        {
            return default;
        }

        int row;
        int column;
        if (this.Type == Av1GlobalMotionType.Translation)
        {
            // AV1 accidentally assigns the horizontal translation parameter to the row component and the vertical
            // parameter to the column component. Decoders preserve that published bitstream behavior for conformance.
            row = this.matrix[0] >> (ModelPrecisionBits - 3);
            column = this.matrix[1] >> (ModelPrecisionBits - 3);
        }
        else
        {
            int blockCenterX = (modeInfoPosition.X << Av1Constants.ModeInfoSizeLog2) + (blockSize.GetWidth() >> 1) - 1;
            int blockCenterY = (modeInfoPosition.Y << Av1Constants.ModeInfoSizeLog2) + (blockSize.GetHeight() >> 1) - 1;
            int horizontal = ((this.matrix[2] - ModelScale) * blockCenterX) +
                (this.matrix[3] * blockCenterY) +
                this.matrix[0];

            int vertical = (this.matrix[4] * blockCenterX) +
                ((this.matrix[5] - ModelScale) * blockCenterY) +
                this.matrix[1];

            int precisionBits = allowHighPrecisionMotionVector ? ModelPrecisionBits - 3 : ModelPrecisionBits - 2;
            column = Av1Math.RoundPowerOf2Signed(horizontal, precisionBits);
            row = Av1Math.RoundPowerOf2Signed(vertical, precisionBits);
            if (!allowHighPrecisionMotionVector)
            {
                column *= 2;
                row *= 2;
            }
        }

        return new Av1MotionVector(row, column).LowerPrecision(
            allowHighPrecision: allowHighPrecisionMotionVector,
            forceInteger: forceIntegerMotionVector);
    }

    /// <summary>
    /// Derives the local affine model for a warped inter block from its spatial neighbor samples.
    /// </summary>
    /// <param name="sourcePoints">The neighbor-center positions relative to the current block in one-eighth-sample units.</param>
    /// <param name="referencePoints">The corresponding positions in the selected reference frame.</param>
    /// <param name="blockSize">The current coding block size.</param>
    /// <param name="motionVector">The current block motion vector in one-eighth-sample units.</param>
    /// <param name="modeInfoPosition">The current block origin in 4x4 mode-information units.</param>
    /// <returns>The derived affine model, marked invalid when AV1's projection or shear constraints cannot be satisfied.</returns>
    public static Av1GlobalMotionParameters DeriveLocalProjection(
        ReadOnlySpan<Point> sourcePoints,
        ReadOnlySpan<Point> referencePoints,
        Av1BlockSize blockSize,
        Av1MotionVector motionVector,
        Point modeInfoPosition)
    {
        Av1GlobalMotionParameters result = Identity;
        result.Type = Av1GlobalMotionType.Affine;

        int blockWidth = blockSize.GetWidth();
        int blockHeight = blockSize.GetHeight();
        int sampleThreshold = Math.Clamp(Math.Max(blockWidth, blockHeight), 16, 112);
        bool hasSelectedSample = sourcePoints.Length == 1;
        if (sourcePoints.Length > 1)
        {
            for (int index = 0; index < sourcePoints.Length; index++)
            {
                int difference = Math.Abs(referencePoints[index].X - sourcePoints[index].X - motionVector.Column) +
                    Math.Abs(referencePoints[index].Y - sourcePoints[index].Y - motionVector.Row);

                hasSelectedSample |= difference <= sampleThreshold;
            }
        }

        int sourceCenterX = ((blockWidth >> 1) - 1) << 3;
        int sourceCenterY = ((blockHeight >> 1) - 1) << 3;
        int referenceCenterX = sourceCenterX + motionVector.Column;
        int referenceCenterY = sourceCenterY + motionVector.Row;
        int a00 = 0;
        int a01 = 0;
        int a11 = 0;
        int bx0 = 0;
        int bx1 = 0;
        int by0 = 0;
        int by1 = 0;

        for (int index = 0; index < sourcePoints.Length; index++)
        {
            int motionVectorDifference = Math.Abs(referencePoints[index].X - sourcePoints[index].X - motionVector.Column) +
                Math.Abs(referencePoints[index].Y - sourcePoints[index].Y - motionVector.Row);

            // av1_selectSamples retains the original first sample when every candidate exceeds the threshold. Keeping
            // that rule here is important because the selected Warped syntax still requires a deterministic model.
            if (sourcePoints.Length > 1 && motionVectorDifference > sampleThreshold && (hasSelectedSample || index != 0))
            {
                continue;
            }

            int sourceX = sourcePoints[index].X - sourceCenterX;
            int sourceY = sourcePoints[index].Y - sourceCenterY;
            int referenceX = referencePoints[index].X - referenceCenterX;
            int referenceY = referencePoints[index].Y - referenceCenterY;
            if (Math.Abs(sourceX - referenceX) >= LocalProjectionMotionVectorLimit ||
                Math.Abs(sourceY - referenceY) >= LocalProjectionMotionVectorLimit)
            {
                continue;
            }

            // These biased products are the normative reduced-precision P'P, P'q, and P'r matrices. Computing them
            // directly preserves the reference decoder's integer least-squares rounding instead of introducing floating-point drift.
            a00 += LeastSquaresSquare(sourceX);
            a01 += LeastSquaresProduct1(sourceX, sourceY);
            a11 += LeastSquaresSquare(sourceY);
            bx0 += LeastSquaresProduct2(sourceX, referenceX);
            bx1 += LeastSquaresProduct1(sourceY, referenceX);
            by0 += LeastSquaresProduct1(sourceX, referenceY);
            by1 += LeastSquaresProduct2(sourceY, referenceY);
        }

        long determinant = ((long)a00 * a11) - ((long)a01 * a01);
        if (determinant == 0)
        {
            result.IsInvalid = true;
            return result;
        }

        int inverseDeterminant = ResolveDivisor((ulong)Math.Abs(determinant), out int determinantShift) *
            (determinant < 0 ? -1 : 1);

        determinantShift -= ModelPrecisionBits;
        if (determinantShift < 0)
        {
            inverseDeterminant <<= -determinantShift;
            determinantShift = 0;
        }

        long projectionX0 = ((long)a11 * bx0) - ((long)a01 * bx1);
        long projectionX1 = -((long)a01 * bx0) + ((long)a00 * bx1);
        long projectionY0 = ((long)a11 * by0) - ((long)a01 * by1);
        long projectionY1 = -((long)a01 * by0) + ((long)a00 * by1);

        result.matrix[2] = ResolveProjectionCoefficient(
            projectionX0,
            inverseDeterminant,
            determinantShift,
            ModelScale - NonDiagonalAffineClamp + 1,
            ModelScale + NonDiagonalAffineClamp - 1);

        result.matrix[3] = ResolveProjectionCoefficient(
            projectionX1,
            inverseDeterminant,
            determinantShift,
            -NonDiagonalAffineClamp + 1,
            NonDiagonalAffineClamp - 1);

        result.matrix[4] = ResolveProjectionCoefficient(
            projectionY0,
            inverseDeterminant,
            determinantShift,
            -NonDiagonalAffineClamp + 1,
            NonDiagonalAffineClamp - 1);

        result.matrix[5] = ResolveProjectionCoefficient(
            projectionY1,
            inverseDeterminant,
            determinantShift,
            ModelScale - NonDiagonalAffineClamp + 1,
            ModelScale + NonDiagonalAffineClamp - 1);

        int absoluteCenterX = (modeInfoPosition.X << Av1Constants.ModeInfoSizeLog2) + (blockWidth >> 1) - 1;
        int absoluteCenterY = (modeInfoPosition.Y << Av1Constants.ModeInfoSizeLog2) + (blockHeight >> 1) - 1;
        int horizontalTranslation = (motionVector.Column << (ModelPrecisionBits - 3)) -
            (absoluteCenterX * (result.matrix[2] - ModelScale)) -
            (absoluteCenterY * result.matrix[3]);

        int verticalTranslation = (motionVector.Row << (ModelPrecisionBits - 3)) -
            (absoluteCenterX * result.matrix[4]) -
            (absoluteCenterY * (result.matrix[5] - ModelScale));

        result.matrix[0] = Math.Clamp(horizontalTranslation, -TranslationClamp, TranslationClamp - 1);
        result.matrix[1] = Math.Clamp(verticalTranslation, -TranslationClamp, TranslationClamp - 1);
        result.UpdateShearParameters();
        return result;
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
    /// Resolves a positive 64-bit divisor into AV1's fixed-point reciprocal representation.
    /// </summary>
    /// <param name="divisor">The positive divisor.</param>
    /// <param name="shift">Receives the reciprocal's binary scale.</param>
    /// <returns>The fixed-point reciprocal multiplier.</returns>
    private static int ResolveDivisor(ulong divisor, out int shift)
    {
        shift = BitOperations.Log2(divisor);
        ulong remainder = divisor - (1UL << shift);
        int reciprocalIndex = shift > ReciprocalIndexBits
            ? (int)((remainder + (1UL << (shift - ReciprocalIndexBits - 1))) >> (shift - ReciprocalIndexBits))
            : (int)(remainder << (ReciprocalIndexBits - shift));

        shift += ReciprocalPrecisionBits;
        return ReciprocalTable[reciprocalIndex];
    }

    /// <summary>
    /// Resolves one adjugate numerator into a clamped affine matrix coefficient.
    /// </summary>
    private static int ResolveProjectionCoefficient(long numerator, int inverseDeterminant, int shift, int minimum, int maximum)
    {
        long product = numerator * inverseDeterminant;
        long value = shift > 0 ? RoundPowerOf2Signed(product, shift) : product << -shift;
        return (int)Math.Clamp(value, minimum, maximum);
    }

    /// <summary>
    /// Computes one reduced-precision diagonal element of the local projection matrix.
    /// </summary>
    private static int LeastSquaresSquare(int value)
        => ((value * value * 4) + (value * 32) + 128) >> 4;

    /// <summary>
    /// Computes one reduced-precision off-diagonal product of the local projection matrix.
    /// </summary>
    private static int LeastSquaresProduct1(int first, int second)
        => ((first * second * 4) + ((first + second) * 16) + 64) >> 4;

    /// <summary>
    /// Computes one reduced-precision source-to-reference product of the local projection matrix.
    /// </summary>
    private static int LeastSquaresProduct2(int first, int second)
        => ((first * second * 4) + ((first + second) * 16) + 128) >> 4;

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
}
