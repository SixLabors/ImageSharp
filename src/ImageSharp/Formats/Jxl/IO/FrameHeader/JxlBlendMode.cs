// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Represents the blending mode describing how to combine
/// current frame with previously saved frame.
/// </summary>
internal enum JxlBlendMode : byte
{
    /// <summary>
    /// New values replace old ones.
    /// <code>
    /// sample = new
    /// </code>
    /// </summary>
    Replace,

    /// <summary>
    /// New values add to the old ones.
    /// <code>
    /// sample = old + new
    /// </code>
    /// </summary>
    Add,

    /// <summary>
    /// New values replace old ones if alpha&gt;0:
    /// <code>
    /// alpha = old + new * (1 - old)
    /// </code>
    /// For other channels if !alpha_associated:
    /// <code>
    /// sample = ((1 - newAlpha) * old * oldAlpha + newAlpha * new) / alpha
    /// </code>
    /// For other channels if alpha_associated:
    /// <code>
    /// sample = (1 - newAlpha) * old + new
    /// </code>
    /// </summary>
    Blend,

    /// <summary>
    /// New values are added to the old ones if alpha&gt;0:
    /// For the alpha channel that is used as source:
    /// <code>
    /// sample = old + new * (1 - old)
    /// </code>
    /// Otherwise:
    /// <code>
    /// sample = old + alpha * new
    /// </code>
    /// </summary>
    AlphaWeightedBlend,

    /// <summary>
    /// New values are multiplied by old ones:
    /// <code>
    /// sample = old * new
    /// </code>
    /// </summary>
    Multiply
}
