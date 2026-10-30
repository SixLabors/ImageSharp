// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
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
    private int planeLength;

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
    /// Gets the number of integer values required for two rows of combined and individual prediction errors.
    /// </summary>
    /// <param name="width">The validated modular channel width.</param>
    /// <returns>The complete reusable scratch length.</returns>
    /// <exception cref="InvalidMemoryOperationException">The complete row layout exceeds the supported span length.</exception>
    public static int GetStorageLength(int width)
    {
        // Palette tables and reconstructed channels can be wider than coding groups.
        // Widen before sizing all five two-row planes; their shared span and indices must fit in an int.
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
        // Plane zero stores signed combined errors. The next four planes store unsigned predictor errors
        // in the same integer words. Every plane has two alternating rows and two trailing margin entries.
        this.rowStride = width + RowMargin;
        this.planeLength = this.rowStride * StoredRows;

        // The shared scratch can belong to a wider channel. Only these five two-row planes take part in the current prediction.
        errors[..(this.planeLength * (PredictorCount + 1))].Clear();
    }

    /// <summary>
    /// Calculates the weighted prediction and its signed error property before decoding the current residual.
    /// </summary>
    /// <param name="errors">The retained channel error planes.</param>
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
        InlineArray4<uint> weights = default;
        uint weightSum = 0;

        // The preceding error row accumulates westward contributions as this row advances. Its N, NE and NW
        // entries therefore measure each predictor's local error. Reduce magnitudes to the 64-entry reciprocal table.
        for (int i = 0; i < PredictorCount; i++)
        {
            int offset = (i + 1) * this.planeLength;
            uint error = unchecked((uint)errors[offset + north] + (uint)errors[offset + northEast] + (uint)errors[offset + northWest]);
            int shift = Math.Max(0, BitOperations.Log2((ulong)error + 1) - ErrorWeightMagnitudeBits);
            uint weight = MinimumWeight + ((this.maximumWeights[i] * Reciprocals[(int)(error >> shift)]) >> shift);
            weights[i] = weight;
            weightSum += weight;
        }

        long errorWest = x == 0 ? 0 : errors[currentRow + x - 1];
        long errorNorth = errors[north];
        long errorNorthWest = errors[northWest];
        long errorNorthEast = errors[northEast];
        long largestError = errorWest;
        if (Math.Abs(errorNorth) > Math.Abs(largestError))
        {
            largestError = errorNorth;
        }

        if (Math.Abs(errorNorthWest) > Math.Abs(largestError))
        {
            largestError = errorNorthWest;
        }

        if (Math.Abs(errorNorthEast) > Math.Abs(largestError))
        {
            largestError = errorNorthEast;
        }

        property = (int)largestError;

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

        // Each initial weight is at least four. Scaling by their common magnitude retains a positive
        // sum no greater than 31. The reciprocal table replaces division with the exact format approximation.
        int weightShift = BitOperations.Log2(weightSum) - NormalizedWeightBits;
        weightSum = 0;
        for (int i = 0; i < PredictorCount; i++)
        {
            weights[i] >>= weightShift;
            weightSum += weights[i];
        }

        long sum = (weightSum >> 1) - 1;
        for (int i = 0; i < PredictorCount; i++)
        {
            sum += this.predictions[i] * weights[i];
        }

        long prediction = (sum * Reciprocals[(int)weightSum - 1]) >> ReciprocalBits;
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
    /// <param name="errors">The retained channel error planes.</param>
    /// <param name="x">The reconstructed column.</param>
    /// <param name="y">The reconstructed row.</param>
    /// <param name="value">The reconstructed signed sample.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(Span<int> errors, int x, int y, int value)
    {
        int currentRow = (y & 1) == 0 ? this.rowStride : 0;
        int previousRow = (y & 1) == 0 ? 0 : this.rowStride;
        long scaled = (long)value << ExtraBits;
        errors[currentRow + x] = unchecked((int)(this.combinedPrediction - scaled));

        // Keep unsigned error magnitudes as raw integer words. Adding into the preceding row's next
        // column supplies the west and west-west contributions used by the next Predict call.
        for (int i = 0; i < PredictorCount; i++)
        {
            uint error = unchecked((uint)((Math.Abs(this.predictions[i] - scaled) + PredictionRounding) >> ExtraBits));
            int offset = (i + 1) * this.planeLength;
            errors[offset + currentRow + x] = unchecked((int)error);
            errors[offset + previousRow + x + 1] = unchecked(errors[offset + previousRow + x + 1] + (int)error);
        }
    }
}
