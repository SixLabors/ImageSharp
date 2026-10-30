// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Compression;
using SixLabors.ImageSharp.Formats.Jxl.IO;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;

/// <summary>
/// Decodes entropy symbols and hybrid integers, and owns the optional LZ77 history for one stream.
/// </summary>
internal sealed class JxlAnsSymbolReader : IDisposable
{
    private const uint WindowMask = JxlAnsReader.WindowSize - 1;

    private readonly JxlAnsEntry[] aliasTables;
    private readonly List<JxlHuffmanDecoder> huffmanDecoders;
    private readonly bool usePrefixCode;
    private uint state;
    private readonly List<JxlAnsHybridUIntConfiguration> configs;
    private readonly int logAlphaSize;
    private readonly int logEntrySize;
    private readonly int entrySizeMinus1;
    // The wire decoder counts UInt32 symbols. Keep these counters unsigned so high-bit values
    // cannot become negative lengths or positions before the ring index is masked.
    private uint numDecoded;
    private uint numToCopy;
    private uint copyPos;
    private readonly int lz77Context;
    private readonly int lz77MinLength;
    private readonly int lz77Threshold = 1 << 20;
    private readonly int numSpecialDistances;
    private readonly JxlAnsHybridUIntConfiguration lz77LengthUInt;

    /// <summary>
    /// Allocator-owned history. Absent when the entropy header disables LZ77.
    /// </summary>
    private readonly IMemoryOwner<int>? lz77Window;

    /// <summary>
    /// Cached distances for the 120 short-distance symbols. Existing inline storage avoids a separate array allocation.
    /// </summary>
    private InlineArray128<int> specialDistances;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlAnsSymbolReader"/> class from decoded entropy tables.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns the history buffer.</param>
    /// <param name="code">The decoded entropy tables and hybrid-integer configurations.</param>
    /// <param name="br">The reader positioned at the entropy stream's initial state.</param>
    /// <param name="distanceMultiplier">The row width used by short-distance symbols, or zero for linear distances.</param>
    private JxlAnsSymbolReader(Configuration configuration, JxlAnsCode code, JxlBitReader br, int distanceMultiplier)
    {
        this.aliasTables = code.AliasTables;
        this.huffmanDecoders = code.HuffmanData;
        this.usePrefixCode = code.UsePrefixCode;
        this.configs = code.UIntConfig;
        this.lz77LengthUInt = code.Lz77.LengthUintConfig;

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

        this.lz77Context = code.Lz77.NonserializedDistanceContext;
        this.lz77Threshold = (int)code.Lz77.MinimumSymbol;
        this.lz77MinLength = (int)code.Lz77.MinimumLength;
        this.numSpecialDistances = distanceMultiplier == 0 ? 0 : Lz77DistanceCodes.Count;

        for (int i = 0; i < this.numSpecialDistances; i++)
        {
            this.specialDistances[i] = Lz77DistanceCodes.GetDistance(i, distanceMultiplier);
        }

        // Allocate only after the initial state and distance setup succeed. A truncated header cannot strand an owner.
        // The ring is initialized by literals or the format's zero-distance rule before any history is read.
        this.lz77Window = configuration.MemoryAllocator.Allocate<int>(JxlAnsReader.WindowSize);
    }

    /// <summary>
    /// Gets whether the prefix stream can encode only literals and repetitions of the previous value.
    /// </summary>
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

    /// <summary>
    /// Gets whether this stream uses LZ77 references.
    /// </summary>
    public bool UsesLz77 => this.lz77Window is not null;

    /// <summary>
    /// Gets the borrowed LZ77 history, or an empty span when LZ77 is disabled.
    /// </summary>
    /// <remarks>Borrow this span before a decode loop and pass it through that loop. It remains valid until disposal.</remarks>
    public Span<int> Lz77Window => this.lz77Window is null ? Span<int>.Empty : this.lz77Window.GetSpan();

    /// <summary>
    /// Creates a symbol reader and allocates history only when its decoded entropy header requires it.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns the history buffer.</param>
    /// <param name="code">The decoded entropy tables.</param>
    /// <param name="br">The reader positioned at the stream's initial state.</param>
    /// <param name="distanceMultiplier">The row width used by short-distance symbols, or zero for linear distances.</param>
    /// <returns>The reader, which the caller must dispose.</returns>
    public static JxlAnsSymbolReader Create(Configuration configuration, JxlAnsCode code, JxlBitReader br, int distanceMultiplier)
        => new(configuration, code, br, distanceMultiplier);

    /// <summary>
    /// Checks that decoding consumed the entropy stream's terminal ANS state.
    /// </summary>
    /// <returns>Whether the stream ended at the format's signature state.</returns>
    public bool CheckAnsFinalState() => this.state == JxlAnsConstants.AnsSignature << 16;

    /// <summary>
    /// Reads one ANS symbol and consumes a 16-bit normalization field only when the state needs it.
    /// </summary>
    /// <param name="histogramIndex">The decoded histogram table to use.</param>
    /// <param name="br">The encoded normalization fields. The bit reader owns reservoir refill.</param>
    /// <returns>The decoded symbol.</returns>
    private int ReadSymbolAns(int histogramIndex, JxlBitReader br)
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

        return symbol.Value;
    }

    /// <summary>
    /// Reads one symbol using the prefix or ANS coding selected by the entropy header.
    /// </summary>
    /// <param name="histogramIndex">The decoded histogram table to use.</param>
    /// <param name="br">The encoded symbol fields. Reads and peeks refill their own reservoir.</param>
    /// <returns>The decoded symbol.</returns>
    public int ReadSymbol(int histogramIndex, JxlBitReader br)
    {
        if (this.usePrefixCode)
        {
            return this.huffmanDecoders[histogramIndex].ReadSymbol(br);
        }
        else
        {
            return this.ReadSymbolAns(histogramIndex, br);
        }
    }

    /// <summary>
    /// Expands a hybrid-integer token and its payload without changing entropy or history state.
    /// </summary>
    /// <param name="config">The split exponent and token bit allocation from the entropy header.</param>
    /// <param name="token">The entropy-decoded token.</param>
    /// <param name="br">The reader positioned at the token's remaining low-order bits.</param>
    /// <returns>The UInt32 value, represented by the same bit pattern in an Int32.</returns>
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

    /// <summary>
    /// Reads a literal or repeated-value run from a prefix stream restricted to the RLE-only distance code.
    /// </summary>
    /// <param name="context">The histogram index.</param>
    /// <param name="br">The encoded symbols.</param>
    /// <param name="value">The previous value, replaced only by a literal.</param>
    /// <param name="run">The number of additional repetitions after the current output value.</param>
    public void ReadHybridUIntClusteredHuffRleOnly(int context, JxlBitReader br, ref uint value, ref uint run)
    {
        DebugGuard.IsTrue(this.IsHuffRleOnly, "This method is meant for huffman RLE only");
        int token = this.huffmanDecoders[context].ReadSymbol(br);
        if (token >= this.lz77Threshold)
        {
            run = (uint)(
                ReadHybridUIntConfiguration(this.lz77LengthUInt, token - this.lz77Threshold, br) +
                this.lz77MinLength - 1);

            // A run token repeats the previous value. It does not also encode a new literal.
            return;
        }

        value = (uint)ReadHybridUIntConfiguration(this.configs[context], token, br);
    }

    /// <summary>
    /// Reads one integer from a selected histogram and updates the borrowed history when LZ77 is enabled.
    /// </summary>
    /// <param name="usesLz77">The LZ77 mode established by the stream header.</param>
    /// <param name="context">The histogram index.</param>
    /// <param name="br">The encoded symbols.</param>
    /// <param name="window">The history span borrowed from this reader before the decode loop.</param>
    /// <returns>The decoded UInt32 bit pattern, stored in an Int32.</returns>
    public int ReadHybridUIntClustered(bool usesLz77, int context, JxlBitReader br, Span<int> window)
    {
        int ret;

        if (usesLz77)
        {
            if (this.numToCopy > 0)
            {
                ret = window[(int)((this.copyPos++) & WindowMask)];
                this.numToCopy--;
                window[(int)((this.numDecoded++) & WindowMask)] = ret;
                return ret;
            }
        }

        int token = this.ReadSymbol(context, br);

        if (usesLz77)
        {
            if (token >= this.lz77Threshold)
            {
                this.numToCopy = (uint)ReadHybridUIntConfiguration(this.lz77LengthUInt, token - this.lz77Threshold, br) + (uint)this.lz77MinLength;
                int distanceToken = this.ReadSymbol(this.lz77Context, br);

                // Hybrid integers retain all 32 bits. Widen before adding one so the largest distance
                // cannot wrap to zero. The format then bounds it by the available history and ring size.
                ulong distance = (uint)ReadHybridUIntConfiguration(this.configs[this.lz77Context], distanceToken, br);
                if (distance < (uint)this.numSpecialDistances)
                {
                    distance = (uint)this.specialDistances[(int)distance];
                }
                else
                {
                    distance = distance + 1 - (uint)this.numSpecialDistances;
                }

                if (distance > this.numDecoded)
                {
                    distance = this.numDecoded;
                }

                if (distance > JxlAnsReader.WindowSize)
                {
                    distance = JxlAnsReader.WindowSize;
                }

                this.copyPos = this.numDecoded - (uint)distance;

                if (distance == 0)
                {
                    int toFill = (int)Math.Min(this.numToCopy, JxlAnsReader.WindowSize);
                    window[..toFill].Clear();
                }

                // Adding the minimum length can wrap a malformed UInt32 length. Reject that run
                // instead of continuing to emit history values, as required by the native decoder.
                if (this.numToCopy < this.lz77MinLength)
                {
                    this.numToCopy = 0;
                    throw new InvalidOperationException("ANS decoder error");
                }

                ret = window[(int)((this.copyPos++) & WindowMask)];
                this.numToCopy--;
                window[(int)((this.numDecoded++) & WindowMask)] = ret;
                return ret;
            }
        }

        ret = ReadHybridUIntConfiguration(this.configs[context], token, br);

        if (usesLz77)
        {
            window[(int)((this.numDecoded++) & WindowMask)] = ret;
        }

        return ret;
    }

    /// <summary>
    /// Reads one mapped integer using the caller's invariant LZ77 mode.
    /// </summary>
    /// <param name="usesLz77">The mode established by the stream header.</param>
    /// <param name="context">The unclustered entropy context.</param>
    /// <param name="br">The encoded symbols.</param>
    /// <param name="contextMap">The map from entropy contexts to histograms.</param>
    /// <param name="window">The history borrowed before the decode loop.</param>
    /// <returns>The decoded UInt32 bit pattern, stored in an Int32.</returns>
    public int ReadHybridUInt(bool usesLz77, int context, JxlBitReader br, ReadOnlySpan<byte> contextMap, Span<int> window)
        => this.ReadHybridUIntClustered(usesLz77, contextMap[context], br, window);

    /// <summary>
    /// Reads one mapped integer using this reader's LZ77 mode.
    /// </summary>
    /// <param name="context">The unclustered entropy context.</param>
    /// <param name="br">The encoded symbols.</param>
    /// <param name="contextMap">The map from entropy contexts to histograms.</param>
    /// <param name="window">The history borrowed before the decode loop.</param>
    /// <returns>The decoded UInt32 bit pattern, stored in an Int32.</returns>
    public int ReadHybridUInt(int context, JxlBitReader br, ReadOnlySpan<byte> contextMap, Span<int> window)
        => this.ReadHybridUIntClustered(this.UsesLz77, contextMap[context], br, window);

    /// <summary>
    /// Advances a constant-symbol stream without reading a symbol for each output value.
    /// </summary>
    /// <param name="context">The histogram index.</param>
    /// <param name="count">The number of output values.</param>
    /// <param name="value">The constant value, or zero when this optimization does not apply.</param>
    /// <param name="window">The history borrowed before the decode loop.</param>
    /// <returns>Whether the histogram encodes a single literal value.</returns>
    public bool IsSingleValueAndAdvance(int context, int count, out uint value, Span<int> window)
    {
        value = 0;

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

        value = (uint)symbol.Value;

        if (this.lz77Window is not null)
        {
            for (int i = 0; i < count; i++)
            {
                window[(int)((this.numDecoded++) & WindowMask)] = symbol.Value;
            }
        }

        return true;
    }

    /// <summary>
    /// Releases the allocator-owned LZ77 history.
    /// </summary>
    public void Dispose() => this.lz77Window?.Dispose();
}
