// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

/// <summary>
/// JPEG XL ANS encoder clustering type
/// </summary>
internal enum JxlClusteringType : byte
{
    /// <summary>
    /// Fastest clustering type, with only 4 clusters
    /// </summary>
    Fastest,

    /// <summary>
    /// A fast clustering type.
    /// </summary>
    Fast,

    /// <summary>
    /// Slower clustering type.
    /// </summary>
    Best,
}
