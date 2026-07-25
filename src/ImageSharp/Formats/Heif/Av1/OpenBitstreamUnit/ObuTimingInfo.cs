// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuTimingInfo
{
    /// <summary>
    /// Gets or sets NumUnitsInDisplayTick. NumUnitsInDisplayTick is the number of time units of a clock operating at the frequency TimeScale Hz that
    /// corresponds to one increment of a clock tick counter. A display clock tick, in seconds, is equal to
    /// NumUnitsInDisplayTick divided by TimeScale.
    /// </summary>
    public uint NumUnitsInDisplayTick { get; set; }

    /// <summary>
    /// Gets or sets TimeScale. TimeScale is the number of time units that pass in one second.
    /// It is a requirement of bitstream conformance that TimeScale is greater than 0.
    /// </summary>
    public uint TimeScale { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether that pictures should be displayed according to their output order with the
    /// number of ticks between two consecutive pictures (without dropping frames) specified by NumTicksPerPicture.
    /// EqualPictureInterval equal to false indicates that the interval between two consecutive pictures is not specified.
    /// </summary>
    public bool EqualPictureInterval { get; set; }

    /// <summary>
    /// Gets or sets NumTicksPerPicture. NumTicksPerPicture specifies the number of clock ticks corresponding to output time between two
    /// consecutive pictures in the output order.
    /// </summary>
    public uint NumTicksPerPicture { get; set; }
}
