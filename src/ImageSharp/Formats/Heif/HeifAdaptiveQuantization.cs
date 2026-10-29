// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Specifies how the encoder varies the compression between areas of a frame.
/// </summary>
public enum HeifAdaptiveQuantization
{
    /// <summary>
    /// The encoder compresses all areas of a frame equally.
    /// </summary>
    None = 0,

    /// <summary>
    /// Areas with less detail get more quality, and areas with more detail get less quality.
    /// </summary>
    Variance = 1,

    /// <summary>
    /// Areas that need few bits get more quality, and areas that need many bits get less. The frames must spend a bit
    /// budget, so this option needs a <see cref="HeifRateControl"/> other than
    /// <see cref="HeifRateControl.ConstantQuality"/>.
    /// </summary>
    Complexity = 2,

    /// <summary>
    /// Each frame gives more quality to a different part of the still areas, so these areas get sharper over a few
    /// frames.
    /// </summary>
    CyclicRefresh = 3
}
