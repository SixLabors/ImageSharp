// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group;

/// <summary>
/// Cache for the JPEG XL Group Decoder.
/// </summary>
internal sealed class JxlGroupDecoderCache(Configuration configuration) : IDisposable
{
    /// <summary>
    /// Owns the floating-point coefficient and transform workspace.
    /// </summary>
    private IMemoryOwner<float>? floatMemory;

    /// <summary>
    /// Owns the full-width quantized coefficients.
    /// </summary>
    private IMemoryOwner<int>? int32Memory;

    /// <summary>
    /// Owns the narrow quantized coefficients.
    /// </summary>
    private IMemoryOwner<short>? int16Memory;

    /// <summary>
    /// The largest transform area supported by the current workspace.
    /// </summary>
    private int maxBlockArea;

    public Memory<float> DecodeGroupBlockScratchSpace { get; set; }

    public Memory<int> DecodeGroupQBlockScratchSpace { get; set; }

    public Memory<short> DecodeGroupQBlock16ScratchSpace { get; set; }

    /// <summary>
    /// Gets or sets the scratch space for
    /// <see cref="JxlTransformsDecoder.TransformToPixels(JxlAcStrategyType, Span{Single}, Span{Single}, int, Span{Single})"/>.
    /// </summary>
    public Memory<float> ScratchSpaceMemory { get; set; }

    /// <summary>
    /// Gets or sets the number of non-zero coefficients.
    /// </summary>
    public JxlImage3<int>[] NumberOfNonZeroes { get; set; } = ArrayHelpers.CreateAndInitialize(JxlShared.MaximumNumberOfPasses, () => new JxlImage3<int>());

    public JxlPlane<float> DcBuffer { get; set; } = new();

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

    /// <summary>
    /// Initializes pass storage and grows coefficient scratch only when a larger transform is required.
    /// </summary>
    /// <param name="numPasses">The number of coefficient passes.</param>
    /// <param name="usedACStrategy">The bit mask of transform types used by the group.</param>
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
            IMemoryOwner<float> floatMemory = configuration.MemoryAllocator.Allocate<float>(maxBlockArea * 7);
            IMemoryOwner<int>? int32Memory = null;
            IMemoryOwner<short>? int16Memory = null;

            try
            {
                int32Memory = configuration.MemoryAllocator.Allocate<int>(maxBlockArea * 3);
                int16Memory = configuration.MemoryAllocator.Allocate<short>(maxBlockArea * 3);
            }
            catch
            {
                int16Memory?.Dispose();
                int32Memory?.Dispose();
                floatMemory.Dispose();
                throw;
            }

            // Publish the replacement only after every allocation succeeds. Later groups reuse
            // these buffers until a larger transform is needed; growth releases the old owners.
            this.floatMemory?.Dispose();
            this.int32Memory?.Dispose();
            this.int16Memory?.Dispose();
            this.floatMemory = floatMemory;
            this.int32Memory = int32Memory;
            this.int16Memory = int16Memory;
            this.maxBlockArea = maxBlockArea;
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

    /// <summary>
    /// Releases coefficient scratch, nonzero-count planes, and DC storage owned by this cache.
    /// </summary>
    public void Dispose()
    {
        this.floatMemory?.Dispose();
        this.int32Memory?.Dispose();
        this.int16Memory?.Dispose();

        foreach (JxlImage3<int> nonZeroes in this.NumberOfNonZeroes)
        {
            nonZeroes.Dispose();
        }

        this.DcBuffer.Dispose();
    }
}
