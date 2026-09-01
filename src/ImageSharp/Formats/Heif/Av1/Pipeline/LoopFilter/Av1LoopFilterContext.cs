// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

/// <summary>
/// Stores the transform-size map consumed by the AV1 deblocking loop filter.
/// </summary>
internal sealed class Av1LoopFilterContext : IDisposable
{
    /// <summary>
    /// Stores luma transform sizes at plane-relative 4x4 granularity.
    /// </summary>
    private readonly MemoryGroup<Av1TransformSize> transformSizesY;

    /// <summary>
    /// The active luma transform-map dimensions in plane-relative 4x4 units.
    /// </summary>
    private readonly Size transformSizesYSize;

    /// <summary>
    /// Stores shared-chroma transform sizes at plane-relative 4x4 granularity.
    /// </summary>
    private readonly MemoryGroup<Av1TransformSize>? transformSizesUv;

    /// <summary>
    /// The active shared-chroma transform-map dimensions in plane-relative 4x4 units.
    /// </summary>
    private readonly Size transformSizesUvSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LoopFilterContext"/> class.
    /// </summary>
    /// <param name="memoryAllocator">The allocator that owns the frame-sized transform maps.</param>
    /// <param name="sequenceHeader">The sequence header defining superblock and chroma geometry.</param>
    /// <param name="frameHeader">The frame header defining active coded dimensions.</param>
    public Av1LoopFilterContext(
        MemoryAllocator memoryAllocator,
        ObuSequenceHeader sequenceHeader,
        ObuFrameHeader frameHeader)
    {
        int modeInfoWidth = frameHeader.ModeInfoColumnCount;
        int modeInfoHeight = frameHeader.ModeInfoRowCount;
        MemoryGroup<Av1TransformSize>? transformSizesY = null;
        MemoryGroup<Av1TransformSize>? transformSizesUv = null;
        this.transformSizesUvSize = default;

        try
        {
            long lumaLength = (long)modeInfoWidth * modeInfoHeight;
            transformSizesY = memoryAllocator.AllocateGroup<Av1TransformSize>(
                lumaLength,
                1,
                AllocationOptions.Clean);

            if (!sequenceHeader.ColorConfig.IsMonochrome)
            {
                int subX = sequenceHeader.ColorConfig.SubSamplingX ? 1 : 0;
                int subY = sequenceHeader.ColorConfig.SubSamplingY ? 1 : 0;
                int chromaWidth = Av1Math.DivideLog2Ceiling(modeInfoWidth, subX);
                int chromaHeight = Av1Math.DivideLog2Ceiling(modeInfoHeight, subY);
                long chromaLength = (long)chromaWidth * chromaHeight;
                transformSizesUv = memoryAllocator.AllocateGroup<Av1TransformSize>(
                    chromaLength,
                    1,
                    AllocationOptions.Clean);
                this.transformSizesUvSize = new Size(chromaWidth, chromaHeight);
            }

            this.transformSizesY = transformSizesY;
            this.transformSizesYSize = new Size(modeInfoWidth, modeInfoHeight);
            this.transformSizesUv = transformSizesUv;
        }
        catch
        {
            transformSizesUv?.Dispose();
            transformSizesY?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Stores a transform size across every 4x4 position covered by one transform block.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="position">The transform origin in plane-relative 4x4 units.</param>
    /// <param name="transformSize">The transform size.</param>
    public void SetTransformSize(Av1Plane plane, Point position, Av1TransformSize transformSize)
    {
        int planeType = Math.Min((int)plane, (int)Av1PlaneType.Uv);
        MemoryGroup<Av1TransformSize> transformSizeMap;
        Size transformSizeMapSize;
        if (planeType == (int)Av1PlaneType.Y)
        {
            transformSizeMap = this.transformSizesY;
            transformSizeMapSize = this.transformSizesYSize;
        }
        else
        {
            transformSizeMap = this.transformSizesUv
                ?? throw new InvalidOperationException("A monochrome AV1 frame has no chroma transform-size map.");

            transformSizeMapSize = this.transformSizesUvSize;
        }

        int width = Math.Min(transformSize.Get4x4WideCount(), transformSizeMapSize.Width - position.X);
        int height = Math.Min(transformSize.Get4x4HighCount(), transformSizeMapSize.Height - position.Y);

        // libaom clips transform coverage to the active plane mi dimensions at frame edges. Each logical row may cross
        // allocator segments, so fill only the current segment before continuing at the same logical map offset.
        for (int y = 0; y < height; y++)
        {
            long offset = ((long)(position.Y + y) * transformSizeMapSize.Width) + position.X;
            int remaining = width;
            while (remaining > 0)
            {
                Span<Av1TransformSize> destination = transformSizeMap.GetRemainingSliceOfBuffer(offset);
                int count = Math.Min(remaining, destination.Length);
                destination[..count].Fill(transformSize);
                offset += count;
                remaining -= count;
            }
        }
    }

    /// <summary>
    /// Gets the transform size covering a plane-relative 4x4 position.
    /// </summary>
    /// <param name="plane">The luma or chroma plane.</param>
    /// <param name="position">The position in plane-relative 4x4 units.</param>
    /// <returns>The transform size covering the position.</returns>
    public Av1TransformSize GetTransformSize(Av1Plane plane, Point position)
    {
        int planeType = Math.Min((int)plane, (int)Av1PlaneType.Uv);
        MemoryGroup<Av1TransformSize> transformSizeMap;
        int width;
        if (planeType == (int)Av1PlaneType.Y)
        {
            transformSizeMap = this.transformSizesY;
            width = this.transformSizesYSize.Width;
        }
        else
        {
            transformSizeMap = this.transformSizesUv
                ?? throw new InvalidOperationException("A monochrome AV1 frame has no chroma transform-size map.");

            width = this.transformSizesUvSize.Width;
        }

        long offset = ((long)position.Y * width) + position.X;
        return transformSizeMap.GetRemainingSliceOfBuffer(offset)[0];
    }

    /// <summary>
    /// Returns the allocator-owned transform-size maps.
    /// </summary>
    public void Dispose()
    {
        this.transformSizesUv?.Dispose();
        this.transformSizesY.Dispose();
    }
}
