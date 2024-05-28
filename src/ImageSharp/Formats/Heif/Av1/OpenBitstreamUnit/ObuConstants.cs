// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal static class ObuConstants
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
}
