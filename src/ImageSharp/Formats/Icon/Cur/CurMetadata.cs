// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon.Cur;

/// <summary>
/// Provides Ico specific metadata information for the image.
/// </summary>
public class CurMetadata : IDeepCloneable<CurMetadata>, IDeepCloneable
{
    /// <inheritdoc/>
    public CurMetadata DeepClone() => new();

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();
}
