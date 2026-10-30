// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Heuristics;

/// <summary>
/// Selects transform strategies with one reusable workspace for sequential rectangle processing.
/// </summary>
/// <remarks>
/// Source images, masks and matrices remain owned by the frame encoder. They must outlive this helper's processing calls.
/// Each search rectangle covers at most eight by eight blocks. Scratch also accommodates the largest supported transform candidate.
/// </remarks>
internal sealed class JxlAcStrategyHeuristics : IDisposable
{
    /// <summary>
    /// Owns floating-point search storage, absent when the configured speed uses only DCT8.
    /// </summary>
    private readonly IMemoryOwner<float>? memoryOwner;

    /// <summary>
    /// Borrows the floating-point workspace for every processed rectangle.
    /// </summary>
    private readonly Memory<float> scratch;

    /// <summary>
    /// Whether the configured speed selects DCT8 without searching other strategies.
    /// </summary>
    private readonly bool useDct8Only;

    /// <summary>
    /// Initializes strategy selection with the frame's real matrices and one sequential workspace.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns scratch storage.</param>
    /// <param name="parameters">The frame encoder's compression settings, which remain fixed while this helper is used.</param>
    /// <param name="matrices">The frame-owned dequantization matrices borrowed by the search.</param>
    public JxlAcStrategyHeuristics(Configuration configuration, JxlCompressionParameters parameters, JxlDequantMatrices matrices)
    {
        this.Configuration = configuration;
        this.CompressionParameters = parameters;
        this.Config = new(matrices);
        this.useDct8Only = parameters.SpeedTier >= JxlSpeedTier.Cheetah;

        if (!this.useDct8Only)
        {
            // Counts are float elements, not bytes. Three maximum-area regions hold the candidate's channel coefficients.
            // A fourth holds reconstruction error. The remaining regions serve block storage and two banks of one-dimensional transform vectors.
            // Each maximum-area region accommodates a 256 by 256 transform. Rounded coefficients remain float vectors in the entropy calculation.
            int dctScratchLength = JxlDct.MaximumWorkingSetLength;
            int scratchLength = (6 * JxlAcStrategy.MaximumCoefficientArea) + dctScratchLength;
            IMemoryOwner<float> memory = configuration.MemoryAllocator.Allocate<float>(scratchLength);
            this.memoryOwner = memory;
            this.scratch = memory.Memory;
        }
    }

    /// <summary>
    /// Gets the configuration that owns scratch allocations.
    /// </summary>
    public Configuration Configuration { get; }

    /// <summary>
    /// Gets the frame encoder's fixed compression settings.
    /// </summary>
    public JxlCompressionParameters CompressionParameters { get; }

    /// <summary>
    /// Gets the matrix references and borrowed source views used by the strategy kernel.
    /// </summary>
    public JxlAcStrategyConfiguration Config { get; }

    /// <summary>
    /// Releases owned scratch without disposing the borrowed source, masks or matrices.
    /// </summary>
    public void Dispose() => this.memoryOwner?.Dispose();

    /// <summary>
    /// Binds source and mask rows and prepares the matrices needed by the configured strategy search.
    /// </summary>
    /// <param name="src">The frame-owned source planes.</param>
    /// <param name="rectIn">The block-aligned source region corresponding to the search coordinates.</param>
    /// <param name="quantField">The frame-owned quantization field in block coordinates.</param>
    /// <param name="mask">The optional masking plane, or an empty plane when masking is absent.</param>
    /// <param name="mask1x1">The optional per-block masking plane, or an empty plane when absent.</param>
    /// <returns>Whether the required matrices were computed. Processing starts only after this returns true.</returns>
    public bool Initialize(
        JxlImage3<float> src,
        Rectangle rectIn,
        JxlPlane<float> quantField,
        JxlPlane<float> mask,
        JxlPlane<float> mask1x1)
    {
        JxlDequantMatrices matrices = this.Config.Dequant;

        if (!this.useDct8Only)
        {
            uint acsMask = 0;

            for (int i = 0; i < (int)JxlAcStrategyType.DCT128X128; i++)
            {
                acsMask |= (uint)(1 << i);
            }

            if (!matrices.EnsureComputed(this.Configuration, acsMask))
            {
                return false;
            }
        }
        else
        {
            if (!matrices.EnsureComputed(this.Configuration, 1U))
            {
                return false;
            }
        }

        this.Config.QuantFieldRow = quantField.GetRowMemory(0);
        this.Config.QuantFieldStride = quantField.PixelsPerRow;

        // Rebinding must also remove optional views from an earlier source. Empty masks never borrow a row.
        this.Config.MaskingFieldRow = default;
        this.Config.MaskingFieldStride = 0;

        if (mask.XSize > 0 && mask.YSize > 0)
        {
            this.Config.MaskingFieldRow = mask.GetRowMemory(0);
            this.Config.MaskingFieldStride = mask.PixelsPerRow;
        }

        this.Config.Mask1x1Width = mask1x1.XSize;
        this.Config.Masking1x1FieldRow = default;
        this.Config.Masking1x1FieldStride = 0;

        if (mask1x1.XSize > 0 && mask1x1.YSize > 0)
        {
            this.Config.Masking1x1FieldRow = mask1x1.GetRowMemory(0);
            this.Config.Masking1x1FieldStride = mask1x1.PixelsPerRow;
        }

        this.Config.SourceRows[0] = src.Plane(0).GetRowMemory(rectIn, 0);
        this.Config.SourceRows[1] = src.Plane(1).GetRowMemory(rectIn, 0);
        this.Config.SourceRows[2] = src.Plane(2).GetRowMemory(rectIn, 0);
        this.Config.SourceStride = src.PixelsPerRow;

        this.Config.InfoLossMultiplier = 1.2f;
        this.Config.ZerosMultiplier = 9.3089059022677905f;
        this.Config.CostDelta = 10.833273317067883f;

        const float bias = 0.13731742964354549f;
        float ratio = (this.CompressionParameters.ButteraugliDistance + bias) / (1.0f + bias);

        const float pow1 = 0.33677806662454718f;
        const float pow2 = 0.50990926717963703f;
        const float pow3 = 0.36702940662370243f;

        this.Config.InfoLossMultiplier *= MathF.Pow(ratio, pow1);
        this.Config.ZerosMultiplier *= MathF.Pow(ratio, pow2);
        this.Config.CostDelta *= MathF.Pow(ratio, pow3);

        return true;
    }

    /// <summary>
    /// Selects strategies for one rectangle using the same scratch as all preceding rectangles.
    /// </summary>
    /// <param name="rect">The search rectangle in block coordinates, at most eight by eight blocks.</param>
    /// <param name="cmap">The frame-owned color-correlation maps.</param>
    /// <param name="acStrategy">The destination strategy image.</param>
    /// <returns>Whether the rectangle's strategies were selected successfully.</returns>
    public bool ProcessRectangle(Rectangle rect, JxlColorCorrelationMap cmap, JxlAcStrategyImage acStrategy)
    {
        if (this.useDct8Only)
        {
            acStrategy.FillDct8(in rect);
            return true;
        }

        // Borrow the span once for this rectangle. The kernel reuses its regions for every candidate; none escape this call.
        Span<float> scratch = this.scratch.Span;
        ReadOnlySpan<float> matrixStorage = this.Config.Dequant.GetPreparedStorage();

        return JxlAcStrategyEncoder.ProcessAcStrategyRectangle(
            this.CompressionParameters,
            this.Config,
            matrixStorage,
            rect,
            cmap,
            scratch,
            acStrategy);
    }

    /// <summary>
    /// Records the selected strategy counts when the encoder requests statistics.
    /// </summary>
    /// <param name="acStrategy">The completed strategy image.</param>
    /// <param name="auxOut">The optional encoder statistics.</param>
    public void Finalize(JxlAcStrategyImage acStrategy, JxlAuxiliaryOutput? auxOut)
    {
        if (auxOut is not null)
        {
            auxOut.NumberOfSmallBlocks =
                acStrategy.CountBlocks(JxlAcStrategyType.IDENTITY) +
                acStrategy.CountBlocks(JxlAcStrategyType.DCT2X2) +
                acStrategy.CountBlocks(JxlAcStrategyType.DCT4X4);

            auxOut.NumberOfDct4x8Blocks =
                acStrategy.CountBlocks(JxlAcStrategyType.DCT4X8) +
                acStrategy.CountBlocks(JxlAcStrategyType.DCT8X4);

            auxOut.NumberOfAfvBlocks = acStrategy.CountBlocks(JxlAcStrategyType.AFV0) +
                                      acStrategy.CountBlocks(JxlAcStrategyType.AFV1) +
                                      acStrategy.CountBlocks(JxlAcStrategyType.AFV2) +
                                      acStrategy.CountBlocks(JxlAcStrategyType.AFV3);

            auxOut.NumberOfDct8Blocks = acStrategy.CountBlocks(JxlAcStrategyType.DCT);

            auxOut.NumberOfDct8x16Blocks =
                acStrategy.CountBlocks(JxlAcStrategyType.DCT8X16) +
                acStrategy.CountBlocks(JxlAcStrategyType.DCT16X8);

            auxOut.NumberOfDct8x32Blocks =
                acStrategy.CountBlocks(JxlAcStrategyType.DCT8X32) +
                acStrategy.CountBlocks(JxlAcStrategyType.DCT32X8);

            auxOut.NumberOfDct16Blocks =
                acStrategy.CountBlocks(JxlAcStrategyType.DCT16X16);

            auxOut.NumberOfDct16x32Blocks =
                acStrategy.CountBlocks(JxlAcStrategyType.DCT16X32) +
                acStrategy.CountBlocks(JxlAcStrategyType.DCT32X16);

            auxOut.NumberOfDct32Blocks =
                acStrategy.CountBlocks(JxlAcStrategyType.DCT32X32);

            auxOut.NumberOfDct32x64Blocks =
                acStrategy.CountBlocks(JxlAcStrategyType.DCT32X64) +
                acStrategy.CountBlocks(JxlAcStrategyType.DCT64X32);

            auxOut.NumberOfDct64Blocks =
                acStrategy.CountBlocks(JxlAcStrategyType.DCT64X64);
        }
    }
}
