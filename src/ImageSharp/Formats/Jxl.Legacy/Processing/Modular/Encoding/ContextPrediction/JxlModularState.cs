// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// Owns reusable weighted-prediction histories for one modular operation.
/// </summary>
/// <remarks>
/// Sequential channels reset and reuse one slot. General palette search keeps one slot per interleaved channel.
/// Reset binds the current header and clears active history before prediction starts. The owner is not shared by concurrent operations.
/// </remarks>
internal sealed class JxlModularState : IDisposable
{
    /// <summary>
    /// Rounding constant for predictions.
    /// </summary>
    private const long PredictionRound = ((1 << 3) >> 1) - 1;

    // Each slot starts with four candidate predictions and the final prediction (five Int64 values = ten UInt32 words).
    // It then stores two signed-error rows and two predictor-error rows. Four predictor errors are adjacent at each position.
    private const int PredictorCount = 4;
    private const int HistoryRows = 2;

    // Keep the native two-position row margin. Error updates can accumulate into the column immediately after the logical row.
    private const int RowMargin = 2;
    private const int PredictionWords = (PredictorCount + 1) * (sizeof(long) / sizeof(uint));
    private const int HistoryWordsPerPosition = HistoryRows * (PredictorCount + 1);
    private readonly IMemoryOwner<uint> storageOwner;
    private readonly int wordsPerSlot;
    private readonly int channelCount;
    private JxlModularHeader header;
    private int width;
    private int rowLength;

    /// <summary>
    /// Allocates fixed-capacity prediction storage for one operation. Sequential channels reuse one slot;
    /// interleaved palette channels retain distinct slots through their complete pixel traversal.
    /// </summary>
    /// <param name="configuration">The configuration that owns the numeric storage.</param>
    /// <param name="header">The initial weighted-prediction settings. Reset binds the settings for each new history.</param>
    /// <param name="maximumWidth">The widest channel this operation will process.</param>
    /// <param name="channelCount">The number of concurrently live channel histories, normally one.</param>
    public JxlModularState(Configuration configuration, JxlModularHeader header, int maximumWidth, int channelCount)
    {
        this.header = header;
        this.channelCount = channelCount;
        this.wordsPerSlot = checked(PredictionWords + (HistoryWordsPerPosition * (maximumWidth + RowMargin)));
        this.storageOwner = configuration.MemoryAllocator.Allocate<uint>(checked(this.wordsPerSlot * channelCount));
    }

    /// <summary>
    /// Binds the current header and clears every active history without allocating or changing capacity.
    /// </summary>
    /// <param name="header">The parsed or encoder-selected settings for the new channel or palette iteration.</param>
    /// <param name="width">The active channel width, within the maximum prepared by the operation owner.</param>
    /// <returns>The complete borrowed storage. Pass this span through prediction loops without acquiring Memory.Span again.</returns>
    /// <remarks>
    /// Call once before an independent sequential channel, or once before all interleaved palette channels.
    /// Borrowed storage must not outlive this owner. Reset invalidates prior prediction history, not the allocation.
    /// </remarks>
    public Span<uint> Reset(JxlModularHeader header, int width)
    {
        this.header = header;
        this.width = width;
        this.rowLength = width + RowMargin;
        Span<uint> storage = this.storageOwner.GetSpan()[..(this.wordsPerSlot * this.channelCount)];
        int activeWords = PredictionWords + (HistoryWordsPerPosition * this.rowLength);

        for (int channel = 0; channel < this.channelCount; channel++)
        {
            storage.Slice(channel * this.wordsPerSlot, activeWords).Clear();
        }

        return storage;
    }

    /// <inheritdoc />
    public void Dispose() => this.storageOwner.Dispose();

    /// <summary>
    /// Gets a table for approximating division by a number from
    /// 1 to 64. It is defined as follows.
    /// <code>
    /// for (int i = 0; i &lt; 64; i++)
    /// {
    ///     DivisionLookup[i] = (1u &lt;&lt; 24) / (i + 1);
    /// }
    /// </code>
    /// </summary>
    private static ReadOnlySpan<uint> DivisionLookup =>
    [
        16777216, 8388608, 5592405, 4194304, 3355443, 2796202, 2396745, 2097152,
        1864135,  1677721, 1525201, 1398101, 1290555, 1198372, 1118481, 1048576,
        986895,   932067,  883011,  838860,  798915,  762600,  729444,  699050,
        671088,   645277,  621378,  599186,  578524,  559240,  541200,  524288,
        508400,   493447,  479349,  466033,  453438,  441505,  430185,  419430,
        409200,   399457,  390167,  381300,  372827,  364722,  356962,  349525,
        342392,   335544,  328965,  322638,  316551,  310689,  305040,  299593,
        294337,   289262,  284359,  279620,  275036,  270600,  266305,  262144
    ];

    /// <summary>
    /// Adds extra bits to the prediction.
    /// </summary>
    /// <param name="x">The prediction</param>
    /// <returns>3 extra bits added to the prediction</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long AddBits(long x) => x << 3;

    /// <summary>
    /// Approximates the fixed-point inverse error weight without losing the unsigned error range.
    /// </summary>
    /// <param name="error">The accumulated UInt32 error. The calculation widens it before adding one.</param>
    /// <param name="maxWeight">The four-bit maximum weight from the parsed header.</param>
    /// <returns>The approximated weight, always at least four.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ErrorWeight(uint error, uint maxWeight)
    {
        int shift = Math.Max(0, (int)JxlMath.FloorLog2Nonzero((ulong)error + 1) - 5);
        return 4 + ((maxWeight * DivisionLookup[(int)(error >> shift)]) >> shift);
    }

    /// <summary>
    /// Combines four integer predictions with the native fixed-point normalization and rounding.
    /// </summary>
    /// <param name="predictions">Four predictions retaining three fractional bits.</param>
    /// <param name="weights">Four weights of at least four, replaced by their normalized values.</param>
    /// <returns>The weighted prediction, still retaining its fractional bits.</returns>
    private static long WeightedAverage(Span<long> predictions, Span<uint> weights)
    {
        // ErrorWeight returns at least four for each of the four predictors, so the sum is at least sixteen.
        // Four-bit header weights also bound the sum below UInt32 overflow before normalization.
        uint weightSum = (weights[0] + weights[1]) + (weights[2] + weights[3]);

        int logWeight = (int)JxlMath.FloorLog2Nonzero(weightSum);
        weightSum = 0;

        for (int i = 0; i < PredictorCount; i++)
        {
            weights[i] >>= logWeight - 4;
            weightSum += weights[i];
        }

        long sum = (weightSum >> 1) - 1;
        for (int i = 0; i < PredictorCount; i++)
        {
            // Dot product
            sum += predictions[i] * weights[i];
        }

        return (sum * DivisionLookup[(int)weightSum - 1]) >> 24;
    }

    /// <summary>
    /// Predicts one sample from an explicitly borrowed channel history. Pixels remain sequential; SIMD lanes represent the four predictors.
    /// </summary>
    /// <param name="computeProperties">Whether to store the signed neighbor error with the greatest magnitude.</param>
    /// <param name="x">The sample column.</param>
    /// <param name="y">The sample row.</param>
    /// <param name="storage">The span returned by Reset for this operation.</param>
    /// <param name="stateIndex">The live channel slot, zero for a sequential channel.</param>
    /// <param name="n">The north sample.</param>
    /// <param name="w">The west sample.</param>
    /// <param name="ne">The north-east sample.</param>
    /// <param name="nw">The north-west sample.</param>
    /// <param name="nn">The sample two rows above.</param>
    /// <param name="properties">The destination for requested context properties.</param>
    /// <param name="offset">The weighted property's position.</param>
    /// <returns>The weighted integer prediction after removing its three fractional bits.</returns>
    public long Predict(
        bool computeProperties,
        int x,
        int y,
        Span<uint> storage,
        int stateIndex,
        long n,
        long w,
        long ne,
        long nw,
        long nn,
        Span<int> properties,
        int offset)
    {
        Span<uint> slot = storage.Slice(stateIndex * this.wordsPerSlot, this.wordsPerSlot);
        Span<long> predictions = MemoryMarshal.Cast<uint, long>(slot[..PredictionWords]);
        Span<int> errors = MemoryMarshal.Cast<uint, int>(slot.Slice(PredictionWords, HistoryRows * this.rowLength));
        Span<uint> predictorErrors = slot.Slice(PredictionWords + (HistoryRows * this.rowLength), HistoryRows * PredictorCount * this.rowLength);
        int width = this.width;
        bool yIsOdd = (y & 1) != 0;

        int cur_row = yIsOdd ? 0 : this.rowLength;
        int prev_row = yIsOdd ? this.rowLength : 0;
        int posN = prev_row + x;
        int posNE = x < width - 1 ? posN + 1 : posN;
        int posNW = x > 0 ? posN - 1 : posN;

        InlineArray4<uint> weightStorage = default;
        Span<uint> weights = weightStorage;
        ReadOnlySpan<uint> headerWeights = this.header.GetW();

        // Four lanes hold one error from each predictor. One Vector128 covers the complete record; there is no vector remainder.
        // UInt32 addition deliberately wraps as in the bitstream algorithm. Wider vectors would cross into another sample's record.
        if (Vector128.IsHardwareAccelerated)
        {
            ref uint first = ref MemoryMarshal.GetReference(predictorErrors);
            Vector128<uint> north = Vector128.LoadUnsafe(ref first, (nuint)(PredictorCount * posN));
            Vector128<uint> northEast = Vector128.LoadUnsafe(ref first, (nuint)(PredictorCount * posNE));
            Vector128<uint> northWest = Vector128.LoadUnsafe(ref first, (nuint)(PredictorCount * posNW));
            (north + northEast + northWest).StoreUnsafe(ref MemoryMarshal.GetReference(weights));
        }
        else
        {
            for (int i = 0; i < PredictorCount; i++)
            {
                weights[i] = unchecked(
                    predictorErrors[(PredictorCount * posN) + i] +
                    predictorErrors[(PredictorCount * posNE) + i] +
                    predictorErrors[(PredictorCount * posNW) + i]);
            }
        }

        for (int i = 0; i < PredictorCount; i++)
        {
            // Widen before ErrorWeight adds one. The largest encoded UInt32 error must not become a signed value or wrap to zero.
            weights[i] = ErrorWeight(weights[i], headerWeights[i]);
        }

        n = AddBits(n);
        w = AddBits(w);
        ne = AddBits(ne);
        nw = AddBits(nw);
        nn = AddBits(nn);

        long teW = x == 0 ? 0 : errors[cur_row + x - 1];
        long teN = errors[posN];
        long teNW = errors[posNW];
        long sumWN = teN + teW;
        long teNE = errors[posNE];

        if (computeProperties)
        {
            long p = teW;

            if (Math.Abs(teN) > Math.Abs(p))
            {
                p = teN;
            }

            if (Math.Abs(teNW) > Math.Abs(p))
            {
                p = teNW;
            }

            if (Math.Abs(teNE) > Math.Abs(p))
            {
                p = teNE;
            }

            properties[offset++] = (int)p;
        }

        predictions[0] = w + ne - n;
        predictions[1] = n - (((sumWN + teNE) * this.header.P1C) >> 5);
        predictions[2] = w - (((sumWN + teNW) * this.header.P2C) >> 5);
        predictions[3] =
            n - (((teNW * this.header.P3Ca) + (teN * this.header.P3Cb) + (teNE * this.header.P3Cc) +
                ((nn - n) * this.header.P3Cd) + ((nw - w) * this.header.P3Ce)) >>
                5);

        predictions[PredictorCount] = WeightedAverage(predictions[..PredictorCount], weights);

        if (((teN ^ teW) | (teN ^ teNW)) > 0)
        {
            return (predictions[PredictorCount] + PredictionRound) >> 3;
        }

        long mx = Math.Max(w, Math.Max(ne, n));
        long mn = Math.Min(w, Math.Min(ne, n));
        predictions[PredictorCount] = Math.Max(mn, Math.Min(mx, predictions[PredictorCount]));
        return (predictions[PredictorCount] + PredictionRound) >> 3;
    }

    /// <summary>
    /// Updates one channel's signed and four-predictor errors after its actual sample is known.
    /// </summary>
    /// <param name="value">The reconstructed or source sample.</param>
    /// <param name="x">The sample column.</param>
    /// <param name="y">The sample row.</param>
    /// <param name="storage">The span returned by Reset for this operation.</param>
    /// <param name="stateIndex">The live channel slot, zero for a sequential channel.</param>
    public void UpdatePredictionErrors(long value, int x, int y, Span<uint> storage, int stateIndex)
    {
        Span<uint> slot = storage.Slice(stateIndex * this.wordsPerSlot, this.wordsPerSlot);
        ReadOnlySpan<long> predictions = MemoryMarshal.Cast<uint, long>(slot[..PredictionWords]);
        Span<int> errors = MemoryMarshal.Cast<uint, int>(slot.Slice(PredictionWords, HistoryRows * this.rowLength));
        Span<uint> predictorErrors = slot.Slice(PredictionWords + (HistoryRows * this.rowLength), HistoryRows * PredictorCount * this.rowLength);
        int currentRow = (y & 1) != 0 ? 0 : this.rowLength;
        int previousRow = (y & 1) != 0 ? this.rowLength : 0;
        value = AddBits(value);
        errors[currentRow + x] = unchecked((int)(predictions[PredictorCount] - value));
        InlineArray4<uint> errorStorage = default;
        Span<uint> currentErrors = errorStorage;

        for (int i = 0; i < PredictorCount; i++)
        {
            currentErrors[i] = unchecked((uint)((Math.Abs(predictions[i] - value) + PredictionRound) >> 3));
        }

        int current = PredictorCount * (currentRow + x);
        int previous = PredictorCount * (previousRow + x + 1);

        // Current and previous records are in different row halves. Vector addition and the scalar fallback both retain UInt32 wrapping.
        if (Vector128.IsHardwareAccelerated)
        {
            ref uint first = ref MemoryMarshal.GetReference(predictorErrors);
            Vector128<uint> values = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(currentErrors));
            Vector128<uint> previousValues = Vector128.LoadUnsafe(ref first, (nuint)previous);
            values.StoreUnsafe(ref first, (nuint)current);
            (previousValues + values).StoreUnsafe(ref first, (nuint)previous);
        }
        else
        {
            for (int i = 0; i < PredictorCount; i++)
            {
                predictorErrors[current + i] = currentErrors[i];
                predictorErrors[previous + i] = unchecked(predictorErrors[previous + i] + currentErrors[i]);
            }
        }
    }
}
