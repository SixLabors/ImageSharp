// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

internal class CostModel
{
    private readonly MemoryAllocator memoryAllocator;
    private const int ValuesInBytes = 256;

    /// <summary>
    /// Initializes a new instance of the <see cref="CostModel"/> class.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="literalArraySize">The literal array size.</param>
    public CostModel(MemoryAllocator memoryAllocator, int literalArraySize)
    {
        this.memoryAllocator = memoryAllocator;
        this.Alpha = new double[ValuesInBytes];
        this.Red = new double[ValuesInBytes];
        this.Blue = new double[ValuesInBytes];
        this.Distance = new double[WebpConstants.NumDistanceCodes];
        this.Literal = new double[literalArraySize];
    }

    public double[] Alpha { get; }

    public double[] Red { get; }

    public double[] Blue { get; }

    public double[] Distance { get; }

    public double[] Literal { get; }

    public void Build(int xSize, int cacheBits, Vp8LBackwardRefs backwardRefs)
    {
        using OwnedVp8LHistogram histogram = OwnedVp8LHistogram.Create(this.memoryAllocator, cacheBits);

        // The following code is similar to HistogramCreate but converts the distance to plane code.
        for (int i = 0; i < backwardRefs.Count; i++)
        {
            histogram.AddSinglePixOrCopy(backwardRefs[i], true, xSize);
        }

        ConvertPopulationCountTableToBitEstimates(histogram.NumCodes(), histogram.Literal, this.Literal);
        ConvertPopulationCountTableToBitEstimates(ValuesInBytes, histogram.Red, this.Red);
        ConvertPopulationCountTableToBitEstimates(ValuesInBytes, histogram.Blue, this.Blue);
        ConvertPopulationCountTableToBitEstimates(ValuesInBytes, histogram.Alpha, this.Alpha);
        ConvertPopulationCountTableToBitEstimates(WebpConstants.NumDistanceCodes, histogram.Distance, this.Distance);
    }

    public double GetLengthCost(int length)
    {
        int extraBits = 0;
        int code = LosslessUtils.PrefixEncodeBits(length, ref extraBits);
        return this.Literal[ValuesInBytes + code] + extraBits;
    }

    public double GetDistanceCost(int distance)
    {
        int extraBits = 0;
        int code = LosslessUtils.PrefixEncodeBits(distance, ref extraBits);
        return this.Distance[code] + extraBits;
    }

    public double GetCacheCost(uint idx)
    {
        int literalIdx = (int)(ValuesInBytes + WebpConstants.NumLengthCodes + idx);
        return this.Literal[literalIdx];
    }

    public double GetLiteralCost(uint v) => this.Alpha[v >> 24] + this.Red[(v >> 16) & 0xff] + this.Literal[(v >> 8) & 0xff] + this.Blue[v & 0xff];

    private static void ConvertPopulationCountTableToBitEstimates(int numSymbols, Span<uint> populationCounts, double[] output)
    {
        uint sum = 0;
        int nonzeros = 0;
        for (int i = 0; i < numSymbols; i++)
        {
            sum += populationCounts[i];
            if (populationCounts[i] > 0)
            {
                nonzeros++;
            }
        }

        if (nonzeros <= 1)
        {
            output.AsSpan(0, numSymbols).Clear();
        }
        else
        {
            double logsum = LosslessUtils.FastLog2(sum);
            for (int i = 0; i < numSymbols; i++)
            {
                output[i] = logsum - LosslessUtils.FastLog2(populationCounts[i]);
            }
        }
    }
}
