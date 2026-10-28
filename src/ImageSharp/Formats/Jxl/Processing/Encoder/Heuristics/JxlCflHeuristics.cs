// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Heuristics;

/// <summary>
/// Chroma from luma heuristics
/// </summary>
internal sealed class JxlCflHeuristics(Configuration configuration) : IDisposable
{
    // TODO
    private Memory<byte> scratchSpace;
    private JxlPlane<float>? dcValues;
    private IMemoryOwner<byte>? scratchSpaceMemoryOwner;

    private static int ItemsPerThread
    {
        get
        {
            int dctScratchSize = 3 * (JxlSimdUtils.MaxVectorSize / sizeof(float)) * JxlAcStrategy.MaximumBlockDimension;
            int dcScratchSize = 3 * JxlAcStrategy.MaximumCoefficientBlocks * JxlAcStrategy.MaximumCoefficientBlocks;

            return (JxlAcStrategy.MaximumCoefficientArea * 3)
                + (JxlChromaFromLuma.ColorTileDimension * JxlChromaFromLuma.ColorTileDimension * 4)
                + (JxlAcStrategy.MaximumCoefficientArea * 2)
                + dctScratchSize + dcScratchSize;
        }
    }

    public void PrepareForThreads(int numThreads)
    {
        int memBytes = numThreads * ItemsPerThread * sizeof(float);

        this.scratchSpaceMemoryOwner = configuration.MemoryAllocator.Allocate<byte>(memBytes);
        this.scratchSpace = this.scratchSpaceMemoryOwner.Memory;
    }

    public void Initialize(Rectangle rect)
    {
        int xSizeBlocks = rect.Width / JxlFrameDimensions.BlockDimensions;
        int ySizeBlocks = rect.Height / JxlFrameDimensions.BlockDimensions;

        this.dcValues = JxlChromaFromLumaEncoder.InitializeDcStorage(configuration, xSizeBlocks * ySizeBlocks);
    }

    public void ComputeTile(
        Rectangle rectangle,
        JxlImage3<float> opsin,
        Rectangle opsinRect,
        JxlDequantMatrices dequant,
        JxlAcStrategyImage? acStrategy,
        JxlPlane<int> rawQuantField,
        JxlQuantizer quantizer,
        bool fast,
        int thread,
        JxlColorCorrelationMap cmap)
    {
        if (this.dcValues is null)
        {
            throw new InvalidOperationException("CfL heuristics not initialized; call .Initialize(Rectangle) first");
        }

        bool useDct8 = acStrategy is null;
        int itemsPerThread = ItemsPerThread;

        Span<float> scratch = MemoryMarshal.Cast<byte, float>(this.scratchSpace.Span).Slice(thread * itemsPerThread, itemsPerThread);
        JxlChromaFromLumaEncoder.ComputeTile(
            opsin,
            opsinRect,
            dequant,
            acStrategy,
            rawQuantField,
            quantizer,
            rectangle,
            fast,
            useDct8,
            cmap.YToXMap!,
            cmap.YToBMap!,
            this.dcValues,
            scratch);
    }

    public void Dispose() => this.scratchSpaceMemoryOwner?.Dispose();
}
