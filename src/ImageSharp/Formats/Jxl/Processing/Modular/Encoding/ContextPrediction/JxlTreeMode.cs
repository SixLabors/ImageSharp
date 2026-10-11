// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;

/// <summary>
/// Decides which kinds of trees should be allowed.
/// </summary>
internal enum JxlTreeMode : byte
{
    /// <summary>
    /// Gradient tree only
    /// </summary>
    GradientOnly,

    /// <summary>
    /// Weighted predictor only
    /// </summary>
    WpOnly,

    /// <summary>
    /// Disable weighted predictor
    /// </summary>
    NoWp,

    /// <summary>
    /// Default trees
    /// </summary>
    Default
}
