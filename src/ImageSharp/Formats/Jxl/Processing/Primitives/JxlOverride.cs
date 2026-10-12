// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Represents a boolean which can be overriden to be a default
/// value.
/// </summary>
internal enum JxlOverride : sbyte
{
    /// <summary>
    /// Specifies a true value.
    /// </summary>
    On = 1,

    /// <summary>
    /// Specifies a false value.
    /// </summary>
    Off = 0,

    /// <summary>
    /// Specifies a default value.
    /// </summary>
    Default = -1
}
