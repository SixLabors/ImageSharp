// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the timing syntax signaled by an AV1 sequence header.
/// </summary>
internal sealed class ObuTimingInfo
{
    /// <summary>
    /// Gets or sets the number of time units in one display clock tick. One tick lasts this value divided by <see cref="TimeScale"/> seconds.
    /// </summary>
    public uint NumUnitsInDisplayTick { get; set; }

    /// <summary>
    /// Gets or sets the number of time units in one second. A conforming bitstream has a value greater than zero.
    /// </summary>
    public uint TimeScale { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether consecutive pictures in output order have a fixed interval of <see cref="NumTicksPerPicture"/> ticks.
    /// When the value is <see langword="false"/>, the interval between pictures is not specified.
    /// </summary>
    public bool EqualPictureInterval { get; set; }

    /// <summary>
    /// Gets or sets the number of clock ticks between two consecutive pictures in output order.
    /// </summary>
    public uint NumTicksPerPicture { get; set; }
}
