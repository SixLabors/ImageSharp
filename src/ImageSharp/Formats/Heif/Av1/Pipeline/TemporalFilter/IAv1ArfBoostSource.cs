// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <summary>
/// Supplies the first-pass boost of an alternate reference, which limits the number of frames the temporal filter
/// uses for it.
/// </summary>
internal interface IAv1ArfBoostSource
{
    /// <summary>
    /// Returns the boost of the frame at a look-ahead offset from the first-pass statistics of the frames around it. The boost
    /// uses the good-quality scale limit and no projected group boost.
    /// </summary>
    /// <param name="offset">The look-ahead index of the frame.</param>
    /// <param name="forwardFrames">The number of later frames to read.</param>
    /// <param name="backwardFrames">The number of earlier frames to read.</param>
    /// <returns>The boost.</returns>
    public int CalculateArfBoost(int offset, int forwardFrames, int backwardFrames);
}
