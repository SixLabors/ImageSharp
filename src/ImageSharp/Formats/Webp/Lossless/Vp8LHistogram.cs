// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

internal abstract unsafe class Vp8LHistogram
{
    private const uint NonTrivialSym = 0xffffffff;
    private readonly uint* red;
    private readonly uint* blue;
    private readonly uint* alpha;
    private readonly uint* distance;
    private readonly uint* literal;
    private readonly uint* isUsed;

    private const int RedSize = WebpConstants.NumLiteralCodes;
    private const int BlueSize = WebpConstants.NumLiteralCodes;
    private const int AlphaSize = WebpConstants.NumLiteralCodes;
    private const int DistanceSize = WebpConstants.NumDistanceCodes;
    public const int LiteralSize = WebpConstants.NumLiteralCodes + WebpConstants.NumLengthCodes + (1 << WebpConstants.MaxColorCacheBits) + 1;
    private const int UsedSize = 5; // 5 for literal, red, blue, alpha, distance
    public const int BufferSize = RedSize + BlueSize + AlphaSize + DistanceSize + LiteralSize + UsedSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8LHistogram"/> class.
    /// </summary>
    /// <param name="basePointer">The base pointer to the backing memory.</param>
    /// <param name="refs">The backward references to initialize the histogram with.</param>
    /// <param name="paletteCodeBits">The palette code bits.</param>
    protected Vp8LHistogram(uint* basePointer, Vp8LBackwardRefs refs, int paletteCodeBits)
        : this(basePointer, paletteCodeBits) => this.StoreRefs(refs);

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8LHistogram"/> class.
    /// </summary>
    /// <param name="basePointer">The base pointer to the backing memory.</param>
    /// <param name="paletteCodeBits">The palette code bits.</param>
    protected Vp8LHistogram(uint* basePointer, int paletteCodeBits)
    {
        this.PaletteCodeBits = paletteCodeBits;
        this.red = basePointer;
        this.blue = this.red + RedSize;
        this.alpha = this.blue + BlueSize;
        this.distance = this.alpha + AlphaSize;
        this.literal = this.distance + DistanceSize;
        this.isUsed = this.literal + LiteralSize;
    }

    /// <summary>
    /// Gets or sets the palette code bits.
    /// </summary>
    public int PaletteCodeBits { get; set; }

    /// <summary>
    /// Gets or sets the cached value of bit cost.
    /// </summary>
    public double BitCost { get; set; }

    /// <summary>
    /// Gets or sets the cached value of literal entropy costs.
    /// </summary>
    public double LiteralCost { get; set; }

    /// <summary>
    /// Gets or sets the cached value of red entropy costs.
    /// </summary>
    public double RedCost { get; set; }

    /// <summary>
    /// Gets or sets the cached value of blue entropy costs.
    /// </summary>
    public double BlueCost { get; set; }

    public Span<uint> Red => new(this.red, RedSize);

    public Span<uint> Blue => new(this.blue, BlueSize);

    public Span<uint> Alpha => new(this.alpha, AlphaSize);

    public Span<uint> Distance => new(this.distance, DistanceSize);

    public Span<uint> Literal => new(this.literal, LiteralSize);

    public uint TrivialSymbol { get; set; }

    private Span<uint> IsUsedSpan => new(this.isUsed, UsedSize);

    private Span<uint> TotalSpan => new(this.red, BufferSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsUsed(int index) => this.IsUsedSpan[index] == 1u;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IsUsed(int index, bool value) => this.IsUsedSpan[index] = value ? 1u : 0;

    /// <summary>
    /// Creates a copy of the given <see cref="Vp8LHistogram"/> class.
    /// </summary>
    /// <param name="other">The histogram to copy to.</param>
    public void CopyTo(Vp8LHistogram other)
    {
        this.Red.CopyTo(other.Red);
        this.Blue.CopyTo(other.Blue);
        this.Alpha.CopyTo(other.Alpha);
        this.Literal.CopyTo(other.Literal);
        this.Distance.CopyTo(other.Distance);
        this.IsUsedSpan.CopyTo(other.IsUsedSpan);

        other.LiteralCost = this.LiteralCost;
        other.RedCost = this.RedCost;
        other.BlueCost = this.BlueCost;
        other.BitCost = this.BitCost;
        other.TrivialSymbol = this.TrivialSymbol;
        other.PaletteCodeBits = this.PaletteCodeBits;
    }

    public void Clear()
    {
        this.TotalSpan.Clear();
        this.PaletteCodeBits = 0;
        this.BitCost = 0;
        this.LiteralCost = 0;
        this.RedCost = 0;
        this.BlueCost = 0;
        this.TrivialSymbol = 0;
    }

    /// <summary>
    /// Collect all the references into a histogram (without reset).
    /// </summary>
    /// <param name="refs">The backward references.</param>
    public void StoreRefs(Vp8LBackwardRefs refs)
    {
        foreach (PixOrCopy v in refs)
        {
            this.AddSinglePixOrCopy(in v, false);
        }
    }

    /// <summary>
    /// Accumulate a token 'v' into a histogram.
    /// </summary>
    /// <param name="v">The token to add.</param>
    /// <param name="useDistanceModifier">Indicates whether to use the distance modifier.</param>
    /// <param name="xSize">xSize is only used when useDistanceModifier is true.</param>
    public void AddSinglePixOrCopy(in PixOrCopy v, bool useDistanceModifier, int xSize = 0)
    {
        if (v.IsLiteral())
        {
            this.Alpha[v.Literal(3)]++;
            this.Red[v.Literal(2)]++;
            this.Literal[v.Literal(1)]++;
            this.Blue[v.Literal(0)]++;
        }
        else if (v.IsCacheIdx())
        {
            int literalIx = (int)(WebpConstants.NumLiteralCodes + WebpConstants.NumLengthCodes + v.CacheIdx());
            this.Literal[literalIx]++;
        }
        else
        {
            int extraBits = 0;
            int code = LosslessUtils.PrefixEncodeBits(v.Length(), ref extraBits);
            this.Literal[WebpConstants.NumLiteralCodes + code]++;
            if (!useDistanceModifier)
            {
                code = LosslessUtils.PrefixEncodeBits((int)v.Distance(), ref extraBits);
            }
            else
            {
                code = LosslessUtils.PrefixEncodeBits(BackwardReferenceEncoder.DistanceToPlaneCode(xSize, (int)v.Distance()), ref extraBits);
            }

            this.Distance[code]++;
        }
    }

    public int NumCodes() => WebpConstants.NumLiteralCodes + WebpConstants.NumLengthCodes + (this.PaletteCodeBits > 0 ? 1 << this.PaletteCodeBits : 0);

    /// <summary>
    /// Estimate how many bits the combined entropy of literals and distance approximately maps to.
    /// </summary>
    /// <returns>Estimated bits.</returns>
    public double EstimateBits(Vp8LStreaks stats, Vp8LBitEntropy bitsEntropy)
    {
        uint notUsed = 0;
        return
            this.PopulationCost(this.Literal, this.NumCodes(), ref notUsed, 0, stats, bitsEntropy)
            + this.PopulationCost(this.Red, WebpConstants.NumLiteralCodes, ref notUsed, 1, stats, bitsEntropy)
            + this.PopulationCost(this.Blue, WebpConstants.NumLiteralCodes, ref notUsed, 2, stats, bitsEntropy)
            + this.PopulationCost(this.Alpha, WebpConstants.NumLiteralCodes, ref notUsed, 3, stats, bitsEntropy)
            + this.PopulationCost(this.Distance, WebpConstants.NumDistanceCodes, ref notUsed, 4, stats, bitsEntropy)
            + ExtraCost(this.Literal[WebpConstants.NumLiteralCodes..], WebpConstants.NumLengthCodes)
            + ExtraCost(this.Distance, WebpConstants.NumDistanceCodes);
    }

    public void UpdateHistogramCost(Vp8LStreaks stats, Vp8LBitEntropy bitsEntropy)
    {
        uint alphaSym = 0, redSym = 0, blueSym = 0;
        uint notUsed = 0;

        double alphaCost = this.PopulationCost(this.Alpha, WebpConstants.NumLiteralCodes, ref alphaSym, 3, stats, bitsEntropy);
        double distanceCost = this.PopulationCost(this.Distance, WebpConstants.NumDistanceCodes, ref notUsed, 4, stats, bitsEntropy) + ExtraCost(this.Distance, WebpConstants.NumDistanceCodes);
        int numCodes = this.NumCodes();
        this.LiteralCost = this.PopulationCost(this.Literal, numCodes, ref notUsed, 0, stats, bitsEntropy) + ExtraCost(this.Literal[WebpConstants.NumLiteralCodes..], WebpConstants.NumLengthCodes);
        this.RedCost = this.PopulationCost(this.Red, WebpConstants.NumLiteralCodes, ref redSym, 1, stats, bitsEntropy);
        this.BlueCost = this.PopulationCost(this.Blue, WebpConstants.NumLiteralCodes, ref blueSym, 2, stats, bitsEntropy);
        this.BitCost = this.LiteralCost + this.RedCost + this.BlueCost + alphaCost + distanceCost;
        if ((alphaSym | redSym | blueSym) == NonTrivialSym)
        {
            this.TrivialSymbol = NonTrivialSym;
        }
        else
        {
            this.TrivialSymbol = (alphaSym << 24) | (redSym << 16) | (blueSym << 0);
        }
    }

    /// <summary>
    /// Performs output = a + b, computing the cost C(a+b) - C(a) - C(b) while comparing
    /// to the threshold value 'costThreshold'. The score returned is
    /// Score = C(a+b) - C(a) - C(b), where C(a) + C(b) is known and fixed.
    /// Since the previous score passed is 'costThreshold', we only need to compare
    /// the partial cost against 'costThreshold + C(a) + C(b)' to possibly bail-out early.
    /// </summary>
    public double AddEval(Vp8LHistogram b, Vp8LStreaks stats, Vp8LBitEntropy bitsEntropy, double costThreshold, Vp8LHistogram output)
    {
        double sumCost = this.BitCost + b.BitCost;
        costThreshold += sumCost;
        if (this.GetCombinedHistogramEntropy(b, stats, bitsEntropy, costThreshold, costInitial: 0, out double cost))
        {
            this.Add(b, output);
            output.BitCost = cost;
            output.PaletteCodeBits = this.PaletteCodeBits;
        }

        return cost - sumCost;
    }

    public double AddThresh(Vp8LHistogram b, Vp8LStreaks stats, Vp8LBitEntropy bitsEntropy, double costThreshold)
    {
        double costInitial = -this.BitCost;
        this.GetCombinedHistogramEntropy(b, stats, bitsEntropy, costThreshold, costInitial, out double cost);
        return cost;
    }

    public void Add(Vp8LHistogram b, Vp8LHistogram output)
    {
        int literalSize = this.NumCodes();

        this.AddLiteral(b, output, literalSize);
        this.AddRed(b, output, WebpConstants.NumLiteralCodes);
        this.AddBlue(b, output, WebpConstants.NumLiteralCodes);
        this.AddAlpha(b, output, WebpConstants.NumLiteralCodes);
        this.AddDistance(b, output, WebpConstants.NumDistanceCodes);

        for (int i = 0; i < 5; i++)
        {
            output.IsUsed(i, this.IsUsed(i) | b.IsUsed(i));
        }

        output.TrivialSymbol = this.TrivialSymbol == b.TrivialSymbol
            ? this.TrivialSymbol
            : NonTrivialSym;
    }

    public bool GetCombinedHistogramEntropy(Vp8LHistogram b, Vp8LStreaks stats, Vp8LBitEntropy bitEntropy, double costThreshold, double costInitial, out double cost)
    {
        bool trivialAtEnd = false;
        cost = costInitial;

        cost += GetCombinedEntropy(this.Literal, b.Literal, this.NumCodes(), this.IsUsed(0), b.IsUsed(0), false, stats, bitEntropy);

        cost += ExtraCostCombined(this.Literal[WebpConstants.NumLiteralCodes..], b.Literal[WebpConstants.NumLiteralCodes..], WebpConstants.NumLengthCodes);

        if (cost > costThreshold)
        {
            return false;
        }

        if (this.TrivialSymbol != NonTrivialSym && this.TrivialSymbol == b.TrivialSymbol)
        {
            // A, R and B are all 0 or 0xff.
            uint colorA = (this.TrivialSymbol >> 24) & 0xff;
            uint colorR = (this.TrivialSymbol >> 16) & 0xff;
            uint colorB = (this.TrivialSymbol >> 0) & 0xff;
            if ((colorA == 0 || colorA == 0xff) &&
                (colorR == 0 || colorR == 0xff) &&
                (colorB == 0 || colorB == 0xff))
            {
                trivialAtEnd = true;
            }
        }

        cost += GetCombinedEntropy(this.Red, b.Red, WebpConstants.NumLiteralCodes, this.IsUsed(1), b.IsUsed(1), trivialAtEnd, stats, bitEntropy);
        if (cost > costThreshold)
        {
            return false;
        }

        cost += GetCombinedEntropy(this.Blue, b.Blue, WebpConstants.NumLiteralCodes, this.IsUsed(2), b.IsUsed(2), trivialAtEnd, stats, bitEntropy);
        if (cost > costThreshold)
        {
            return false;
        }

        cost += GetCombinedEntropy(this.Alpha, b.Alpha, WebpConstants.NumLiteralCodes, this.IsUsed(3), b.IsUsed(3), trivialAtEnd, stats, bitEntropy);
        if (cost > costThreshold)
        {
            return false;
        }

        cost += GetCombinedEntropy(this.Distance, b.Distance, WebpConstants.NumDistanceCodes, this.IsUsed(4), b.IsUsed(4), false, stats, bitEntropy);
        if (cost > costThreshold)
        {
            return false;
        }

        cost += ExtraCostCombined(this.Distance, b.Distance, WebpConstants.NumDistanceCodes);
        return cost <= costThreshold;
    }

    private void AddLiteral(Vp8LHistogram b, Vp8LHistogram output, int literalSize)
    {
        if (this.IsUsed(0))
        {
            if (b.IsUsed(0))
            {
                AddVector(this.Literal, b.Literal, output.Literal, literalSize);
            }
            else
            {
                this.Literal[..literalSize].CopyTo(output.Literal);
            }
        }
        else if (b.IsUsed(0))
        {
            b.Literal[..literalSize].CopyTo(output.Literal);
        }
        else
        {
            output.Literal[..literalSize].Clear();
        }
    }

    private void AddRed(Vp8LHistogram b, Vp8LHistogram output, int size)
    {
        if (this.IsUsed(1))
        {
            if (b.IsUsed(1))
            {
                AddVector(this.Red, b.Red, output.Red, size);
            }
            else
            {
                this.Red[..size].CopyTo(output.Red);
            }
        }
        else if (b.IsUsed(1))
        {
            b.Red[..size].CopyTo(output.Red);
        }
        else
        {
            output.Red[..size].Clear();
        }
    }

    private void AddBlue(Vp8LHistogram b, Vp8LHistogram output, int size)
    {
        if (this.IsUsed(2))
        {
            if (b.IsUsed(2))
            {
                AddVector(this.Blue, b.Blue, output.Blue, size);
            }
            else
            {
                this.Blue[..size].CopyTo(output.Blue);
            }
        }
        else if (b.IsUsed(2))
        {
            b.Blue[..size].CopyTo(output.Blue);
        }
        else
        {
            output.Blue[..size].Clear();
        }
    }

    private void AddAlpha(Vp8LHistogram b, Vp8LHistogram output, int size)
    {
        if (this.IsUsed(3))
        {
            if (b.IsUsed(3))
            {
                AddVector(this.Alpha, b.Alpha, output.Alpha, size);
            }
            else
            {
                this.Alpha[..size].CopyTo(output.Alpha);
            }
        }
        else if (b.IsUsed(3))
        {
            b.Alpha[..size].CopyTo(output.Alpha);
        }
        else
        {
            output.Alpha[..size].Clear();
        }
    }

    private void AddDistance(Vp8LHistogram b, Vp8LHistogram output, int size)
    {
        if (this.IsUsed(4))
        {
            if (b.IsUsed(4))
            {
                AddVector(this.Distance, b.Distance, output.Distance, size);
            }
            else
            {
                this.Distance[..size].CopyTo(output.Distance);
            }
        }
        else if (b.IsUsed(4))
        {
            b.Distance[..size].CopyTo(output.Distance);
        }
        else
        {
            output.Distance[..size].Clear();
        }
    }

    private static double GetCombinedEntropy(
        Span<uint> x,
        Span<uint> y,
        int length,
        bool isXUsed,
        bool isYUsed,
        bool trivialAtEnd,
        Vp8LStreaks stats,
        Vp8LBitEntropy bitEntropy)
    {
        stats.Clear();
        bitEntropy.Init();
        if (trivialAtEnd)
        {
            // This configuration is due to palettization that transforms an indexed
            // pixel into 0xff000000 | (pixel << 8) in BundleColorMap.
            // BitsEntropyRefine is 0 for histograms with only one non-zero value.
            // Only FinalHuffmanCost needs to be evaluated.

            // Deal with the non-zero value at index 0 or length-1.
            stats.Streaks[1][0] = 1;

            // Deal with the following/previous zero streak.
            stats.Counts[0] = 1;
            stats.Streaks[0][1] = length - 1;

            return stats.FinalHuffmanCost();
        }

        if (isXUsed)
        {
            if (isYUsed)
            {
                bitEntropy.GetCombinedEntropyUnrefined(x, y, length, stats);
            }
            else
            {
                bitEntropy.GetEntropyUnrefined(x, length, stats);
            }
        }
        else if (isYUsed)
        {
            bitEntropy.GetEntropyUnrefined(y, length, stats);
        }
        else
        {
            stats.Counts[0] = 1;
            stats.Streaks[0][length > 3 ? 1 : 0] = length;
            bitEntropy.Init();
        }

        return bitEntropy.BitsEntropyRefine() + stats.FinalHuffmanCost();
    }

    private static double ExtraCostCombined(Span<uint> x, Span<uint> y, int length)
    {
        double cost = 0.0d;
        for (int i = 2; i < length - 2; i++)
        {
            int xy = (int)(x[i + 2] + y[i + 2]);
            cost += (i >> 1) * xy;
        }

        return cost;
    }

    /// <summary>
    /// Get the symbol entropy for the distribution 'population'.
    /// </summary>
    private double PopulationCost(Span<uint> population, int length, ref uint trivialSym, int isUsedIndex, Vp8LStreaks stats, Vp8LBitEntropy bitEntropy)
    {
        bitEntropy.Init();
        stats.Clear();
        bitEntropy.BitsEntropyUnrefined(population, length, stats);

        trivialSym = (bitEntropy.NoneZeros == 1) ? bitEntropy.NoneZeroCode : NonTrivialSym;

        // The histogram is used if there is at least one non-zero streak.
        this.IsUsed(isUsedIndex, stats.Streaks[1][0] != 0 || stats.Streaks[1][1] != 0);

        return bitEntropy.BitsEntropyRefine() + stats.FinalHuffmanCost();
    }

    private static double ExtraCost(Span<uint> population, int length)
    {
        double cost = 0.0d;
        for (int i = 2; i < length - 2; i++)
        {
            cost += (i >> 1) * population[i + 2];
        }

        return cost;
    }

    private static void AddVector(Span<uint> a, Span<uint> b, Span<uint> output, int count)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(a.Length, count, nameof(a.Length));
        DebugGuard.MustBeGreaterThanOrEqualTo(b.Length, count, nameof(b.Length));
        DebugGuard.MustBeGreaterThanOrEqualTo(output.Length, count, nameof(output.Length));

        if (Avx2.IsSupported && count >= 32)
        {
            ref uint aRef = ref MemoryMarshal.GetReference(a);
            ref uint bRef = ref MemoryMarshal.GetReference(b);
            ref uint outputRef = ref MemoryMarshal.GetReference(output);

            nuint idx = 0;
            do
            {
                // Load values.
                Vector256<uint> a0 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref aRef, idx + 0));
                Vector256<uint> a1 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref aRef, idx + 8));
                Vector256<uint> a2 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref aRef, idx + 16));
                Vector256<uint> a3 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref aRef, idx + 24));
                Vector256<uint> b0 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref bRef, idx + 0));
                Vector256<uint> b1 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref bRef, idx + 8));
                Vector256<uint> b2 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref bRef, idx + 16));
                Vector256<uint> b3 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref bRef, idx + 24));

                // Note we are adding uint32_t's as *signed* int32's (using _mm_add_epi32). But
                // that's ok since the histogram values are less than 1<<28 (max picture count).
                Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref outputRef, idx + 0)) = Avx2.Add(a0, b0);
                Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref outputRef, idx + 8)) = Avx2.Add(a1, b1);
                Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref outputRef, idx + 16)) = Avx2.Add(a2, b2);
                Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref outputRef, idx + 24)) = Avx2.Add(a3, b3);
                idx += 32;
            }
            while (idx <= (uint)count - 32);

            int i = (int)idx;
            for (; i < count; i++)
            {
                output[i] = a[i] + b[i];
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                output[i] = a[i] + b[i];
            }
        }
    }
}

internal sealed unsafe class OwnedVp8LHistogram : Vp8LHistogram, IDisposable
{
    private readonly IMemoryOwner<uint> bufferOwner;
    private MemoryHandle bufferHandle;
    private bool isDisposed;

    private OwnedVp8LHistogram(
        IMemoryOwner<uint> bufferOwner,
        ref MemoryHandle bufferHandle,
        uint* basePointer,
        int paletteCodeBits)
        : base(basePointer, paletteCodeBits)
    {
        this.bufferOwner = bufferOwner;
        this.bufferHandle = bufferHandle;
    }

    /// <summary>
    /// Creates an <see cref="OwnedVp8LHistogram"/> that is not a member of a <see cref="Vp8LHistogramSet"/>.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="paletteCodeBits">The palette code bits.</param>
    public static OwnedVp8LHistogram Create(MemoryAllocator memoryAllocator, int paletteCodeBits)
    {
        IMemoryOwner<uint> bufferOwner = memoryAllocator.Allocate<uint>(BufferSize, AllocationOptions.Clean);
        MemoryHandle bufferHandle = bufferOwner.Memory.Pin();
        return new(bufferOwner, ref bufferHandle, (uint*)bufferHandle.Pointer, paletteCodeBits);
    }

    /// <summary>
    /// Creates an <see cref="OwnedVp8LHistogram"/> that is not a member of a <see cref="Vp8LHistogramSet"/>.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="refs">The backward references to initialize the histogram with.</param>
    /// <param name="paletteCodeBits">The palette code bits.</param>
    public static OwnedVp8LHistogram Create(MemoryAllocator memoryAllocator, Vp8LBackwardRefs refs, int paletteCodeBits)
    {
        OwnedVp8LHistogram histogram = Create(memoryAllocator, paletteCodeBits);
        histogram.StoreRefs(refs);
        return histogram;
    }

    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.bufferHandle.Dispose();
            this.bufferOwner.Dispose();
            this.isDisposed = true;
        }
    }
}
