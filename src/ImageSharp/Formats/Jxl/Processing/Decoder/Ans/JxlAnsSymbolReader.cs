// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Compression;
using SixLabors.ImageSharp.Formats.Jxl.IO;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;

internal sealed class JxlAnsSymbolReader : IDisposable
{
    private const int MaxCheckpointInterval = 512;

    private const int WindowMask = JxlAnsReader.WindowSize - 1;

    private readonly JxlAnsEntry[] aliasTables = [];
    private readonly List<JxlHuffmanDecoder> huffmanDecoders = [];
    private readonly bool usePrefixCode;
    private uint state = JxlAnsConstants.AnsTableSize << 16;
    private readonly List<JxlAnsHybridUIntConfiguration> configs = [];
    private readonly int logAlphaSize;
    private readonly int logEntrySize;
    private readonly int entrySizeMinus1;
    private int numDecoded;
    private int numToCopy;
    private int copyPos;
    private readonly int lz77Context;
    private readonly int lz77MinLength;
    private readonly int lz77Threshold = 1 << 20;
    private readonly int numSpecialDistances;
    private readonly JxlAnsHybridUIntConfiguration lz77LengthUInt = new();
    private readonly int[]? lz77Window;
    private readonly int[] specialDistances = new int[Lz77DistanceCodes.Count];

    private JxlAnsSymbolReader(JxlAnsCode code, JxlBitReader br, int distanceMultiplier, int[]? lz77WindowStorage)
    {
        this.aliasTables = code.AliasTables!;
        this.huffmanDecoders = code.HuffmanData;
        this.usePrefixCode = code.UsePrefixCode;
        this.configs = code.UIntConfig;

        if (!this.usePrefixCode)
        {
            this.state = br.ReadBits32(32u);
            this.logAlphaSize = code.LogAlphaSize;
            this.logEntrySize = JxlAnsConstants.AnsLogTableSize - code.LogAlphaSize;
            this.entrySizeMinus1 = (1 << this.logEntrySize) - 1;
        }
        else
        {
            this.state = JxlAnsConstants.AnsSignature << 16;
        }

        if (!code.Lz77.Enabled)
        {
            return;
        }

        this.lz77Window = lz77WindowStorage;
        this.lz77Context = code.Lz77.NonserializedDistanceContext;
        this.lz77LengthUInt = code.Lz77.LengthUintConfig;
        this.lz77Threshold = (int)code.Lz77.MinimumSymbol;
        this.lz77MinLength = (int)code.Lz77.MinimumLength;
        this.numSpecialDistances = distanceMultiplier == 0 ? 0 : Lz77DistanceCodes.Count;

        for (int i = 0; i < this.numSpecialDistances; i++)
        {
            this.specialDistances[i] = Lz77DistanceCodes.GetDistance(i, distanceMultiplier);
        }
    }

    public bool IsHuffRleOnly
    {
        get
        {
            if (this.lz77Window is null || !this.usePrefixCode)
            {
                return false;
            }

            JxlHuffmanDecoder huffDecoder = this.huffmanDecoders[this.lz77Context];

            for (int i = 0; i < JxlHuffmanDecoder.HuffmanTableBits; i++)
            {
                ref JxlHuffmanCode code = ref huffDecoder.Table[i];

                if (code.Bits > 0 || code.Value != 1)
                {
                    return false;
                }
            }

            if (this.configs[this.lz77Context].SplitToken > 1)
            {
                return false;
            }

            return true;
        }
    }

    public bool UsesLz77 => this.lz77Window is not null;

    public static JxlAnsSymbolReader Create(JxlAnsCode code, JxlBitReader br, int distanceMultiplier)
    {
        int[]? lz77 = code.Lz77.Enabled
            ? ArrayPool<int>.Shared.Rent(JxlAnsReader.WindowSize)
            : null;

        return new JxlAnsSymbolReader(code, br, distanceMultiplier, lz77);
    }

    /// <summary>
    /// Checks that decoding consumed the entropy stream's terminal ANS state.
    /// </summary>
    /// <returns>Whether the stream ended at the format's signature state.</returns>
    public bool CheckAnsFinalState() => this.state == JxlAnsConstants.AnsSignature << 16;

    public int ReadSymbolAnsWithoutRefill(int histogramIndex, JxlBitReader br)
    {
        uint res = this.state & (JxlAnsConstants.AnsTableSize - 1);

        // Histograms occupy consecutive, equal-sized tables. The histogram selects the starting offset,
        // not the prefix length. Histogram zero therefore still has one complete table.
        Span<JxlAnsEntry> table = this.aliasTables.AsSpan(histogramIndex << this.logAlphaSize, 1 << this.logAlphaSize);
        JxlAnsSymbol symbol = JxlAnsHelper.Lookup(table, (int)res, this.logEntrySize, this.entrySizeMinus1);

        this.state = (uint)((symbol.Frequency * (this.state >> JxlAnsConstants.AnsLogTableSize)) + symbol.Offset);

        bool shouldNormalize = this.state < (1 << 16);
        if (shouldNormalize)
        {
            // New state to normalize is 16 bits
            uint newState = (this.state << 16) | br.ReadBits32(16u);
            this.state = newState;
        }

        uint nextRes = this.state & (JxlAnsConstants.AnsTableSize - 1);
        JxlAnsHelper.Prefetch(table, nextRes, this.logEntrySize);

        return symbol.Value;
    }

    public int ReadSymbolHuffmanWithoutRefill(int histogramIndex, JxlBitReader br)
        => this.huffmanDecoders[histogramIndex].ReadSymbol(br);

    public int ReadSymbolWithoutRefill(int histogramIndex, JxlBitReader br)
    {
        if (this.usePrefixCode)
        {
            return this.ReadSymbolHuffmanWithoutRefill(histogramIndex, br);
        }
        else
        {
            return this.ReadSymbolAnsWithoutRefill(histogramIndex, br);
        }
    }

    public int ReadSymbol(int histogramIndex, JxlBitReader br)
    {
        br.Refill();
        return this.ReadSymbolWithoutRefill(histogramIndex, br);
    }

    public static int ReadHybridUIntConfiguration(JxlAnsHybridUIntConfiguration config, int token, JxlBitReader br)
    {
        uint splitToken = config.SplitToken;
        uint msbInToken = config.MsbInToken;
        uint lsbInToken = config.LsbInToken;
        uint splitExponent = config.SplitExponent;

        if (token < splitToken)
        {
            return token;
        }

        uint numBits = splitExponent - (msbInToken + lsbInToken) +
                     (uint)((token - splitToken) >> (int)(msbInToken + lsbInToken));
        // Keep malformed shift counts within the UInt32 primitive's width, as in the reference decoder.
        numBits &= 31u;

        int low = token & ((1 << (int)lsbInToken) - 1);
        token >>= (int)lsbInToken;

        uint bits = br.ReadBits32(numBits);

        int msbInToken1Lsh = 1 << (int)msbInToken;
        uint result = (uint)(((((msbInToken1Lsh | (token & (msbInToken1Lsh - 1)))
                    << (int)numBits) | (int)bits)
                    << (int)lsbInToken) | low);

        return (int)result;
    }

    public void ReadHybridUIntClusteredHuffRleOnly(int context, JxlBitReader br, ref int value, ref int run)
    {
        DebugGuard.IsTrue(this.IsHuffRleOnly, "This method is meant for huffman RLE only");
        br.Refill();

        int token = this.ReadSymbolHuffmanWithoutRefill(context, br);
        if (token >= this.lz77Threshold)
        {
            run =
                ReadHybridUIntConfiguration(this.lz77LengthUInt, token - this.lz77Threshold, br) +
                this.lz77MinLength - 1;

            // A run token repeats the previous value. It does not also encode a new literal.
            return;
        }

        value = ReadHybridUIntConfiguration(this.configs[context], token, br);
    }

    public int ReadHybridUIntClustered(bool usesLz77, int context, JxlBitReader br)
    {
        int ret;

        if (usesLz77)
        {
            if (this.numToCopy > 0)
            {
                ret = this.lz77Window![(this.copyPos++) & WindowMask];
                this.numToCopy--;
                this.lz77Window[(this.numDecoded++) & WindowMask] = ret;
                return ret;
            }
        }

        br.Refill();
        int token = this.ReadSymbolWithoutRefill(context, br);

        if (usesLz77)
        {
            if (token >= this.lz77Threshold)
            {
                this.numToCopy = ReadHybridUIntConfiguration(this.lz77LengthUInt, token - this.lz77Threshold, br) + this.lz77MinLength;
                br.Refill();

                int d_token = this.ReadSymbolWithoutRefill(this.lz77Context, br);
                int distance = ReadHybridUIntConfiguration(this.configs[this.lz77Context], d_token, br);

                if (distance < this.numSpecialDistances)
                {
                    distance = this.specialDistances[distance];
                }
                else
                {
                    distance = distance + 1 - this.numSpecialDistances;
                }

                if (distance > this.numDecoded)
                {
                    distance = this.numDecoded;
                }

                if (distance > JxlAnsReader.WindowSize)
                {
                    distance = JxlAnsReader.WindowSize;
                }

                this.copyPos = this.numDecoded - distance;

                if (distance == 0)
                {
                    if (this.lz77Window is null)
                    {
                        throw new InvalidOperationException("Missing LZ77 window");
                    }

                    int toFill = Math.Min(this.numToCopy, JxlAnsReader.WindowSize);
                    this.lz77Window.AsSpan(0, toFill).Clear();
                }

                if (this.numToCopy < this.lz77MinLength)
                {
                    this.numToCopy = 0;
                    throw new InvalidOperationException("ANS decoder error");
                }

                ret = this.lz77Window![(this.copyPos++) & WindowMask];
                this.numToCopy--;
                this.lz77Window[(this.numDecoded++) & WindowMask] = ret;
                return ret;
            }
        }

        ret = ReadHybridUIntConfiguration(this.configs[context], token, br);

        if (usesLz77 && this.lz77Window is not null)
        {
            this.lz77Window[(this.numDecoded++) & WindowMask] = ret;
        }

        return ret;
    }

    public int ReadHybridUIntInlined(bool usesLz77, int context, JxlBitReader br, Span<byte> contextMap)
        => this.ReadHybridUIntClustered(usesLz77, contextMap[context], br);

    public int ReadHybridUIntInlined(bool usesLz77, int context, JxlBitReader br, List<byte> contextMap)
        => this.ReadHybridUIntClustered(usesLz77, contextMap[context], br);

    public int ReadHybridUInt(int context, JxlBitReader br, Span<byte> contextMap)
        => this.ReadHybridUIntClustered(this.UsesLz77, contextMap[context], br);

    public int ReadHybridUInt(int context, JxlBitReader br, List<byte> contextMap)
        => this.ReadHybridUIntClustered(this.UsesLz77, contextMap[context], br);

    public bool IsSingleValueAndAdvance(int context, ref int value, int count)
    {
        if (this.usePrefixCode || this.numToCopy != 0)
        {
            return false;
        }

        uint res = this.state & (JxlAnsConstants.AnsTableSize - 1);
        Span<JxlAnsEntry> table = this.aliasTables.AsSpan()[(context << this.logAlphaSize)..];
        JxlAnsSymbol symbol = JxlAnsHelper.Lookup(table, (int)res, this.logEntrySize, this.entrySizeMinus1);

        if (symbol.Frequency != JxlAnsConstants.AnsTableSize ||
            this.configs[context].SplitToken <= symbol.Value ||
            symbol.Value >= this.lz77Threshold)
        {
            return false;
        }

        value = symbol.Value;

        if (this.lz77Window is not null)
        {
            for (int i = 0; i < count; i++)
            {
                this.lz77Window[(this.numDecoded++) & WindowMask] = symbol.Value;
            }
        }

        return true;
    }

    public void Save(Checkpoint checkpoint)
    {
        checkpoint.State = this.state;
        checkpoint.NumDecoded = this.numDecoded;
        checkpoint.NumToCopy = this.numToCopy;
        checkpoint.CopyPos = this.copyPos;

        if (this.lz77Window is not null)
        {
            int windowStart = this.numDecoded & WindowMask;
            int windowEnd = (this.numDecoded + MaxCheckpointInterval) & WindowMask;

            if (windowEnd > windowStart)
            {
                this.lz77Window.AsSpan(windowStart, windowEnd - windowStart)
                    .CopyTo(this.lz77Window);
            }
            else
            {
                this.lz77Window.AsSpan(windowStart, JxlAnsReader.WindowSize - windowStart)
                    .CopyTo(this.lz77Window);

                this.lz77Window.AsSpan(0, windowEnd)
                    .CopyTo(this.lz77Window.AsSpan(JxlAnsReader.WindowSize - windowStart, windowEnd));
            }
        }
    }

    private void Restore(Checkpoint checkpoint)
    {
        this.state = checkpoint.State;

        if (this.numDecoded > checkpoint.NumDecoded + MaxCheckpointInterval)
        {
            throw new InvalidOperationException("numDecoded is too large");
        }

        this.numDecoded = checkpoint.NumDecoded;
        this.numToCopy = checkpoint.NumToCopy;
        this.copyPos = checkpoint.CopyPos;

        if (this.lz77Window is not null)
        {
            int windowStart = this.numDecoded & WindowMask;
            int windowEnd = (this.numDecoded + MaxCheckpointInterval) & WindowMask;

            if (windowEnd > windowStart)
            {
                checkpoint.Lz77Window.AsSpan(0, windowEnd - windowStart)
                    .CopyTo(this.lz77Window.AsSpan(windowStart));
            }
            else
            {
                int firstLength = JxlAnsReader.WindowSize - windowStart;

                checkpoint.Lz77Window.AsSpan(0, firstLength)
                    .CopyTo(this.lz77Window.AsSpan(windowStart));

                checkpoint.Lz77Window.AsSpan(firstLength, windowEnd)
                    .CopyTo(this.lz77Window);
            }
        }
    }

    public void Dispose()
    {
        if (this.lz77Window is not null)
        {
            ArrayPool<int>.Shared.Return(this.lz77Window);
        }
    }

    // Use class because the Lz77Window property uses 2KB memory
    public sealed class Checkpoint
    {
        public uint State { get; set; }

        public int NumToCopy { get; set; }

        public int CopyPos { get; set; }

        public int NumDecoded { get; set; }

        public int[] Lz77Window { get; set; } = new int[MaxCheckpointInterval];
    }
}
