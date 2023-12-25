// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Provides enumeration of supported x265's LAN Unit Types.
/// </summary>
public enum HeicNalUnitType : uint
{
    NAL_UNIT_CODED_SLICE_TRAIL_N = 0,
    NAL_UNIT_CODED_SLICE_TRAIL_R,
    NAL_UNIT_CODED_SLICE_TSA_N,
    NAL_UNIT_CODED_SLICE_TSA_R,
    NAL_UNIT_CODED_SLICE_STSA_N,
    NAL_UNIT_CODED_SLICE_STSA_R,
    NAL_UNIT_CODED_SLICE_RADL_N,
    NAL_UNIT_CODED_SLICE_RADL_R,
    NAL_UNIT_CODED_SLICE_RASL_N,
    NAL_UNIT_CODED_SLICE_RASL_R,
    NAL_UNIT_CODED_SLICE_BLA_W_LP = 16,
    NAL_UNIT_CODED_SLICE_BLA_W_RADL,
    NAL_UNIT_CODED_SLICE_BLA_N_LP,
    NAL_UNIT_CODED_SLICE_IDR_W_RADL,
    NAL_UNIT_CODED_SLICE_IDR_N_LP,
    NAL_UNIT_CODED_SLICE_CRA,
    NAL_UNIT_VPS = 32,
    NAL_UNIT_SPS,
    NAL_UNIT_PPS,
    NAL_UNIT_ACCESS_UNIT_DELIMITER,
    NAL_UNIT_EOS,
    NAL_UNIT_EOB,
    NAL_UNIT_FILLER_DATA,
    NAL_UNIT_PREFIX_SEI,
    NAL_UNIT_SUFFIX_SEI,
    Unspecified = 62,
    Invalid = 64,
}
