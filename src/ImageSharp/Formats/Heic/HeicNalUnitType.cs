// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Provides enumeration of supported x265's LAN Unit Types.
/// </summary>
public enum HeicNalUnitType : byte
{
    CODED_SLICE_TRAIL_N = 0,
    CODED_SLICE_TRAIL_R = 1,

    CODED_SLICE_TSA_N = 2,
    CODED_SLICE_TSA_R = 3,

    CODED_SLICE_STSA_N = 4,
    CODED_SLICE_STSA_R = 5,

    CODED_SLICE_RADL_N = 6,
    CODED_SLICE_RADL_R = 7,

    CODED_SLICE_RASL_N = 8,
    CODED_SLICE_RASL_R = 9,

    VParameterSet = 32,
    SequenceParameterSet = 33,
    PictureParameterSet = 34,
    AccessUnitDelimiter = 35,
    EndOfSequence = 36,
    IsEndOfStream = 37,
    FillerData = 38,
    PrefixSei = 39,
    SuffixSei = 40,
    Invalid = 64,
}
