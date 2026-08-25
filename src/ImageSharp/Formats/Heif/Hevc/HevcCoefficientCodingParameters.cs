// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the immutable entropy-coding parameters for one HEVC transform block.
/// </summary>
internal readonly struct HevcCoefficientCodingParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcCoefficientCodingParameters"/> struct.
    /// </summary>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="plane">The reconstructed component.</param>
    /// <param name="scanType">The coefficient scan selected for the block.</param>
    /// <param name="useSingleSignificanceContext">Whether transform skip or transquant bypass selects the single significance context.</param>
    /// <param name="signDataHidingEnabled">Whether the first coefficient sign in an eligible group is inferred.</param>
    /// <param name="persistentRiceAdaptationEnabled">Whether Rice parameters adapt across transform blocks.</param>
    /// <param name="cabacBypassAlignmentEnabled">Whether coefficient bypass data is byte aligned when escape data is present.</param>
    /// <param name="extendedPrecisionProcessingEnabled">Whether coefficient remainders use the bounded extended-precision prefix.</param>
    /// <param name="maximumLog2TransformDynamicRange">The component transform dynamic range excluding its sign bit.</param>
    /// <param name="riceStatisticsIndex">The luma/chroma and transformed/non-transformed Rice statistics selector.</param>
    public HevcCoefficientCodingParameters(
        int width,
        int height,
        HevcPlane plane,
        HevcCoefficientScanType scanType,
        bool useSingleSignificanceContext,
        bool signDataHidingEnabled,
        bool persistentRiceAdaptationEnabled,
        bool cabacBypassAlignmentEnabled,
        bool extendedPrecisionProcessingEnabled,
        int maximumLog2TransformDynamicRange,
        int riceStatisticsIndex)
    {
        this.Width = width;
        this.Height = height;
        this.Plane = plane;
        this.ScanType = scanType;
        this.FirstSignificanceMapContext = GetFirstSignificanceMapContext(width, height, plane != HevcPlane.Y, scanType, useSingleSignificanceContext);
        this.SignDataHidingEnabled = signDataHidingEnabled;
        this.PersistentRiceAdaptationEnabled = persistentRiceAdaptationEnabled;
        this.CabacBypassAlignmentEnabled = cabacBypassAlignmentEnabled;
        this.ExtendedPrecisionProcessingEnabled = extendedPrecisionProcessingEnabled;
        this.MaximumLog2TransformDynamicRange = maximumLog2TransformDynamicRange;
        this.RiceStatisticsIndex = riceStatisticsIndex;
    }

    /// <summary>
    /// Gets the transform-block width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the transform-block height.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the reconstructed component.
    /// </summary>
    public HevcPlane Plane { get; }

    /// <summary>
    /// Gets the coefficient scan selected for the block.
    /// </summary>
    public HevcCoefficientScanType ScanType { get; }

    /// <summary>
    /// Gets the first significant-coefficient context within the component context set.
    /// </summary>
    public int FirstSignificanceMapContext { get; }

    /// <summary>
    /// Gets a value indicating whether an eligible first coefficient sign is inferred from the group parity.
    /// </summary>
    public bool SignDataHidingEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether Rice parameters adapt across transform blocks.
    /// </summary>
    public bool PersistentRiceAdaptationEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether coefficient bypass data is byte aligned when escape data is present.
    /// </summary>
    public bool CabacBypassAlignmentEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether coefficient remainders use the bounded extended-precision prefix.
    /// </summary>
    public bool ExtendedPrecisionProcessingEnabled { get; }

    /// <summary>
    /// Gets the component transform dynamic range excluding its sign bit.
    /// </summary>
    public int MaximumLog2TransformDynamicRange { get; }

    /// <summary>
    /// Gets the luma/chroma and transformed/non-transformed Rice statistics selector.
    /// </summary>
    public int RiceStatisticsIndex { get; }

    /// <summary>
    /// Gets the raster-position context mapping for a 4 by 4 transform block.
    /// </summary>
    private static ReadOnlySpan<byte> SignificanceContexts4x4 =>
    [
        0, 1, 4, 5,
        2, 3, 4, 5,
        6, 6, 8, 8,
        7, 7, 8, 8,
    ];

    /// <summary>
    /// Creates the coefficient parameters selected by the active sequence, picture, and transform-unit state.
    /// </summary>
    /// <param name="pictureParameterSet">The active picture parameters.</param>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="plane">The reconstructed component.</param>
    /// <param name="isIntra">Whether the containing coding unit uses intra prediction.</param>
    /// <param name="intraPredictionMode">The effective intra prediction mode, or a value ignored for inter prediction.</param>
    /// <param name="transformSkip">Whether the transform block bypasses the inverse transform.</param>
    /// <param name="transquantBypass">Whether the coding unit bypasses inverse quantization and inverse transform.</param>
    /// <param name="residualDpcmMode">The residual differential-pulse-code-modulation mode selected for the block.</param>
    /// <param name="useLumaSyntax">Whether a separately coded color plane uses the luma coefficient context set.</param>
    /// <returns>The coefficient entropy-coding parameters for the transform block.</returns>
    public static HevcCoefficientCodingParameters Create(
        HevcPictureParameterSet pictureParameterSet,
        int width,
        int height,
        HevcPlane plane,
        bool isIntra,
        int intraPredictionMode,
        bool transformSkip,
        bool transquantBypass,
        HevcResidualDpcmMode residualDpcmMode,
        bool useLumaSyntax = false)
    {
        HevcSequenceParameterSet sequenceParameterSet = pictureParameterSet.SequenceParameterSet;
        HevcPlane codingPlane = useLumaSyntax ? HevcPlane.Y : plane;
        bool isChroma = codingPlane != HevcPlane.Y;
        bool nonTransformed = transformSkip || transquantBypass;
        HevcCoefficientScanType scanType = SelectScanType(
            width,
            height,
            codingPlane,
            isIntra,
            intraPredictionMode,
            sequenceParameterSet.ChromaFormat,
            sequenceParameterSet.SeparateColorPlaneFlag);

        return new HevcCoefficientCodingParameters(
            width,
            height,
            plane,
            scanType,
            sequenceParameterSet.TransformSkipContextEnabled && nonTransformed,
            pictureParameterSet.SignDataHidingEnabled && !transquantBypass && residualDpcmMode == HevcResidualDpcmMode.None,
            sequenceParameterSet.PersistentRiceAdaptationEnabled,
            sequenceParameterSet.CabacBypassAlignmentEnabled,
            sequenceParameterSet.ExtendedPrecisionProcessingEnabled,
            sequenceParameterSet.GetMaxTransformDynamicRange(plane),
            (isChroma ? 2 : 0) + (nonTransformed ? 1 : 0));
    }

    /// <summary>
    /// Selects the scan direction from transform geometry and the effective intra prediction direction.
    /// </summary>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="plane">The reconstructed component.</param>
    /// <param name="isIntra">Whether the containing coding unit uses intra prediction.</param>
    /// <param name="intraPredictionMode">The effective intra prediction mode.</param>
    /// <param name="chromaFormat">The sequence chroma-format identifier.</param>
    /// <param name="separateColorPlane">Whether each 4:4:4 component is coded as an independent color plane.</param>
    /// <returns>The selected coefficient scan.</returns>
    public static HevcCoefficientScanType SelectScanType(
        int width,
        int height,
        HevcPlane plane,
        bool isIntra,
        int intraPredictionMode,
        byte chromaFormat,
        bool separateColorPlane)
    {
        if (!isIntra)
        {
            return HevcCoefficientScanType.Diagonal;
        }

        bool isSubsampledChroma = plane != HevcPlane.Y && !separateColorPlane;
        int subsamplingX = isSubsampledChroma && chromaFormat is 1 or 2 ? 1 : 0;
        int subsamplingY = isSubsampledChroma && chromaFormat == 1 ? 1 : 0;
        if (width > (8 >> subsamplingX) || height > (8 >> subsamplingY))
        {
            return HevcCoefficientScanType.Diagonal;
        }

        int mode = plane != HevcPlane.Y && chromaFormat == 2 && !separateColorPlane
            ? HevcIntraPredictionMode.RemapChroma422(intraPredictionMode)
            : intraPredictionMode;

        // Modes close to vertical place correlated residuals along rows, while modes close to horizontal use the
        // transposed column scan. All other modes retain the diagonal scan.
        if (Math.Abs(mode - HevcIntraPredictionMode.Vertical) <= 4)
        {
            return HevcCoefficientScanType.Horizontal;
        }

        return Math.Abs(mode - HevcIntraPredictionMode.Horizontal) <= 4
            ? HevcCoefficientScanType.Vertical
            : HevcCoefficientScanType.Diagonal;
    }

    /// <summary>
    /// Derives the coded-sub-block significance context from already decoded right and lower groups.
    /// </summary>
    /// <param name="groupFlags">The raster-ordered significant-group flags.</param>
    /// <param name="groupX">The current group horizontal coordinate.</param>
    /// <param name="groupY">The current group vertical coordinate.</param>
    /// <returns>Zero when neither neighbor is significant; otherwise, one.</returns>
    public int GetSignificantGroupContext(ReadOnlySpan<int> groupFlags, int groupX, int groupY)
    {
        int widthInGroups = this.Width / 4;
        int heightInGroups = this.Height / 4;
        bool rightSignificant = groupX < widthInGroups - 1 && groupFlags[(groupY * widthInGroups) + groupX + 1] != 0;
        bool lowerSignificant = groupY < heightInGroups - 1 && groupFlags[((groupY + 1) * widthInGroups) + groupX] != 0;
        return rightSignificant || lowerSignificant ? 1 : 0;
    }

    /// <summary>
    /// Derives the two-bit right-and-lower significance pattern for coefficient contexts.
    /// </summary>
    /// <param name="groupFlags">The raster-ordered significant-group flags.</param>
    /// <param name="groupX">The current group horizontal coordinate.</param>
    /// <param name="groupY">The current group vertical coordinate.</param>
    /// <returns>The right flag in bit zero and the lower flag in bit one.</returns>
    public int GetSignificancePattern(ReadOnlySpan<int> groupFlags, int groupX, int groupY)
    {
        int widthInGroups = this.Width / 4;
        int heightInGroups = this.Height / 4;
        int right = groupX < widthInGroups - 1 && groupFlags[(groupY * widthInGroups) + groupX + 1] != 0 ? 1 : 0;
        int lower = groupY < heightInGroups - 1 && groupFlags[((groupY + 1) * widthInGroups) + groupX] != 0 ? 1 : 0;
        return right + (lower << 1);
    }

    /// <summary>
    /// Derives the significant-coefficient context from its position and neighboring coefficient groups.
    /// </summary>
    /// <param name="rasterPosition">The coefficient raster position.</param>
    /// <param name="significancePattern">The right-and-lower significant-group pattern.</param>
    /// <returns>The context index within the component significance-map context set.</returns>
    public int GetSignificantCoefficientContext(int rasterPosition, int significancePattern)
    {
        bool isChroma = this.Plane != HevcPlane.Y;
        if (this.FirstSignificanceMapContext == (isChroma ? 15 : 27))
        {
            return this.FirstSignificanceMapContext;
        }

        int y = rasterPosition / this.Width;
        int x = rasterPosition - (y * this.Width);
        if (x + y == 0)
        {
            return 0;
        }

        if (this.Width == 4 && this.Height == 4)
        {
            return SignificanceContexts4x4[(y * 4) + x];
        }

        int context;
        switch (significancePattern)
        {
            case 0:
                int positionInGroup = (x & 3) + (y & 3);
                context = positionInGroup >= 3 ? 0 : positionInGroup >= 1 ? 1 : 2;
                break;
            case 1:
                int yInGroup = y & 3;
                context = yInGroup >= 2 ? 0 : yInGroup >= 1 ? 1 : 2;
                break;
            case 2:
                int xInGroup = x & 3;
                context = xInGroup >= 2 ? 0 : xInGroup >= 1 ? 1 : 2;
                break;
            default:
                context = 2;
                break;
        }

        bool isBeyondFirstGroup = (x >> 2) + (y >> 2) > 0;
        return this.FirstSignificanceMapContext + (isBeyondFirstGroup && !isChroma ? 3 : 0) + context;
    }

    /// <summary>
    /// Selects the greater-than-one and greater-than-two context set for one coefficient group.
    /// </summary>
    /// <param name="subset">The coefficient-group scan index.</param>
    /// <param name="foundGreaterThanOne">Whether the preceding group ended after finding a coefficient greater than one.</param>
    /// <returns>The zero-based context set within the component context range.</returns>
    public int GetLevelContextSet(int subset, bool foundGreaterThanOne)
    {
        int nonFirstSubsetOffset = this.Plane == HevcPlane.Y && subset > 0 ? 2 : 0;
        return nonFirstSubsetOffset + (foundGreaterThanOne ? 1 : 0);
    }

    /// <summary>
    /// Derives the first significant-coefficient context within one component context set.
    /// </summary>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="isChroma">Whether the transform block belongs to a chroma channel.</param>
    /// <param name="scanType">The selected coefficient scan.</param>
    /// <param name="useSingleSignificanceContext">Whether Range Extensions selects the single-context mode.</param>
    /// <returns>The first significant-coefficient context index.</returns>
    private static int GetFirstSignificanceMapContext(
        int width,
        int height,
        bool isChroma,
        HevcCoefficientScanType scanType,
        bool useSingleSignificanceContext)
    {
        if (useSingleSignificanceContext)
        {
            return isChroma ? 15 : 27;
        }

        if (width == 4 && height == 4)
        {
            return 0;
        }

        if (width == 8 && height == 8)
        {
            return isChroma ? 9 : scanType == HevcCoefficientScanType.Diagonal ? 9 : 15;
        }

        return isChroma ? 12 : 21;
    }
}
