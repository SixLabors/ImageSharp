// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores block-size, intra/inter prediction, transform, and palette decisions shared by AV1 block processing.
/// </summary>
internal struct Av1BlockModeInfo
{
    /// <summary>
    /// Stores the primary and optional secondary reference-frame labels.
    /// </summary>
    private InlineArray2<Av1ReferenceFrameType> referenceFrames;

    /// <summary>
    /// Stores variable luma transform sizes on the coding block's compact transform grid.
    /// </summary>
    private InlineArray16<Av1TransformSize> interTransformSizes;

    /// <summary>
    /// Stores the motion vector associated with each reference-frame label.
    /// </summary>
    private InlineArray2<Av1MotionVector> motionVectors;

    /// <summary>
    /// Stores the vertical and horizontal subpixel interpolation filters in that order.
    /// </summary>
    private InlineArray2<Av1InterpolationFilter> interpolationFilters;

    /// <summary>
    /// The palette size for the luma plane.
    /// </summary>
    private int lumaPaletteSize;

    /// <summary>
    /// The palette size shared by both chroma planes.
    /// </summary>
    private int chromaPaletteSize;

    /// <summary>
    /// Stores the decoded luma palette colors.
    /// </summary>
    private InlineArray8<ushort> lumaPaletteColors;

    /// <summary>
    /// Stores the decoded blue-difference chroma palette colors.
    /// </summary>
    private InlineArray8<ushort> chromaBluePaletteColors;

    /// <summary>
    /// Stores the decoded red-difference chroma palette colors.
    /// </summary>
    private InlineArray8<ushort> chromaRedPaletteColors;

    /// <summary>
    /// Stores the luma palette color-index map.
    /// </summary>
    private Rectangle lumaPaletteColorIndexBounds;

    /// <summary>
    /// Stores the shared chroma palette color-index map.
    /// </summary>
    private Rectangle chromaPaletteColorIndexBounds;

    /// <summary>
    /// The directional prediction angle adjustment for luma.
    /// </summary>
    private int lumaAngleDelta;

    /// <summary>
    /// The directional prediction angle adjustment shared by both chroma planes.
    /// </summary>
    private int chromaAngleDelta;

    /// <summary>
    /// The plane-relative index of the first luma transform.
    /// </summary>
    private int firstLumaTransformLocation;

    /// <summary>
    /// The plane-relative index of the first chroma transform.
    /// </summary>
    private int firstChromaTransformLocation;

    /// <summary>
    /// The number of luma transform units.
    /// </summary>
    private int lumaTransformUnitCount;

    /// <summary>
    /// The number of transform units for one chroma plane.
    /// </summary>
    private int chromaTransformUnitCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1BlockModeInfo"/> structure.
    /// </summary>
    /// <param name="blockSize">The decoded block size.</param>
    /// <param name="positionInSuperblock">The block origin relative to its superblock in 4x4 mode-information units.</param>
    public Av1BlockModeInfo(Av1BlockSize blockSize, Point positionInSuperblock)
    {
        this.BlockSize = blockSize;
        this.PositionInSuperblock = positionInSuperblock;

        // Both entries begin absent because inter syntax has not selected either reference yet. Intra parsing replaces
        // the primary entry with the current frame while retaining None as the optional secondary reference.
        this.referenceFrames[0] = Av1ReferenceFrameType.None;
        this.referenceFrames[1] = Av1ReferenceFrameType.None;
    }

    /// <summary>
    /// Gets the decoded block size.
    /// </summary>
    public Av1BlockSize BlockSize { get; }

    /// <summary>
    /// Gets or sets the frame storage index shared by every mode-information position covered by this block.
    /// </summary>
    public int ModeInfoIndex { get; set; }

    /// <summary>
    /// Gets or sets the selected luma transform size.
    /// </summary>
    public Av1TransformSize TransformSize { get; set; }

    /// <summary>
    /// Gets the variable luma transform sizes retained with this coding block.
    /// </summary>
    [UnscopedRef]
    public Span<Av1TransformSize> InterTransformSizes => this.interTransformSizes;

    /// <summary>
    /// Gets or sets the <see cref="Av1PredictionMode"/> for the luminance channel.
    /// </summary>
    public Av1PredictionMode YMode { get; set; }

    /// <summary>
    /// Gets the primary and optional secondary reference-frame labels.
    /// </summary>
    /// <remarks>
    /// Index zero is the primary reference. Index one is <see cref="Av1ReferenceFrameType.None"/> for a single-reference
    /// block, <see cref="Av1ReferenceFrameType.Intra"/> for an inter-intra block, or the secondary inter-reference label
    /// for compound prediction.
    /// </remarks>
    [UnscopedRef]
    public Span<Av1ReferenceFrameType> ReferenceFrames => this.referenceFrames;

    /// <summary>
    /// Gets the decoded motion vectors corresponding to <see cref="ReferenceFrames"/>.
    /// </summary>
    [UnscopedRef]
    public Span<Av1MotionVector> MotionVectors => this.motionVectors;

    /// <summary>
    /// Gets the interpolation filters used for vertical and horizontal subpixel prediction.
    /// </summary>
    /// <remarks>
    /// Index zero is the vertical filter and index one is the horizontal filter, matching the reference decoder's
    /// <c>InterpFilters.y_filter</c> and <c>InterpFilters.x_filter</c> layout.
    /// </remarks>
    [UnscopedRef]
    public Span<Av1InterpolationFilter> InterpolationFilters => this.interpolationFilters;

    /// <summary>
    /// Gets or sets the selected index in the derived reference-motion-vector stack.
    /// </summary>
    /// <remarks>
    /// The AV1 syntax constrains this value to the inclusive range zero through two.
    /// </remarks>
    public byte ReferenceMotionVectorIndex { get; set; }

    /// <summary>
    /// Gets or sets the motion model used to construct inter prediction.
    /// </summary>
    public Av1MotionMode MotionMode { get; set; }

    /// <summary>
    /// Gets or sets the affine model derived for local warped prediction.
    /// </summary>
    public Av1GlobalMotionParameters WarpedMotionParameters { get; set; }

    /// <summary>
    /// Gets or sets the intra predictor blended with a single-reference inter predictor.
    /// </summary>
    public Av1InterIntraMode InterIntraMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether inter-intra prediction uses a wedge mask.
    /// </summary>
    public bool UseInterIntraWedge { get; set; }

    /// <summary>
    /// Gets or sets the inter-intra wedge-mask index in the inclusive range 0 through 15.
    /// </summary>
    public byte InterIntraWedgeIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether compound prediction uses the masked-compound mode group.
    /// </summary>
    public bool CompoundGroupIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether unmasked compound prediction uses average blending.
    /// A value of <see langword="false"/> selects distance-weighted blending.
    /// </summary>
    public bool CompoundIndex { get; set; }

    /// <summary>
    /// Gets or sets the compound blending method selected for two inter predictors.
    /// </summary>
    public Av1CompoundType CompoundType { get; set; }

    /// <summary>
    /// Gets or sets the compound wedge-mask index in the inclusive range 0 through 15.
    /// </summary>
    public byte CompoundWedgeIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the compound wedge mask is inverted.
    /// </summary>
    public bool CompoundWedgeSign { get; set; }

    /// <summary>
    /// Gets or sets the orientation of the difference-weighted compound mask.
    /// </summary>
    public Av1DifferenceWeightedMaskType DifferenceWeightedMaskType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether residual coefficients are omitted for the block.
    /// </summary>
    public bool Skip { get; set; }

    /// <summary>
    /// Gets or sets the partition type that produced the block.
    /// </summary>
    public Av1PartitionType PartitionType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether compound skip mode is selected.
    /// </summary>
    public bool SkipMode { get; set; }

    /// <summary>
    /// Gets or sets the segmentation identifier assigned to the block.
    /// </summary>
    public int SegmentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether temporal prediction supplied the segment identifier.
    /// </summary>
    public bool SegmentIdPredicted { get; set; }

    /// <summary>
    /// Gets or sets the chroma intra-prediction mode.
    /// </summary>
    public Av1ChromaPredictionMode UvMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether intra block copy is selected.
    /// </summary>
    public bool UseIntraBlockCopy { get; set; }

    /// <summary>
    /// Gets or sets the intra-block-copy displacement vector in one-eighth-sample units.
    /// </summary>
    public Av1MotionVector DisplacementVector { get; set; }

    /// <summary>
    /// Gets or sets the packed chroma-from-luma alpha magnitude indices.
    /// </summary>
    public int ChromaFromLumaAlphaIndex { get; set; }

    /// <summary>
    /// Gets or sets the joint chroma-from-luma alpha sign value.
    /// </summary>
    public int ChromaFromLumaAlphaSign { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether filter-intra prediction is enabled for the block.
    /// </summary>
    public bool UseFilterIntra { get; set; }

    /// <summary>
    /// Gets the position relative to the superblock in 4x4 mode-information units.
    /// </summary>
    public Point PositionInSuperblock { get; }

    /// <summary>
    /// Gets or sets the filter-intra mode selected for the block.
    /// </summary>
    public Av1FilterIntraMode FilterIntraMode { get; set; }

    /// <summary>
    /// Finds the compact transform-grid entry covering a position relative to the coding block.
    /// </summary>
    /// <param name="row">The row in luma 4x4 units.</param>
    /// <param name="column">The column in luma 4x4 units.</param>
    /// <returns>The entry index in the block's sixteen-element transform grid.</returns>
    public int GetInterTransformSizeIndex(int row, int column)
    {
        // One subdivision of the maximum transform defines the storage cell. A final split into
        // 4x4 transforms shares one entry because all children of that split have the same size.
        Av1TransformSize cellSize = this.BlockSize.GetMaximumTransformSize().GetSubSize();
        int cellWidth = cellSize.Get4x4WideCount();
        int cellHeight = cellSize.Get4x4HighCount();
        int stride = this.BlockSize.Get4x4WideCount() / cellWidth;
        return ((row / cellHeight) * stride) + (column / cellWidth);
    }

    /// <summary>
    /// Gets the directional prediction angle adjustment for a color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <returns>The luma adjustment or the adjustment shared by both chroma planes.</returns>
    public int GetAngleDelta(Av1Plane plane) => plane == Av1Plane.Y ? this.lumaAngleDelta : this.chromaAngleDelta;

    /// <summary>
    /// Sets the directional prediction angle adjustment for a plane class.
    /// </summary>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <param name="value">The directional prediction angle adjustment.</param>
    public void SetAngleDelta(Av1PlaneType planeType, int value)
    {
        if (planeType == Av1PlaneType.Y)
        {
            this.lumaAngleDelta = value;
        }
        else
        {
            this.chromaAngleDelta = value;
        }
    }

    /// <summary>
    /// Gets the plane-relative index of the first transform for a color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <returns>The first transform index for luma or the selected chroma plane.</returns>
    public int GetFirstTransformLocation(Av1Plane plane)
        => plane == Av1Plane.Y ? this.firstLumaTransformLocation : this.firstChromaTransformLocation;

    /// <summary>
    /// Gets the plane-relative index of the first transform for a plane class.
    /// </summary>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <returns>The first transform index for the plane class.</returns>
    public int GetFirstTransformLocation(Av1PlaneType planeType)
        => planeType == Av1PlaneType.Y ? this.firstLumaTransformLocation : this.firstChromaTransformLocation;

    /// <summary>
    /// Sets the plane-relative index of the first transform for a plane class.
    /// </summary>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <param name="value">The first transform index.</param>
    public void SetFirstTransformLocation(Av1PlaneType planeType, int value)
    {
        if (planeType == Av1PlaneType.Y)
        {
            this.firstLumaTransformLocation = value;
        }
        else
        {
            this.firstChromaTransformLocation = value;
        }
    }

    /// <summary>
    /// Gets the number of transform units for a color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <returns>The luma count or the count for one chroma plane.</returns>
    public int GetTransformUnitCount(Av1Plane plane)
        => plane == Av1Plane.Y ? this.lumaTransformUnitCount : this.chromaTransformUnitCount;

    /// <summary>
    /// Gets the number of transform units for a plane class.
    /// </summary>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <returns>The transform-unit count for the plane class.</returns>
    public int GetTransformUnitCount(Av1PlaneType planeType)
        => planeType == Av1PlaneType.Y ? this.lumaTransformUnitCount : this.chromaTransformUnitCount;

    /// <summary>
    /// Sets the number of transform units for a plane class.
    /// </summary>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <param name="value">The transform-unit count.</param>
    public void SetTransformUnitCount(Av1PlaneType planeType, int value)
    {
        if (planeType == Av1PlaneType.Y)
        {
            this.lumaTransformUnitCount = value;
        }
        else
        {
            this.chromaTransformUnitCount = value;
        }
    }

    /// <summary>
    /// Gets the palette size for the specified color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <returns>The palette size for the plane.</returns>
    public int GetPaletteSize(Av1Plane plane) => plane == Av1Plane.Y ? this.lumaPaletteSize : this.chromaPaletteSize;

    /// <summary>
    /// Gets the palette size for the specified plane class.
    /// </summary>
    /// <param name="planeType">The luma or chroma plane class.</param>
    /// <returns>The palette size for the plane class.</returns>
    public int GetPaletteSize(Av1PlaneType planeType) => planeType == Av1PlaneType.Y ? this.lumaPaletteSize : this.chromaPaletteSize;

    /// <summary>
    /// Sets the luma and shared chroma palette sizes.
    /// </summary>
    /// <param name="ySize">The luma palette size.</param>
    /// <param name="uvSize">The palette size shared by the chroma planes.</param>
    public void SetPaletteSizes(int ySize, int uvSize)
    {
        this.lumaPaletteSize = ySize;
        this.chromaPaletteSize = uvSize;
    }

    /// <summary>
    /// Gets the decoded palette colors for a color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <returns>The palette colors in prediction-index order.</returns>
    [UnscopedRef]
    public ReadOnlySpan<ushort> GetPaletteColors(Av1Plane plane)
    {
        if (plane == Av1Plane.Y)
        {
            return this.lumaPaletteColors[..this.lumaPaletteSize];
        }

        return plane == Av1Plane.U
            ? this.chromaBluePaletteColors[..this.chromaPaletteSize]
            : this.chromaRedPaletteColors[..this.chromaPaletteSize];
    }

    /// <summary>
    /// Stores the decoded palette colors for a color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <param name="colors">The palette colors in prediction-index order.</param>
    public void SetPaletteColors(Av1Plane plane, ReadOnlySpan<ushort> colors)
    {
        if (plane == Av1Plane.Y)
        {
            colors.CopyTo(this.lumaPaletteColors);
        }
        else if (plane == Av1Plane.U)
        {
            colors.CopyTo(this.chromaBluePaletteColors);
        }
        else
        {
            colors.CopyTo(this.chromaRedPaletteColors);
        }
    }

    /// <summary>
    /// Gets the palette color-index map for a color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <param name="colorIndexMap">The decoder-session palette map for the selected plane class.</param>
    /// <returns>The luma map for <see cref="Av1Plane.Y"/> or the shared chroma map for either chroma plane.</returns>
    public Buffer2DRegion<byte> GetPaletteColorIndexMap(Av1Plane plane, Buffer2D<byte> colorIndexMap)
        => new(
            colorIndexMap,
            plane == Av1Plane.Y ? this.lumaPaletteColorIndexBounds : this.chromaPaletteColorIndexBounds);

    /// <summary>
    /// Stores the palette color-index map for a plane class.
    /// </summary>
    /// <param name="planeType">The luma or shared chroma plane class.</param>
    /// <param name="bounds">The row-major color-index bounds including coded-block edge padding.</param>
    public void SetPaletteColorIndexMap(Av1PlaneType planeType, Rectangle bounds)
    {
        if (planeType == Av1PlaneType.Y)
        {
            this.lumaPaletteColorIndexBounds = bounds;
        }
        else
        {
            this.chromaPaletteColorIndexBounds = bounds;
        }
    }
}
