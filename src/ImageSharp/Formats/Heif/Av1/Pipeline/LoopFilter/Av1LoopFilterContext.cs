// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

/// <summary>
/// Stores the transform-size map consumed by the AV1 deblocking loop filter.
/// </summary>
internal class Av1LoopFilterContext
{
    /// <summary>
    /// Stores luma and shared-chroma transform sizes at plane-relative 4x4 granularity.
    /// </summary>
    private readonly Av1TransformSize[][] transformSizes = new Av1TransformSize[2][];

    /// <summary>
    /// Stores the row stride of each transform-size map.
    /// </summary>
    private readonly int[] transformSizeStrides = new int[2];

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LoopFilterContext"/> class.
    /// </summary>
    /// <param name="sequenceHeader">The sequence header defining aligned frame and chroma dimensions.</param>
    public Av1LoopFilterContext(ObuSequenceHeader sequenceHeader)
    {
        int alignedModeInfoWidth = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameWidth, sequenceHeader.SuperblockSizeLog2) >>
            Av1Constants.ModeInfoSizeLog2;

        int alignedModeInfoHeight = Av1Math.AlignPowerOf2(sequenceHeader.MaxFrameHeight, sequenceHeader.SuperblockSizeLog2) >>
            Av1Constants.ModeInfoSizeLog2;

        this.transformSizeStrides[(int)Av1PlaneType.Y] = alignedModeInfoWidth;
        this.transformSizes[(int)Av1PlaneType.Y] = new Av1TransformSize[alignedModeInfoWidth * alignedModeInfoHeight];

        if (!sequenceHeader.ColorConfig.IsMonochrome)
        {
            int subX = sequenceHeader.ColorConfig.SubSamplingX ? 1 : 0;
            int subY = sequenceHeader.ColorConfig.SubSamplingY ? 1 : 0;
            int chromaWidth = Av1Math.DivideLog2Ceiling(alignedModeInfoWidth, subX);
            int chromaHeight = Av1Math.DivideLog2Ceiling(alignedModeInfoHeight, subY);

            this.transformSizeStrides[(int)Av1PlaneType.Uv] = chromaWidth;
            this.transformSizes[(int)Av1PlaneType.Uv] = new Av1TransformSize[chromaWidth * chromaHeight];
        }
        else
        {
            this.transformSizes[(int)Av1PlaneType.Uv] = [];
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
        Span<Av1TransformSize> transformSizeMap = this.transformSizes[planeType];
        int stride = this.transformSizeStrides[planeType];
        int width = transformSize.Get4x4WideCount();
        int height = transformSize.Get4x4HighCount();

        // Loop filtering addresses every covered 4x4 position, not only the transform origin. Replication keeps
        // edge lookup independent of the transform traversal order used while reconstructing the coded block.
        for (int y = 0; y < height; y++)
        {
            transformSizeMap.Slice(((position.Y + y) * stride) + position.X, width).Fill(transformSize);
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
        int stride = this.transformSizeStrides[planeType];
        return this.transformSizes[planeType][(position.Y * stride) + position.X];
    }
}
