// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group;

/// <summary>
/// Cache for the JPEG XL Group Decoder.
/// </summary>
internal sealed class JxlGroupDecoderCache(Configuration configuration)
{
    private IMemoryOwner<float>? floatMemory;
    private IMemoryOwner<int>? int32Memory;
    private IMemoryOwner<short>? int16Memory;
    private int maxBlockArea;

    public Memory<float> DecodeGroupBlockScratchSpace { get; set; }

    public Memory<int> DecodeGroupQBlockScratchSpace { get; set; }

    public Memory<short> DecodeGroupQBlock16ScratchSpace { get; set; }

    /// <summary>
    /// Gets or sets the scratch-space for <see cref="JxlTransformsDecoder.TransformToPixels(JxlAcStrategyType, Span{float}, Span{float}, int, Span{float})"/>.
    /// </summary>
    public Memory<float> ScratchSpaceMemory { get; set; }

    /// <summary>
    /// Gets or sets the number of non-zero coefficients.
    /// </summary>
    public JxlImage3I[] NumberOfNonZeroes { get; set; } = ArrayHelpers.CreateAndInitialize(JxlShared.MaximumNumberOfPasses, () => new JxlImage3I());

    public JxlImageF DcBuffer { get; set; } = new();

    public void InitializeDcBufferOnce()
    {
        if (this.DcBuffer.XSize == 0)
        {
            // When we do new() on a JxlImage of any type,
            // its width/height is 0. So that way we know
            // it's not initted.
            this.DcBuffer = new(
                configuration,
                JxlFrameDimensions.GroupDimensionsInBlocks + (RenderPipelineStageBase.RenderPipelineXOffset * 2),
                JxlFrameDimensions.GroupDimensionsInBlocks + 4);
        }
    }

    public void InitializeOnce(int numPasses, int usedACStrategy)
    {
        for (int i = 0; i < numPasses; i++)
        {
            if (this.NumberOfNonZeroes[i].XSize == 0)
            {
                // Uninitialized.
                this.NumberOfNonZeroes[i] = new(
                    configuration,
                    JxlFrameDimensions.GroupDimensionsInBlocks,
                    JxlFrameDimensions.GroupDimensionsInBlocks);
            }
        }

        int maxBlockArea = 0;

        for (int o = 0; o < JxlAcStrategy.NumberOfValidStrategies; ++o)
        {
            JxlAcStrategy acs = new(o);
            if ((usedACStrategy & (1 << o)) == 0)
            {
                continue;
            }

            int area = acs.CoveredBlocksX * acs.CoveredBlocksY * JxlFrameDimensions.DctBlockSize;
            maxBlockArea = Math.Max(area, maxBlockArea);
        }

        if (maxBlockArea > this.maxBlockArea)
        {
            this.maxBlockArea = maxBlockArea;

            this.floatMemory = configuration.MemoryAllocator.Allocate<float>(this.maxBlockArea * 7);
            this.int32Memory = configuration.MemoryAllocator.Allocate<int>(this.maxBlockArea * 3);
            this.int16Memory = configuration.MemoryAllocator.Allocate<short>(this.maxBlockArea * 3);
        }

        this.DecodeGroupBlockScratchSpace = this.floatMemory?.Memory ?? Memory<float>.Empty;
        this.DecodeGroupQBlockScratchSpace = this.int32Memory?.Memory ?? Memory<int>.Empty;
        this.DecodeGroupQBlock16ScratchSpace = this.int16Memory?.Memory ?? Memory<short>.Empty;

        if (this.DecodeGroupBlockScratchSpace.Length > 0)
        {
            this.ScratchSpaceMemory = this.DecodeGroupBlockScratchSpace[(this.maxBlockArea * 3)..];
        }
        else
        {
            this.ScratchSpaceMemory = Memory<float>.Empty;
        }
    }
}
