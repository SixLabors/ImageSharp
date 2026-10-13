// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Classifies source changes for estimated mode decisions.
/// </summary>
internal enum Av1SourceSadLevel : byte
{
    /// <summary>No source change.</summary>
    Zero,

    /// <summary>Very little source change.</summary>
    VeryLow,

    /// <summary>Little source change.</summary>
    Low,

    /// <summary>Moderate source change.</summary>
    Medium,

    /// <summary>Large source change.</summary>
    High
}
