// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal static class Av1Constants
{
    public const ObuSequenceProfile MaxSequenceProfile = ObuSequenceProfile.Professional;

    public const int LevelBits = 5;

    /// <summary>
    /// Number of fractional bits for computing position in upscaling.
    /// </summary>
    public const int SuperResolutionScaleBits = 14;

    public const int ScaleNumerator = -1;

    /// <summary>
    /// Number of reference frames that can be used for inter prediction.
    /// </summary>
    public const int ReferencesPerFrame = 7;

    /// <summary>
    /// Maximum area of a tile in units of luma samples.
    /// </summary>
    public const int MaxTileArea = 4096 * 2304;

    /// <summary>
    /// Maximum width of a tile in units of luma samples.
    /// </summary>
    public const int MaxTileWidth = 4096;

    /// <summary>
    /// Maximum number of tile columns.
    /// </summary>
    public const int MaxTileColumnCount = 64;

    /// <summary>
    /// Maximum number of tile rows.
    /// </summary>
    public const int MaxTileRowCount = 64;

    /// <summary>
    /// Number of frames that can be stored for future reference.
    /// </summary>
    public const int ReferenceFrameCount = 8;

    /// <summary>
    /// Value of 'PrimaryReferenceFrame' indicating that there is no primary reference frame.
    /// </summary>
    public const uint PrimaryReferenceFrameNone = 7;

    public const int PimaryReferenceBits = 3;

    /// <summary>
    /// Number of segments allowed in segmentation map.
    /// </summary>
    public const int MaxSegmentCount = 8;

    /// <summary>
    /// Smallest denominator for upscaling ratio.
    /// </summary>
    public const int SuperResolutionScaleDenominatorMinimum = 9;

    /// <summary>
    /// Base 2 logarithm of maximum size of a superblock in luma samples.
    /// </summary>
    public const int MaxSuperBlockSizeLog2 = 7;

    /// <summary>
    /// Base 2 logarithm of smallest size of a mode info block.
    /// </summary>
    public const int ModeInfoSizeLog2 = 2;

    public const int MaxQ = 255;

    /// <summary>
    /// Number of segmentation features.
    /// </summary>
    public const int SegmentationLevelMax = 8;

    /// <summary>
    /// Maximum size of a loop restoration tile.
    /// </summary>
    public const int RestorationMaxTileSize = 256;

    /// <summary>
    /// Number of Wiener coefficients to read.
    /// </summary>
    public const int WienerCoefficientCount = 3;

    public const int FrameLoopFilterCount = 4;

    /// <summary>
    /// Value indicating alternative encoding of quantizer index delta values.
    /// </summary>
    public const int DeltaQuantizerSmall = 3;

    /// <summary>
    /// Value indicating alternative encoding of loop filter delta values.
    /// </summary>
    public const int DeltaLoopFilterSmall = 3;

    /// <summary>
    /// Maximum value used for loop filtering.
    /// </summary>
    public const int MaxLoopFilter = 63;

    /// <summary>
    /// Maximum magnitude of AngleDeltaY and AngleDeltaUV.
    /// </summary>
    public const int MaxAngleDelta = 3;

    /// <summary>
    /// Maximum number of color planes.
    /// </summary>
    public const int MaxPlanes = 3;

    /// <summary>
    /// Number of reference frame types (including intra type).
    /// </summary>
    public const int TotalReferencesPerFrame = 8;

    /// <summary>
    /// Number of values for palette_size.
    /// </summary>
    public const int PaletteMaxSize = 8;

    /// <summary>
    /// Maximum transform size categories.
    /// </summary>
    public const int MaxTransformCategories = 4;

    public const int CoefficientContextCount = 6;

    public const int BaseLevelsCount = 2;

    public const int CoefficientBaseRange = 12;

    public const int MaxTransformSize = 1 << 6;

    public const int MaxTransformSizeUnit = MaxTransformSize >> 2;

    public const int CoefficientContextBitCount = 6;

    public const int CoefficientContextMask = (1 << CoefficientContextBitCount) - 1;

    public const int TransformPadHorizontalLog2 = 2;

    public const int TransformPadHorizontal = 1 << TransformPadHorizontalLog2;

    public const int TransformPadVertical = 6;

    public const int TransformPadEnd = 16;

    public const int TransformPad2d = ((MaxTransformSize + TransformPadHorizontal) * (MaxTransformSize + TransformPadVertical)) + TransformPadEnd;

    public const int TransformPadTop = 2;

    public const int TransformPadBottom = 4;

    public const int BaseRangeSizeMinus1 = 3;

    public const int MaxBaseRange = 15;

    /// <summary>
    /// Log2 of number of values for ChromaFromLuma Alpha U and ChromaFromLuma Alpha V.
    /// </summary>
    public const int ChromaFromLumaAlphabetSizeLog2 = 4;

    /// <summary>
    /// Total number of Quantification Matrices sets stored.
    /// </summary>
    public const int QuantificationMatrixLevelCount = 4;

    public const int AngleStep = 3;

    /// <summary>
    /// Maximum number of stages in a 1-dimensioanl transform function.
    /// </summary>
    public const int MaxTransformStageNumber = 12;

    public const int PartitionProbabilitySet = 4;

    // Number of transform sizes that use extended transforms.
    public const int ExtendedTransformCount = 4;

    public const int MaxVarTransform = 2;

    /// <summary>
    /// Maximum number of transform blocks per depth
    /// </summary>
    public const int MaxTransformBlockCount = 16;

    /// <summary>
    /// Number of items in the <see cref="Av1PlaneType"/> enumeration.
    /// </summary>
    public const int PlaneTypeCount = 2;

    public const int MaxTransformUnitCount = 16;
}
