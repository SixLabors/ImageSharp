// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

/// <summary>
/// ANS histogram strategy during encoding
/// </summary>
internal enum JxlAnsHistogramStrategy : byte
{
    /// <summary>
    /// Only try a few methods, early exit.
    /// </summary>
    Fast,

    /// <summary>
    /// Only try a few methods.
    /// </summary>
    Approximate,

    /// <summary>
    /// Try all methods.
    /// </summary>
    Precise
}
