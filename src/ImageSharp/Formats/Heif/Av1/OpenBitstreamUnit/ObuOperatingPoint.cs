// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuOperatingPoint
{
    internal int OperatorIndex { get; set; }

    internal int SequenceLevelIndex { get; set; }

    internal int SequenceTier { get; set; }

    internal bool IsDecoderModelInfoPresent { get; set; }

    internal bool IsInitialDisplayDelayPresent { get; set; }

    internal uint InitialDisplayDelay { get; set; }

    /// <summary>
    /// Gets or sets of sets the Idc bitmask. The bitmask that indicates which spatial and temporal layers should be decoded for
    /// operating point i.Bit k is equal to 1 if temporal layer k should be decoded(for k between 0 and 7). Bit j+8 is equal to 1 if
    /// spatial layer j should be decoded(for j between 0 and 3).
    /// However, if operating_point_idc[i] is equal to 0 then the coded video sequence has no scalability information in OBU
    /// extension headers and the operating point applies to the entire coded video sequence.This means that all OBUs must be decoded.
    /// It is a requirement of bitstream conformance that operating_point_idc[i] is not equal to operating_point_idc[j] for j = 0..(i- 1).
    /// </summary>
    internal uint Idc { get; set; }
}
