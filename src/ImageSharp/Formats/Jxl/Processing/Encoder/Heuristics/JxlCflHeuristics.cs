// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Heuristics;

/// <summary>
/// Computes color-correlation tiles with one reusable sequential workspace and frame-wide DC storage.
/// </summary>
/// <remarks>Source planes, quantization controls and destination maps remain owned by the frame encoder.</remarks>
internal sealed class JxlCflHeuristics : IDisposable
{
    /// <summary>
    /// Owns floating-point coefficient and transform scratch reused by every tile.
    /// </summary>
    private readonly IMemoryOwner<float> scratchSpaceMemoryOwner;

    /// <summary>
    /// Borrows the owned scratch allocation without a byte-to-float memory cast.
    /// </summary>
    private readonly Memory<float> scratchSpace;

    /// <summary>
    /// Owns four rows of frame-wide DC pairs: Y for X, X, Y for B, and B.
    /// </summary>
    private readonly JxlPlane<float> dcValues;

    /// <summary>
    /// Initializes storage for the frame's block-aligned source region.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns sample storage.</param>
    /// <param name="rect">The positive frame region in pixels. Its width and height are multiples of the eight-pixel block dimension.</param>
    public JxlCflHeuristics(Configuration configuration, Rectangle rect)
    {
        int xSizeBlocks = rect.Width / JxlFrameDimensions.BlockDimensions;
        int ySizeBlocks = rect.Height / JxlFrameDimensions.BlockDimensions;
        this.dcValues = JxlChromaFromLumaEncoder.InitializeDcStorage(configuration, checked(xSizeBlocks * ySizeBlocks));

        try
        {
            this.scratchSpaceMemoryOwner = configuration.MemoryAllocator.Allocate<float>(ScratchLength);
        }
        catch
        {
            this.dcValues.Dispose();
            throw;
        }

        this.scratchSpace = this.scratchSpaceMemoryOwner.Memory;
    }

    /// <summary>
    /// Gets the workspace length in float elements for the existing tile kernel.
    /// </summary>
    private static int ScratchLength
    {
        get
        {
            // Transform blocks can cover up to 256 by 256 pixels. A color tile covers 64 by 64 pixels, independently of that maximum.
            // Regions follow ComputeTile's order: three transform blocks; four tile coefficient arrays; three DC arrays; transform scratch.
            int dctScratchLength = 3 * (JxlSimdUtils.MaxVectorSize / sizeof(float)) * JxlAcStrategy.MaximumBlockDimension;
            int dcScratchLength = 3 * JxlAcStrategy.MaximumCoefficientBlocks * JxlAcStrategy.MaximumCoefficientBlocks;

            return (JxlAcStrategy.MaximumCoefficientArea * 3)
                + (JxlChromaFromLuma.ColorTileDimension * JxlChromaFromLuma.ColorTileDimension * 4)
                + dcScratchLength
                + (JxlAcStrategy.MaximumCoefficientArea * 2)
                + dctScratchLength;
        }
    }

    /// <summary>
    /// Computes one color-correlation tile using the same scratch as all preceding tiles.
    /// </summary>
    /// <param name="rectangle">The tile in block coordinates, at most eight by eight blocks.</param>
    /// <param name="opsin">The frame-owned XYB sample planes.</param>
    /// <param name="opsinRect">The pixel region corresponding to the frame-wide DC storage.</param>
    /// <param name="dequant">The frame-owned dequantization matrices.</param>
    /// <param name="acStrategy">The selected strategies, or null to use DCT8 for every block.</param>
    /// <param name="rawQuantField">The frame-owned quantization field.</param>
    /// <param name="quantizer">The frame-owned quantization controls.</param>
    /// <param name="fast">Whether to use the existing fast correlation estimate.</param>
    /// <param name="cmap">The destination color-correlation maps.</param>
    public void ComputeTile(
        Rectangle rectangle,
        JxlImage3<float> opsin,
        Rectangle opsinRect,
        JxlDequantMatrices dequant,
        JxlAcStrategyImage? acStrategy,
        JxlPlane<int> rawQuantField,
        JxlQuantizer quantizer,
        bool fast,
        JxlColorCorrelationMap cmap)
    {
        // Borrow once before entering the coefficient loops. The helper owns both buffers; the kernel only uses their views.
        Span<float> scratch = this.scratchSpace.Span;
        JxlChromaFromLumaEncoder.ComputeTile(
            opsin,
            opsinRect,
            dequant,
            acStrategy,
            rawQuantField,
            quantizer,
            rectangle,
            fast,
            acStrategy is null,
            cmap.YToXMap!,
            cmap.YToBMap!,
            this.dcValues,
            scratch);
    }

    /// <summary>
    /// Releases the owned scratch and DC storage without disposing borrowed frame inputs.
    /// </summary>
    public void Dispose()
    {
        this.scratchSpaceMemoryOwner.Dispose();
        this.dcValues.Dispose();
    }
}
