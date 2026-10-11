// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// State for context prediction
/// </summary>
internal sealed class JxlModularState
{
    /// <summary>
    /// Rounding constant for predictions.
    /// </summary>
    private const long PredictionRound = ((1 << 3) >> 1) - 1;

    private InlineArray4<long> prediction;
    private long pred;
    private readonly uint[][] predErrors = new uint[4][];
    private readonly int[] error;
    private readonly JxlModularHeader header;

    public JxlModularState(JxlModularHeader header, int width)
    {
        this.header = header;

        for (int i = 0; i < 4; i++)
        {
            this.predErrors[i] = new uint[(width + 2) * 2];
        }

        this.error = new int[(width + 2) * 2];
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ErrorWeight(int x, uint maxWeight)
    {
        int shift = Math.Max(0, JxlMath.FloorLog2Nonzero(x + 1) - 5); // Math.Max call ensures the value isn't negative
        return 4 + ((maxWeight * DivisionLookup[x >> shift]) >> shift);
    }

    public static long WeightedAverage(Span<long> p, Span<uint> w)
    {
        uint weightSum = (w[0] + w[1]) + (w[2] + w[3]);

        if (weightSum <= 15)
        {
            throw new InvalidOperationException("Weight sum is too low");
        }

        int logWeight = (int)JxlMath.FloorLog2Nonzero(weightSum);
        weightSum = 0;

        for (int i = 0; i < 4; i++)
        {
            w[i] >>= logWeight - 4;
            weightSum += w[i];
        }

        long sum = (weightSum >> 1) - 1;
        for (int i = 0; i < 4; i++)
        {
            // Dot product
            sum += p[i] * w[i];
        }

        return (sum * DivisionLookup[(int)weightSum - 1]) >> 24;
    }

    public long Predict(bool computeProperties, int x, int y, int width, long n, long w, long ne, long nw, long nn, Span<int> properties, int offset)
    {
        bool yIsOdd = (y & 1) != 0;

        int cur_row = yIsOdd ? 0 : (width + 2);
        int prev_row = yIsOdd ? (width + 2) : 0;
        int posN = prev_row + x;
        int posNE = x < width - 1 ? posN + 1 : posN;
        int posNW = x > 0 ? posN - 1 : posN;

        Span<uint> weights = [0, 0, 0, 0];
        Span<uint> headerW = this.header.GetW();

        for (int i = 0; i < 4; i++)
        {
            Span<uint> error = this.predErrors[i].AsSpan();
            weights[i] = error[posN] + error[posNE] + error[posNW];
            weights[i] = ErrorWeight((int)weights[i], headerW[i]);
        }

        n = AddBits(n);
        w = AddBits(w);
        ne = AddBits(ne);
        nw = AddBits(nw);
        nn = AddBits(nn);

        long teW = x == 0 ? 0 : this.error[cur_row + x - 1];
        long teN = this.error[posN];
        long teNW = this.error[posNW];
        long sumWN = teN + teW;
        long teNE = this.error[posNE];

        if (computeProperties)
        {
            long p = teW;
            long absP = Math.Abs(p);

            if (Math.Abs(teN) > absP)
            {
                p = teN;
            }

            if (Math.Abs(teNW) > absP)
            {
                p = teNW;
            }

            if (Math.Abs(teNE) > absP)
            {
                p = teNE;
            }

            properties[offset++] = (int)p;
        }

        this.prediction[0] = w + ne - n;
        this.prediction[1] = n - (((sumWN + teNE) * this.header.P1C) >> 5);
        this.prediction[2] = w - (((sumWN + teNW) * this.header.P2C) >> 5);
        this.prediction[3] =
            n - (((teNW * this.header.P3Ca) + (teN * this.header.P3Cb) + (teNE * this.header.P3Cc) +
                ((nn - n) * this.header.P3Cd) + ((nw - w) * this.header.P3Ce)) >>
                5);

        this.pred = WeightedAverage(this.prediction, weights);

        if (((teN ^ teW) | (teN ^ teNW)) > 0)
        {
            return (this.pred + PredictionRound) >> 3;
        }

        long mx = Math.Max(w, Math.Max(ne, n));
        long mn = Math.Min(w, Math.Min(ne, n));
        this.pred = Math.Max(mn, Math.Min(mx, this.pred));
        return (this.pred + PredictionRound) >> 3;
    }

    public void UpdatePredictionErrors(long value, int x, int y, int width)
    {
        bool yIsOdd = (y & 1) != 0;

        long curRow = yIsOdd ? 0 : (width + 2);
        long prevRow = yIsOdd ? (width + 2) : 0;
        value = AddBits(value);
        this.error[curRow + x] = (int)(this.pred - value);
        for (int i = 0; i < 4; i++)
        {
            long err = (Math.Abs(this.prediction[i] - value) + PredictionRound) >> 3;
            this.predErrors[i][curRow + x] = (uint)err;
            this.predErrors[i][prevRow + x + 1] += (uint)err;
        }
    }
}
