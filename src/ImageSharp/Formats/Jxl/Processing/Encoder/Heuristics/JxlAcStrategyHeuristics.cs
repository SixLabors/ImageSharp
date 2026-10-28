// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Heuristics;

internal class JxlAcStrategyHeuristics(Configuration configuration, JxlCompressionParameters cparams) : IDisposable
{
    private IMemoryOwner<float>? memoryOwner;
    private IMemoryOwner<int>? qMemoryOwner;

    public Configuration Configuration { get; set; } = configuration;

    public JxlCompressionParameters CompressionParameters { get; set; } = cparams;

    public JxlAcStrategyConfiguration Config { get; set; } = new(new());

    public int MemoryPerThread { get; set; }

    public Memory<float> Memory { get; set; }

    public int QMemoryPerThread { get; set; }

    public Memory<int> QMemory { get; set; }

    public void Dispose()
    {
        this.memoryOwner?.Dispose();
        this.qMemoryOwner?.Dispose();
    }

    public bool Initialize(
        JxlImage3<float> src,
        Rectangle rectIn,
        JxlPlane<float> quantField,
        JxlPlane<float> mask,
        JxlPlane<float> mask1x1,
        JxlDequantMatrices matrices)
    {
        this.Config.Dequant = matrices;

        if (this.CompressionParameters.SpeedTier < JxlSpeedTier.Cheetah)
        {
            uint acsMask = 0;

            for (int i = 0; i < (int)JxlAcStrategyType.DCT128X128; i++)
            {
                acsMask |= (uint)(1 << i);
            }

            if (!matrices.EnsureComputed(this.Configuration, (int)acsMask))
            {
                return false;
            }
        }
        else
        {
            if (!matrices.EnsureComputed(this.Configuration, 1))
            {
                return false;
            }
        }

        this.Config.QuantFieldRow = quantField.GetRowMemory(0);
        this.Config.QuantFieldStride = quantField.PixelsPerRow;

        if (mask.XSize > 0 && mask.YSize > 0)
        {
            this.Config.MaskingFieldRow = mask.GetRowMemory(0);
            this.Config.MaskingFieldStride = mask.PixelsPerRow;
        }

        this.Config.Mask1x1Width = mask1x1.XSize;

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

    public void PrepareForThreads(int numThreads)
    {
        int dctScratchSize = 3 * (JxlSimdUtils.MaxVectorSize / sizeof(float)) * JxlAcStrategy.MaximumBlockDimension;

        this.MemoryPerThread = (6 * JxlAcStrategy.MaximumCoefficientArea) + dctScratchSize;
        int memBytes = numThreads * this.MemoryPerThread;
        this.memoryOwner = this.Configuration.MemoryAllocator.Allocate<float>(memBytes);
        this.Memory = this.memoryOwner.Memory;

        this.QMemoryPerThread = JxlAcStrategy.MaximumCoefficientArea;
        int qmemBytes = numThreads * this.QMemoryPerThread;
        this.qMemoryOwner = this.Configuration.MemoryAllocator.Allocate<int>(qmemBytes);
        this.QMemory = this.qMemoryOwner.Memory;
    }

    public bool ProcessRectangle(Rectangle rect, JxlColorCorrelationMap cmap, JxlAcStrategyImage acStrategy, int thread)
    {
        if (this.CompressionParameters.SpeedTier >= JxlSpeedTier.Cheetah)
        {
            acStrategy.FillDct8(in rect);
            return true;
        }

        ref int qmem = ref this.QMemory.Span[thread * this.QMemoryPerThread];

        bool status = JxlAcStrategyEncoder.ProcessAcStrategyRectangle(
            this.CompressionParameters,
            this.Config,
            rect,
            cmap,
            this.Memory[(thread * this.MemoryPerThread)..],
            ref qmem,
            acStrategy);

        return status;
    }

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
