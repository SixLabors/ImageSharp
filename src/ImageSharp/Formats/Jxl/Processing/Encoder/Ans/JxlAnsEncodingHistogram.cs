// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

internal class JxlAnsEncodingHistogram
{
    private const int MaxNumSymbolsForSmallCode = 2;
    private int method;
    private int omitPos;
    private int alphabetSize;
    private int numSymbols;
    private InlineArray2<int> symbols;
    private static readonly uint[] lg2 = new uint[JxlAnsConstants.AnsTableSize + 1];

    private static ReadOnlySpan<byte> BitWidthLengths => [5, 4, 4, 4, 4, 4, 3, 3, 3, 3, 3, 6, 7, 7];

    private static ReadOnlySpan<byte> BitWidthSymbols => [17, 11, 15, 3, 9, 7, 4, 2, 5, 6, 0, 33, 1, 65];

    public List<int> Counts { get; } = [];

    public float Cost { get; }

    private bool RebalanceHistogram(JxlHistogram histo)
    {
        int tableSize = JxlAnsConstants.AnsTableSize;
        int shift = this.method - 1;

        Span<long> balanceInc = stackalloc long[JxlAnsConstants.AnsLogTableSize - 1];
        Span<long> balanceDec = stackalloc long[JxlAnsConstants.AnsLogTableSize - 1];

        var ac = this.allowedCounts.Array[shift];
        var ai = this.allowedCounts.Index[shift];

        long DeltaEntropyInc(EntropyDelta a)
        {
            var allowed = ac[a.CountIndex];
            return ((long)a.Frequency * allowed.delta_lg2)
                - balanceInc[allowed.step_log];
        }

        long DeltaEntropyDec(EntropyDelta a)
        {
            var allowed = ac[a.CountIndex + 1];
            return ((long)a.Frequency * allowed.delta_lg2)
                - balanceDec[allowed.step_log];
        }

        bool IncLess(EntropyDelta a, EntropyDelta b) => (DeltaEntropyInc(a) >> ac[a.CountIndex].step_log)
                < (DeltaEntropyInc(b) >> ac[b.CountIndex].step_log);

        bool DecLess(EntropyDelta a, EntropyDelta b) => (DeltaEntropyDec(a) >> ac[a.CountIndex + 1].step_log)
                < (DeltaEntropyDec(b) >> ac[b.CountIndex + 1].step_log);

        List<EntropyDelta> bins = new(256);
        double norm = (double)tableSize / histo.TotalCount;

        int remainderPos = 0;
        long maxFreq = 0;
        int rest = tableSize;

        for (int n = 0; n < this.alphabetSize; n++)
        {
            int freq = histo.Counts[n];

            if (freq > maxFreq)
            {
                remainderPos = n;
                maxFreq = freq;
            }

            double target = freq * norm;

            int count = (int)Math.Max(
                Math.Round(target, MidpointRounding.AwayFromZero),
                freq > 0 ? 1 : 0);

            count = Math.Min(count, tableSize - 1);

            uint stepLog = SmallestIncrementLog(count, shift);
            int inc = 1 << (int)stepLog;
            count &= ~(inc - 1);

            this.Counts[n] = count;
            rest -= count;

            if (target > 1.0)
            {
                bins.Add(new EntropyDelta(freq, ai[count], n));
            }
        }

        // Exclude the balancing bin from the adjustable bins.
        int balancingIndex = bins.FindIndex(a => a.BinIndex == remainderPos);
        if (balancingIndex >= 0)
        {
            bins.RemoveAt(balancingIndex);
        }

        // Rest is now the balancing bin's frequency.
        rest += this.Counts[remainderPos];

        if (bins.Count > 0)
        {
            uint maxLog = ac[1].step_log;

            while (true)
            {
                // Update penalties for the balancing bin.
                for (int log = 0; log <= maxLog; log++)
                {
                    int delta = 1 << log;

                    if (rest >= tableSize)
                    {
                        // Permit increases; forbid decreases.
                        balanceInc[log] = 0;
                        balanceDec[log] = 0;
                    }
                    else if (rest > 1)
                    {
                        balanceInc[log] = rest > delta
                            ? maxFreq * (lg2[rest] - lg2[rest - delta])
                            : long.MaxValue;

                        balanceDec[log] = rest + delta < tableSize
                            ? maxFreq * (lg2[rest + delta] - lg2[rest])
                            : 0;
                    }
                    else
                    {
                        // Forbid increases; permit decreases.
                        balanceInc[log] = long.MaxValue;
                        balanceDec[log] = long.MaxValue;
                    }
                }

                // Find the bin with the greatest increase score.
                int bestIncIndex = 0;

                for (int i = 1; i < bins.Count; i++)
                {
                    if (IncLess(bins[bestIncIndex], bins[i]))
                    {
                        bestIncIndex = i;
                    }
                }

                EntropyDelta bestBinInc = bins[bestIncIndex];

                if (DeltaEntropyInc(bestBinInc) > 0)
                {
                    // Grow the selected bin.
                    rest -= (int)(1 << (int)ac[bestBinInc.CountIndex].step_log);

                    bestBinInc.CountIndex--;
                }
                else
                {
                    // Find the bin with the smallest decrease score.
                    int bestDecIndex = 0;

                    for (int i = 1; i < bins.Count; i++)
                    {
                        if (DecLess(bins[i], bins[bestDecIndex]))
                        {
                            bestDecIndex = i;
                        }
                    }

                    EntropyDelta bestBinDec = bins[bestDecIndex];

                    // Stop when no reverse step improves entropy.
                    if (DeltaEntropyDec(bestBinDec) >= 0)
                    {
                        break;
                    }

                    // Decrease the selected bin.
                    bestBinDec.CountIndex++;

                    rest += (int)(1 << (int)ac[bestBinDec.CountIndex].step_log);
                }
            }

            // Write the adjusted frequencies.
            foreach (EntropyDelta a in bins)
            {
                this.Counts[a.BinIndex] = ac[a.CountIndex].count;
            }

            for (int n = 0; n < remainderPos; n++)
            {
                if (this.Counts[n] >= 2048)
                {
                    this.Counts[remainderPos] = this.Counts[n];
                    remainderPos = n;
                    break;
                }
            }
        }

        // Set the balancing bin.
        this.Counts[remainderPos] = rest;
        this.omitPos = remainderPos;

        return this.Counts[remainderPos] > 0;
    }

    private static float EstimateDataBitsFlat(JxlHistogram histo)
    {
        int length = histo.GetAlphabetSize();
        long flatBits = (long)lg2[length] * JxlAnsConstants.AnsLogTableSize;
        return MathF.ScaleB(histo.TotalCount * flatBits, -31);
    }

    private float EstimateDataBits(JxlHistogram histo)
    {
        long sum = 0;

        for (int i = 0; i < this.alphabetSize; i++)
        {
            sum += histo.Counts[i] * lg2[this.Counts[i]];
        }

        return (histo.TotalCount - MathF.ScaleB(sum, -31)) * JxlAnsConstants.AnsLogTableSize;
    }

    public bool Encode(JxlBitWriter writer)
    {
        if (this.alphabetSize > JxlAnsConstants.AnsMaxAlphabetSize)
        {
            throw new InvalidOperationException("Alphabet is out of range");
        }

        // Flat histogram
        if (this.method == 0)
        {
            // Mark non-small tree
            writer.Write(1, 0);

            // Mark uniform histogram
            writer.Write(1, 1);

            if (this.alphabetSize <= 0)
            {
                throw new InvalidOperationException("Alphabet size cannot be 0 or negative");
            }

            // Encode alphabet size
            JxlAnsEncoder.StoreVarLenUInt8(this.alphabetSize - 1, writer);
            return true;
        }

        // Small tree
        if (this.numSymbols <= MaxNumSymbolsForSmallCode)
        {
            // Small tree marker to encode 1-2 symbols.
            writer.Write(1, 1);

            if (this.numSymbols == 0)
            {
                writer.Write(1, 0);
                JxlAnsEncoder.StoreVarLenUInt8(0, writer);
            }
            else
            {
                writer.Write(1, this.numSymbols - 1);

                for (int i = 0; i < this.numSymbols; ++i)
                {
                    JxlAnsEncoder.StoreVarLenUInt8(this.symbols[i], writer);
                }
            }

            if (this.numSymbols == 2)
            {
                writer.Write(JxlAnsConstants.AnsLogTableSize, this.Counts[this.symbols[0]]);
            }

            return true;
        }

        // General tree
        // Mark non-small tree
        writer.Write(1, 0);

        // Mark non-flat histogram
        writer.Write(1, 0);

        int upperBoundLog = JxlMath.FloorLog2Nonzero(JxlAnsConstants.AnsLogTableSize + 1);
        int log = JxlMath.FloorLog2Nonzero(this.method);

        writer.Write(log, (1 << log) - 1);

        if (log != upperBoundLog)
        {
            writer.Write(1, 0);
        }

        writer.Write(log, ((1 << log) - 1) & this.method);

        JxlAnsEncoder.StoreVarLenUInt8(this.alphabetSize - 3, writer);

        Span<byte> same = stackalloc byte[JxlAnsConstants.AnsMaxAlphabetSize];
        same.Clear();
        int last = 0;

        for (int i = 1; i <= this.alphabetSize; i++)
        {
            if (i == this.alphabetSize || i == this.omitPos || i == this.omitPos + 1 || this.Counts[i] != this.Counts[last])
            {
                same[last] = (byte)(i - last);
                last = i;
            }
        }

        Span<byte> bitWidth = stackalloc byte[JxlAnsConstants.AnsMaxAlphabetSize];
        bitWidth.Clear();
        int omitWidth = 10;

        for (int i = 0; i < this.alphabetSize; ++i)
        {
            if (i != this.omitPos && this.Counts[i] > 0)
            {
                bitWidth[i] = (byte)(JxlMath.FloorLog2Nonzero(this.Counts[i]) + 1);
                omitWidth = Math.Max(omitWidth, bitWidth[i] + (i < this.omitPos ? 1 : 0));
            }
        }

        bitWidth[this.omitPos] = (byte)omitWidth;

        // Minimum repeats
        const byte minReps = 5;
        const int rep = JxlAnsConstants.AnsLogTableSize + 1;

        for (int i = 0; i < this.alphabetSize; ++i)
        {
            writer.Write(BitWidthLengths[bitWidth[i]], BitWidthSymbols[bitWidth[i]]);

            if (same[i] >= minReps)
            {
                writer.Write(BitWidthLengths[rep], BitWidthSymbols[rep]);
                JxlAnsEncoder.StoreVarLenUInt8(same[i] - minReps, writer);

                i += same[i] - 1;
            }
        }

        int shift = this.method - 1;

        if (shift != 0)
        {
            for (int i = 0; i < this.alphabetSize; ++i)
            {
                if (bitWidth[i] > 1 && i != this.omitPos)
                {
                    int bitcount = JxlAnsHelper.GetPopulationCountPrecision(bitWidth[i] - 1, shift);
                    int dropBits = bitWidth[i] - 1 - bitcount;

                    if ((this.Counts[i] & ((1 << dropBits) - 1)) != 0)
                    {
                        throw new InvalidOperationException("Incorrect counts");
                    }

                    writer.Write(bitcount, (this.Counts[i] >> dropBits) - (1 << bitcount));
                }

                if (same[i] >= minReps)
                {
                    // skip symbols encoded by RLE
                    i += same[i] - 1;
                }
            }
        }

        return true;
    }

    private static int SmallestIncrementLog(int count, int shift)
    {
        if (count == 0)
        {
            return 0;
        }

        int bits = JxlMath.FloorLog2Nonzero(count);
        int dropBits = bits - JxlAnsHelper.GetPopulationCountPrecision(bits, shift);

        return dropBits;
    }

    private struct EntropyDelta
    {
        public int Frequency;
        public int CountIndex;
        public int BinIndex;

        public EntropyDelta(int freq, int countInd, int binInd)
        {
            this.Frequency = freq;
            this.CountIndex = countInd;
            this.BinIndex = binInd;
        }
    }
}
