// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Defines shared AV1 syntax, geometry, entropy, and transform limits.
/// </summary>
internal static class Av1Constants
{
    /// <summary>
    /// The highest sequence profile defined by AV1.
    /// </summary>
    public const ObuSequenceProfile MaxSequenceProfile = ObuSequenceProfile.Professional;

    /// <summary>
    /// The number of bits used for an operating-point level index.
    /// </summary>
    public const int LevelBits = 5;

    /// <summary>
    /// The maximum number of operating points declared by one AV1 sequence header.
    /// </summary>
    public const int MaxOperatingPointCount = 32;

    /// <summary>
    /// The maximum number of spatial layers identified by an AV1 OBU extension header.
    /// </summary>
    public const int MaxSpatialLayerCount = 4;

    /// <summary>
    /// The number of bits used to signal a super-resolution denominator offset.
    /// </summary>
    public const int SuperResolutionScaleBits = 3;

    /// <summary>
    /// The fixed numerator of the AV1 super-resolution scaling ratio.
    /// </summary>
    public const int ScaleNumerator = 8;

    /// <summary>
    /// The number of reference frames that can be used for inter prediction.
    /// </summary>
    public const int ReferencesPerFrame = 7;

    /// <summary>
    /// The maximum area of a tile in units of luma samples.
    /// </summary>
    public const int MaxTileArea = 4096 * 2304;

    /// <summary>
    /// The maximum width of a tile in units of luma samples.
    /// </summary>
    public const int MaxTileWidth = 4096;

    /// <summary>
    /// The maximum number of tile columns.
    /// </summary>
    public const int MaxTileColumnCount = 64;

    /// <summary>
    /// The maximum number of tile rows.
    /// </summary>
    public const int MaxTileRowCount = 64;

    /// <summary>
    /// The number of frames that can be stored for future reference.
    /// </summary>
    public const int ReferenceFrameCount = 8;

    /// <summary>
    /// The primary-reference-frame value indicating that no primary reference is selected.
    /// </summary>
    public const uint PrimaryReferenceFrameNone = 7;

    /// <summary>
    /// The number of bits used to signal a primary reference frame.
    /// </summary>
    public const int PrimaryReferenceBits = 3;

    /// <summary>
    /// The number of segments allowed in a segmentation map.
    /// </summary>
    public const int MaxSegmentCount = 8;

    /// <summary>
    /// The smallest signaled denominator for an active super-resolution ratio.
    /// </summary>
    public const int SuperResolutionScaleDenominatorMinimum = 9;

    /// <summary>
    /// The base-two logarithm of the maximum superblock size in luma samples.
    /// </summary>
    public const int MaxSuperBlockSizeLog2 = 7;

    /// <summary>
    /// The base-two logarithm of the smallest mode-info block size in luma samples.
    /// </summary>
    public const int ModeInfoSizeLog2 = 2;

    /// <summary>
    /// The maximum quantizer index.
    /// </summary>
    public const int MaxQ = 255;

    /// <summary>
    /// The number of segmentation features.
    /// </summary>
    public const int SegmentationLevelMax = 8;

    /// <summary>
    /// The maximum loop-restoration tile size in samples.
    /// </summary>
    public const int RestorationMaxTileSize = 256;

    /// <summary>
    /// The number of independent Wiener filter coefficients per direction.
    /// </summary>
    public const int WienerCoefficientCount = 3;

    /// <summary>
    /// The number of luma and chroma frame loop-filter levels.
    /// </summary>
    public const int FrameLoopFilterCount = 4;

    /// <summary>
    /// The first quantizer-delta magnitude encoded through the escape path.
    /// </summary>
    public const int DeltaQuantizerSmall = 3;

    /// <summary>
    /// The first loop-filter-delta magnitude encoded through the escape path.
    /// </summary>
    public const int DeltaLoopFilterSmall = 3;

    /// <summary>
    /// The maximum loop-filter strength.
    /// </summary>
    public const int MaxLoopFilter = 63;

    /// <summary>
    /// The maximum directional-prediction angle-delta magnitude.
    /// </summary>
    public const int MaxAngleDelta = 3;

    /// <summary>
    /// The maximum number of color planes.
    /// </summary>
    public const int MaxPlanes = 3;

    /// <summary>
    /// The number of reference-frame types, including the intra type.
    /// </summary>
    public const int TotalReferencesPerFrame = 8;

    /// <summary>
    /// The maximum palette size.
    /// </summary>
    public const int PaletteMaxSize = 8;

    /// <summary>
    /// The number of transform-size probability categories.
    /// </summary>
    public const int MaxTransformCategories = 4;

    /// <summary>
    /// The number of cumulative coefficient-level magnitude contexts.
    /// </summary>
    public const int CoefficientContextCount = 6;

    /// <summary>
    /// The number of coefficient magnitudes represented by base symbols before base-range coding.
    /// </summary>
    public const int BaseLevelsCount = 2;

    /// <summary>
    /// The maximum coefficient magnitude increment represented by base-range symbols.
    /// </summary>
    public const int CoefficientBaseRange = 12;

    /// <summary>
    /// The maximum transform dimension in samples.
    /// </summary>
    public const int MaxTransformSize = 1 << 6;

    /// <summary>
    /// The maximum transform dimension in units of four samples.
    /// </summary>
    public const int MaxTransformSizeUnit = MaxTransformSize >> 2;

    /// <summary>
    /// The number of low-order bits reserved for a cumulative coefficient-level context.
    /// </summary>
    public const int CoefficientContextBitCount = 3;

    /// <summary>
    /// The mask selecting the cumulative coefficient-level magnitude bits.
    /// </summary>
    public const int CoefficientContextMask = (1 << CoefficientContextBitCount) - 1;

    /// <summary>
    /// The base-two logarithm of the horizontal coefficient-context padding.
    /// </summary>
    public const int TransformPadHorizontalLog2 = 2;

    /// <summary>
    /// The horizontal coefficient-context padding in elements.
    /// </summary>
    public const int TransformPadHorizontal = 1 << TransformPadHorizontalLog2;

    /// <summary>
    /// The total vertical coefficient-context padding in rows.
    /// </summary>
    public const int TransformPadVertical = 6;

    /// <summary>
    /// The trailing coefficient-context padding in elements.
    /// </summary>
    public const int TransformPadEnd = 16;

    /// <summary>
    /// The maximum padded two-dimensional coefficient-context allocation size.
    /// </summary>
    public const int TransformPad2d = ((MaxTransformSize + TransformPadHorizontal) * (MaxTransformSize + TransformPadVertical)) + TransformPadEnd;

    /// <summary>
    /// The coefficient-context padding above a transform.
    /// </summary>
    public const int TransformPadTop = 2;

    /// <summary>
    /// The coefficient-context padding below a transform.
    /// </summary>
    public const int TransformPadBottom = 4;

    /// <summary>
    /// The largest symbol in a coefficient base-range distribution.
    /// </summary>
    public const int BaseRangeSizeMinus1 = 3;

    /// <summary>
    /// The largest coefficient magnitude represented before Golomb coding.
    /// </summary>
    public const int MaxBaseRange = 15;

    /// <summary>
    /// The base-two logarithm of the chroma-from-luma alpha alphabet size.
    /// </summary>
    public const int ChromaFromLumaAlphabetSizeLog2 = 4;

    /// <summary>
    /// The number of quantization-matrix levels.
    /// </summary>
    public const int QuantificationMatrixLevelCount = 1 << 4;

    /// <summary>
    /// The fixed-point precision of each quantization-matrix element.
    /// </summary>
    public const int QuantizationMatrixElementBitCount = 5;

    /// <summary>
    /// The directional intra-prediction angle increment in degrees.
    /// </summary>
    public const int AngleStep = 3;

    /// <summary>
    /// The maximum number of stages in a one-dimensional transform function.
    /// </summary>
    public const int MaxTransformStageNumber = 12;

    /// <summary>
    /// The number of partition contexts per block-size logarithm.
    /// </summary>
    public const int PartitionProbabilitySet = 4;

    /// <summary>
    /// The number of square transform-size contexts that can signal extended transforms.
    /// </summary>
    public const int ExtendedTransformCount = 4;

    /// <summary>
    /// The highest variable-transform depth index.
    /// </summary>
    public const int MaxVarTransform = 2;

    /// <summary>
    /// The maximum number of transform blocks at one depth.
    /// </summary>
    public const int MaxTransformBlockCount = 16;

    /// <summary>
    /// Number of items in the <see cref="Av1PlaneType"/> enumeration.
    /// </summary>
    public const int PlaneTypeCount = 2;

    /// <summary>
    /// The maximum number of transform units stored for one encoded block.
    /// </summary>
    public const int MaxTransformUnitCount = 16;

    /// <summary>
    /// Gets the number of payload bits used by each segmentation feature.
    /// </summary>
    public static ReadOnlySpan<int> SegmentationFeatureBits => [8, 6, 6, 6, 6, 3, 0, 0];

    /// <summary>
    /// Gets values indicating whether each segmentation feature is signed.
    /// </summary>
    public static ReadOnlySpan<int> SegmentationFeatureSigned => [1, 1, 1, 1, 1, 0, 0, 0];

    /// <summary>
    /// Gets the maximum magnitude or value permitted for each segmentation feature.
    /// </summary>
    public static ReadOnlySpan<int> SegmentationFeatureMax => [MaxQ, MaxLoopFilter, MaxLoopFilter, MaxLoopFilter, MaxLoopFilter, 7, 0, 0];
}
