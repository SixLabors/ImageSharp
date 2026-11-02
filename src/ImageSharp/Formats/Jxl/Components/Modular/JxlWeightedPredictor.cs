// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Modular;

/// <summary>
/// Combines four modular predictions with weights derived from their preceding reconstruction errors.
/// </summary>
/// <remarks>
/// The decoder owns the error storage and reuses it between channels. Prediction and error updates follow sample order on one thread.
/// Arithmetic follows the weighted predictor in ISO/IEC 18181-1, as implemented by libjxl's context_predict.h.
/// </remarks>
internal struct JxlWeightedPredictor
{
    /// <summary>
    /// The number of candidate predictions combined for each sample.
    /// </summary>
    private const int PredictorCount = 4;

    /// <summary>
    /// The current and preceding error rows retained for each prediction.
    /// </summary>
    private const int StoredRows = 2;

    /// <summary>
    /// The trailing row margin used by the native error-update layout.
    /// </summary>
    private const int RowMargin = 2;

    /// <summary>
    /// The fractional precision retained in candidate and combined predictions.
    /// </summary>
    private const int ExtraBits = 3;

    /// <summary>
    /// The format's rounding offset before removing the three fractional bits.
    /// </summary>
    private const int PredictionRounding = (1 << (ExtraBits - 1)) - 1;

    /// <summary>
    /// The encoded width of each prediction-correction coefficient.
    /// </summary>
    private const int CoefficientBits = 5;

    /// <summary>
    /// The shift that divides the weighted error corrections by 32.
    /// </summary>
    private const int CorrectionShift = 5;

    /// <summary>
    /// The encoded width of each predictor's maximum weight.
    /// </summary>
    private const int MaximumWeightBits = 4;

    /// <summary>
    /// The minimum weight that keeps the combined weight sum positive after normalization.
    /// </summary>
    private const uint MinimumWeight = 4;

    /// <summary>
    /// The retained error magnitude that keeps reciprocal indices in the range zero through 63.
    /// </summary>
    private const int ErrorWeightMagnitudeBits = 5;

    /// <summary>
    /// The retained weight-sum magnitude that bounds the normalized sum to 31.
    /// </summary>
    private const int NormalizedWeightBits = 4;

    /// <summary>
    /// The fractional precision in the format's integer reciprocal table.
    /// </summary>
    private const int ReciprocalBits = 24;

    private byte northPredictionCoefficient;
    private byte westPredictionCoefficient;
    private byte northWestErrorCoefficient;
    private byte northErrorCoefficient;
    private byte northEastErrorCoefficient;
    private byte northDifferenceCoefficient;
    private byte westDifferenceCoefficient;
    private InlineArray4<uint> maximumWeights;
    private InlineArray4<long> predictions;
    private long combinedPrediction;
    private int rowStride;
    private int combinedErrorLength;

    /// <summary>
    /// Gets floor(2^24 / (index + 1)) for indices zero through 63. The format uses these exact integer reciprocals.
    /// </summary>
    private static ReadOnlySpan<uint> Reciprocals =>
    [
        16777216, 8388608, 5592405, 4194304, 3355443, 2796202, 2396745, 2097152,
        1864135, 1677721, 1525201, 1398101, 1290555, 1198372, 1118481, 1048576,
        986895, 932067, 883011, 838860, 798915, 762600, 729444, 699050,
        671088, 645277, 621378, 599186, 578524, 559240, 541200, 524288,
        508400, 493447, 479349, 466033, 453438, 441505, 430185, 419430,
        409200, 399457, 390167, 381300, 372827, 364722, 356962, 349525,
        342392, 335544, 328965, 322638, 316551, 310689, 305040, 299593,
        294337, 289262, 284359, 279620, 275036, 270600, 266305, 262144
    ];

    /// <summary>
    /// Reads the modular group's weighted prediction coefficients, including the serialized all-default flag.
    /// </summary>
    /// <param name="reader">The group reader before the weighted parameter header.</param>
    /// <returns>The retained coefficients and uninitialized channel state.</returns>
    public static JxlWeightedPredictor Read(ref JxlBitReader reader)
    {
        JxlWeightedPredictor result = default;
        if (reader.ReadBoolean())
        {
            result.northPredictionCoefficient = 16;
            result.westPredictionCoefficient = 10;
            result.northWestErrorCoefficient = 7;
            result.northErrorCoefficient = 7;
            result.northEastErrorCoefficient = 7;
            result.maximumWeights[0] = 13;
            result.maximumWeights[1] = result.maximumWeights[2] = result.maximumWeights[3] = 12;
        }
        else
        {
            // Seven correction coefficients precede the four maximum weights. No predictor state is serialized here.
            result.northPredictionCoefficient = (byte)reader.ReadBits(CoefficientBits);
            result.westPredictionCoefficient = (byte)reader.ReadBits(CoefficientBits);
            result.northWestErrorCoefficient = (byte)reader.ReadBits(CoefficientBits);
            result.northErrorCoefficient = (byte)reader.ReadBits(CoefficientBits);
            result.northEastErrorCoefficient = (byte)reader.ReadBits(CoefficientBits);
            result.northDifferenceCoefficient = (byte)reader.ReadBits(CoefficientBits);
            result.westDifferenceCoefficient = (byte)reader.ReadBits(CoefficientBits);
            for (int i = 0; i < PredictorCount; i++)
            {
                result.maximumWeights[i] = reader.ReadBits(MaximumWeightBits);
            }
        }

        return result;
    }

    /// <summary>
    /// Calculates the format's four exact integer predictor weights from wrapped local error sums.
    /// </summary>
    /// <param name="errors">Four unsigned error sums in predictor order, including values across the complete uint range.</param>
    /// <param name="maximumWeights">The corresponding four-bit maximum weights. Each lane must be from zero through 15.</param>
    /// <returns>The unnormalized integer weights in predictor order, each with the format's minimum contribution of four.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<uint> CalculateWeights(Vector128<uint> errors, Vector128<uint> maximumWeights)
    {
        // Lanes identify four predictors for this sample. For a nonzero uint value,
        // floor(log2(value)) = 31-LZCNT(value). Error+1 needs a separate all-ones lane:
        // its mathematical value is 2^32, so the required shift is 32-5 rather than the wrapped zero's shift.
        Vector128<uint> incremented = errors + Vector128<uint>.One;
        Vector128<int> shifts = Vector128.Max(
            Vector128.Create(31 - ErrorWeightMagnitudeBits) - Vector128_.LeadingZeroCount(incremented).AsInt32(),
            Vector128<int>.Zero);

        Vector128<uint> counts = Vector128.ConditionalSelect(
            Vector128.Equals(errors, Vector128<uint>.AllBitsSet),
            Vector128.Create((uint)(32 - ErrorWeightMagnitudeBits)),
            shifts.AsUInt32());

        // Shifts are 0..27 and every index is 0..63, including index 31 for uint.MaxValue.
        // Gather uses the existing exact reciprocal table; its values fit signed 32-bit words too.
        // A four-bit maximum weight times 2^24 is at most 251658240, so the product fits uint.
        Vector128<uint> indices = Vector128_.ShiftRightLogicalVariable(errors, counts);
        ref uint table = ref MemoryMarshal.GetReference(Reciprocals);
        Vector128<uint> reciprocals = Vector128_.Gather(ref Unsafe.As<uint, int>(ref table), indices.AsInt32()).AsUInt32();
        return Vector128_.ShiftRightLogicalVariable(maximumWeights * reciprocals, counts) + Vector128.Create((uint)MinimumWeight);
    }

    /// <summary>
    /// Gets the number of integer values required for two rows of combined and individual prediction errors.
    /// </summary>
    /// <param name="width">The validated modular channel width.</param>
    /// <returns>The complete reusable scratch length.</returns>
    /// <exception cref="InvalidMemoryOperationException">The complete row layout exceeds the supported span length.</exception>
    public static int GetStorageLength(int width)
    {
        // Palette tables and reconstructed channels can be wider than coding groups.
        // Widen before sizing two rows with one combined error and four predictor errors per column.
        // The complete shared span and all indices must fit in an int.
        long length = ((long)width + RowMargin) * StoredRows * (PredictorCount + 1);
        if (length > int.MaxValue)
        {
            throw new InvalidMemoryOperationException("The JPEG XL weighted prediction rows exceed the supported buffer length.");
        }

        return (int)length;
    }

    /// <summary>
    /// Clears preceding-channel errors and prepares the row layout for the next channel.
    /// </summary>
    /// <param name="errors">The decoder-owned scratch with at least <see cref="GetStorageLength"/> values for this width.</param>
    /// <param name="width">The channel width.</param>
    public void Reset(Span<int> errors, int width)
    {
        // The first two rows store signed combined errors. The remaining storage holds two rows of
        // adjacent [predictor0,predictor1,predictor2,predictor3] unsigned errors for each column. Every
        // row retains two trailing margin entries. The total remains five error words per column in each retained row.
        this.rowStride = width + RowMargin;
        this.combinedErrorLength = this.rowStride * StoredRows;

        // Clear only the combined and interleaved error rows used by this channel; the shared scratch can belong to a wider channel.
        errors[..(this.combinedErrorLength * (PredictorCount + 1))].Clear();
    }

    /// <summary>
    /// Calculates the weighted prediction and its signed error property before decoding the current residual.
    /// </summary>
    /// <param name="errors">The two scalar combined-error rows followed by the two interleaved predictor-error rows.</param>
    /// <param name="x">The current column.</param>
    /// <param name="y">The current row.</param>
    /// <param name="left">The preceding sample, with the modular edge substitution already applied.</param>
    /// <param name="top">The sample in the preceding row.</param>
    /// <param name="topLeft">The preceding row's preceding column.</param>
    /// <param name="topRight">The preceding row's following column.</param>
    /// <param name="topTop">The sample two rows before the current row.</param>
    /// <param name="property">The signed preceding error with the largest magnitude, used by tree property 15.</param>
    /// <returns>The prediction in native sample units.</returns>
    public long Predict(ReadOnlySpan<int> errors, int x, int y, int left, int top, int topLeft, int topRight, int topTop, out int property)
    {
        int currentRow = (y & 1) == 0 ? this.rowStride : 0;
        int previousRow = (y & 1) == 0 ? 0 : this.rowStride;
        int north = previousRow + x;
        int northEast = x + 1 < this.rowStride - RowMargin ? north + 1 : north;
        int northWest = x == 0 ? north : north - 1;

        // Fixed lanes identify the four predictors for this one sample, not four image columns.
        // Each wrapped sum combines the previous row's N, NE and NW errors exactly as libjxl and
        // jxl-rs predict.rs do. Each load contains all four predictors at one neighboring column.
        ref int firstError = ref MemoryMarshal.GetReference(errors);
        ref uint errorWords = ref Unsafe.As<int, uint>(ref firstError);
        int predictorBase = this.combinedErrorLength;
        Vector128<uint> summedErrors = Vector128.LoadUnsafe(ref errorWords, (nuint)(predictorBase + (north * PredictorCount)))
            + Vector128.LoadUnsafe(ref errorWords, (nuint)(predictorBase + (northEast * PredictorCount)))
            + Vector128.LoadUnsafe(ref errorWords, (nuint)(predictorBase + (northWest * PredictorCount)));

        // Reset reserves both rows with a two-word margin, and callers traverse 0 <= x < width.
        // These scalar indices are therefore within the first two rows, just like the vector indices above.
        // Borrow the same managed reference so each sample does not repeat the already established bounds checks.
        long errorWest = x == 0 ? 0 : Unsafe.Add(ref firstError, currentRow + x - 1);
        long errorNorth = Unsafe.Add(ref firstError, north);
        long errorNorthWest = Unsafe.Add(ref firstError, northWest);
        long errorNorthEast = Unsafe.Add(ref firstError, northEast);

        // The four signed errors come from int storage. Integer SIMD absolute value retains the
        // int.MinValue bit pattern; treating the magnitudes as uint represents its exact value 2^31.
        // Strict comparisons retain W, N, NW, NE tie order without long absolute-value overflow checks.
        Vector128<int> signedErrors = Vector128.Create((int)errorWest, (int)errorNorth, (int)errorNorthWest, (int)errorNorthEast);
        Vector128<uint> magnitudes = Vector128.Abs(signedErrors).AsUInt32();

        if (Vector128.IsHardwareAccelerated)
        {
            const byte SwapAdjacentErrors = 0xB1;
            const byte SwapErrorPairs = 0x4E;

            // Adjacent lanes compare [W,N] and [NW,NE]. Shuffle control 0xB1 selects [N,W,NE,NW].
            // Replace a signed candidate only for a strictly greater unsigned magnitude. Lane zero therefore
            // keeps W before N on ties; lane two keeps NW before NE. int.MinValue retains magnitude 2^31.
            Vector128<uint> adjacentMagnitudes = Vector128_.ShuffleNative(magnitudes.AsInt32(), SwapAdjacentErrors).AsUInt32();
            Vector128<int> swappedErrors = Vector128_.ShuffleNative(signedErrors, SwapAdjacentErrors);
            Vector128<int> takeAdjacent = Vector128.GreaterThan(adjacentMagnitudes, magnitudes).AsInt32();
            Vector128<int> winners = Vector128.ConditionalSelect(takeAdjacent, swappedErrors, signedErrors);
            Vector128<uint> pairMagnitudes = Vector128.Max(magnitudes, adjacentMagnitudes);

            // Each pair now has the same maximum magnitude in both lanes. Control 0x4E selects [2,3,0,1],
            // bringing the NW/NE winner to lane zero. A strict comparison keeps W/N ahead of NW/NE on ties.
            Vector128<uint> otherMagnitudes = Vector128_.ShuffleNative(pairMagnitudes.AsInt32(), SwapErrorPairs).AsUInt32();
            Vector128<int> otherWinners = Vector128_.ShuffleNative(winners, SwapErrorPairs);
            Vector128<int> takeOther = Vector128.GreaterThan(otherMagnitudes, pairMagnitudes).AsInt32();
            property = Vector128.ConditionalSelect(takeOther, otherWinners, winners).ToScalar();
        }
        else
        {
            long largestError = errorWest;
            uint largestMagnitude = magnitudes.GetElement(0);

            if (magnitudes.GetElement(1) > largestMagnitude)
            {
                largestError = errorNorth;
                largestMagnitude = magnitudes.GetElement(1);
            }

            if (magnitudes.GetElement(2) > largestMagnitude)
            {
                largestError = errorNorthWest;
                largestMagnitude = magnitudes.GetElement(2);
            }

            if (magnitudes.GetElement(3) > largestMagnitude)
            {
                largestError = errorNorthEast;
            }

            property = (int)largestError;
        }

        // Predictions retain three fractional bits. Widen native samples before shifting and preserve
        // arithmetic right shifts in each correction. Floating-point division changes the specified rounding.
        long n = (long)top << ExtraBits;
        long w = (long)left << ExtraBits;
        long ne = (long)topRight << ExtraBits;
        long nw = (long)topLeft << ExtraBits;
        long nn = (long)topTop << ExtraBits;
        long adjacentErrors = errorNorth + errorWest;
        this.predictions[0] = w + ne - n;
        this.predictions[1] = n - (((adjacentErrors + errorNorthEast) * this.northPredictionCoefficient) >> CorrectionShift);
        this.predictions[2] = w - (((adjacentErrors + errorNorthWest) * this.westPredictionCoefficient) >> CorrectionShift);
        this.predictions[3] = n - (((errorNorthWest * this.northWestErrorCoefficient) + (errorNorth * this.northErrorCoefficient)
            + (errorNorthEast * this.northEastErrorCoefficient) + ((nn - n) * this.northDifferenceCoefficient)
            + ((nw - w) * this.westDifferenceCoefficient)) >> CorrectionShift);

        long prediction = this.CombinePredictions(summedErrors);
        if (((errorNorth ^ errorWest) | (errorNorth ^ errorNorthWest)) <= 0)
        {
            // The format bounds a prediction by W, NE and N only for this error-sign condition.
            // Preserve the unbounded prediction otherwise; changing this condition changes decoded samples.
            prediction = Math.Clamp(prediction, Math.Min(w, Math.Min(ne, n)), Math.Max(w, Math.Max(ne, n)));
        }

        this.combinedPrediction = prediction;
        return (prediction + PredictionRounding) >> ExtraBits;
    }

    /// <summary>
    /// Updates all prediction errors from the reconstructed sample, even when its leaf selected another predictor.
    /// </summary>
    /// <param name="errors">The two scalar combined-error rows followed by the two interleaved predictor-error rows.</param>
    /// <param name="x">The reconstructed column.</param>
    /// <param name="y">The reconstructed row.</param>
    /// <param name="value">The reconstructed signed sample.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(Span<int> errors, int x, int y, int value)
    {
        int currentRow = (y & 1) == 0 ? this.rowStride : 0;
        int previousRow = (y & 1) == 0 ? 0 : this.rowStride;
        long scaled = (long)value << ExtraBits;

        // The validated column and Reset's two-row layout bound this index below combinedErrorLength.
        // Use the same reference for the scalar combined error and the interleaved vector errors below.
        ref int firstError = ref MemoryMarshal.GetReference(errors);
        Unsafe.Add(ref firstError, currentRow + x) = unchecked((int)(this.combinedPrediction - scaled));

        // The four lanes depend on this same reconstructed sample. They can be computed together,
        // although the predictor state still advances sequentially from one image column to the next.
        // Format-bounded predictions and 32-bit samples keep each difference and its absolute value in long.
        // A logical shift removes the three fractional bits. The direct vector operator keeps the
        // shift in the arithmetic expression at every width; narrowing below retains the low 32 bits.
        Vector128<uint> magnitudes;
        ref long prediction = ref this.predictions[0];
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<long> difference = Vector256.LoadUnsafe(ref prediction) - Vector256.Create(scaled);
            Vector256<long> absolute = Vector256.Abs(difference) + Vector256.Create((long)PredictionRounding);
            Vector256<long> rounded = absolute >>> ExtraBits;

            // Narrow discards high bits; it does not saturate. This retains the native uint conversion,
            // including wrapped error magnitudes. Only the populated four-lane lower half is stored.
            magnitudes = Vector256.Narrow(rounded, Vector256<long>.Zero).GetLower().AsUInt32();
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128<long> scaledValue = Vector128.Create(scaled);
            Vector128<long> rounding = Vector128.Create((long)PredictionRounding);
            Vector128<long> first = Vector128.Abs(Vector128.LoadUnsafe(ref prediction) - scaledValue) + rounding;
            Vector128<long> second = Vector128.Abs(Vector128.LoadUnsafe(ref prediction, (nuint)Vector128<long>.Count) - scaledValue) + rounding;
            first >>>= ExtraBits;
            second >>>= ExtraBits;
            magnitudes = Vector128.Narrow(first, second).AsUInt32();
        }
        else
        {
            // Scalar equations retain the same long arithmetic, rounding and conversion to the low 32 bits.
            magnitudes = Vector128.Create(
                unchecked((uint)((Math.Abs(this.predictions[0] - scaled) + PredictionRounding) >> ExtraBits)),
                unchecked((uint)((Math.Abs(this.predictions[1] - scaled) + PredictionRounding) >> ExtraBits)),
                unchecked((uint)((Math.Abs(this.predictions[2] - scaled) + PredictionRounding) >> ExtraBits)),
                unchecked((uint)((Math.Abs(this.predictions[3] - scaled) + PredictionRounding) >> ExtraBits)));
        }

        // Store one current-column group and add into the previous row's next group. That wrapped
        // addition supplies the westward contributions consumed by the next Predict call. The row margin
        // guarantees a complete four-lane group even when this is the last visible image column.
        ref uint errorWords = ref Unsafe.As<int, uint>(ref firstError);
        nuint current = (nuint)(this.combinedErrorLength + ((currentRow + x) * PredictorCount));
        nuint next = (nuint)(this.combinedErrorLength + ((previousRow + x + 1) * PredictorCount));
        magnitudes.StoreUnsafe(ref errorWords, current);
        (Vector128.LoadUnsafe(ref errorWords, next) + magnitudes).StoreUnsafe(ref errorWords, next);
    }

    /// <summary>
    /// Converts one wrapped local error sum to the format's positive predictor weight.
    /// </summary>
    /// <param name="error">The wrapped unsigned sum of north, northeast and northwest error magnitudes.</param>
    /// <param name="maximumWeight">The four-bit maximum weight read from the group header.</param>
    /// <returns>The integer reciprocal weight, including the minimum contribution of four.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetWeight(uint error, uint maximumWeight)
    {
        // All four predictor weights use this equation for every sample. Keep it in the caller, as in
        // jxl-rs predict.rs, so the four local error lanes do not cross separate call boundaries.
        // Widen before adding one: uint.MaxValue must have log2(error+1)=32. This shift bounds the
        // reciprocal index below 64 even at that endpoint, without changing the wrapped local error sum.
        int shift = Math.Max(0, BitOperations.Log2((ulong)error + 1) - ErrorWeightMagnitudeBits);
        return MinimumWeight + ((maximumWeight * Reciprocals[(int)(error >> shift)]) >> shift);
    }

    /// <summary>
    /// Combines the four retained predictions with exact integer weights from their wrapped local error sums.
    /// </summary>
    /// <param name="errors">Four unsigned lanes in predictor order, each containing the wrapped N, NE and NW error sum.</param>
    /// <returns>The weighted prediction with its three fractional bits, before the error-sign clamp.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long CombinePredictions(Vector128<uint> errors)
    {
        if (Avx512CD.VL.IsSupported || AdvSimd.IsSupported)
        {
            ref uint firstMaximum = ref Unsafe.As<InlineArray4<uint>, uint>(ref this.maximumWeights);
            Vector128<uint> weights = CalculateWeights(errors, Vector128.LoadUnsafe(ref firstMaximum));

            // A four-bit maximum weight times 2^24 is at most 251658240. Each resulting weight is
            // at most 251658244; their sum fits uint. The shared magnitude normalization leaves a
            // positive sum no greater than 31, exactly as the scalar equations below do.
            uint weightSum = Vector128.Sum(weights);
            int weightShift = BitOperations.Log2(weightSum) - NormalizedWeightBits;
            weights >>= weightShift;
            weightSum = Vector128.Sum(weights);
            long sum = (weightSum >> 1) - 1;

            if (Avx512DQ.VL.IsSupported)
            {
                // Four uint weights widen to exactly four long lanes. DQ supplies native low-64-bit
                // multiplication. Integer products and sums preserve the scalar result without floating-point rounding.
                // Scaled int samples have magnitude at most 2^34. The five-bit correction coefficients keep each prediction
                // below 2^37 in magnitude; normalized weights total at most 31, so every dot-product partial fits long.
                Vector256<long> wideWeights = Vector256.Create(Vector128.WidenLower(weights), Vector128.WidenUpper(weights)).AsInt64();
                ref long firstPrediction = ref Unsafe.As<InlineArray4<long>, long>(ref this.predictions);
                sum += Vector256.Sum(Vector256.LoadUnsafe(ref firstPrediction) * wideWeights);
            }
            else
            {
                // Targets without DQ retain four scalar products instead of emulating long-lane multiplication.
                // This also keeps ARM's native four-lane weight arithmetic without requiring a nonexistent general long multiply.
                sum += this.predictions[0] * weights.GetElement(0);
                sum += this.predictions[1] * weights.GetElement(1);
                sum += this.predictions[2] * weights.GetElement(2);
                sum += this.predictions[3] * weights.GetElement(3);
            }

            return (sum * Reciprocals[(int)weightSum - 1]) >> ReciprocalBits;
        }

        // Keep the original scalar weight, normalization and dot-product flow on targets without native
        // lane-wise leading-zero count. In particular, AVX2-only targets do not pack and extract weights.
        uint weight0 = GetWeight(errors.GetElement(0), this.maximumWeights[0]);
        uint weight1 = GetWeight(errors.GetElement(1), this.maximumWeights[1]);
        uint weight2 = GetWeight(errors.GetElement(2), this.maximumWeights[2]);
        uint weight3 = GetWeight(errors.GetElement(3), this.maximumWeights[3]);
        uint scalarWeightSum = weight0 + weight1 + weight2 + weight3;
        int scalarWeightShift = BitOperations.Log2(scalarWeightSum) - NormalizedWeightBits;
        weight0 >>= scalarWeightShift;
        weight1 >>= scalarWeightShift;
        weight2 >>= scalarWeightShift;
        weight3 >>= scalarWeightShift;
        scalarWeightSum = weight0 + weight1 + weight2 + weight3;
        long scalarSum = (scalarWeightSum >> 1) - 1;
        scalarSum += this.predictions[0] * weight0;
        scalarSum += this.predictions[1] * weight1;
        scalarSum += this.predictions[2] * weight2;
        scalarSum += this.predictions[3] * weight3;

        return (scalarSum * Reciprocals[(int)scalarWeightSum - 1]) >> ReciprocalBits;
    }
}
