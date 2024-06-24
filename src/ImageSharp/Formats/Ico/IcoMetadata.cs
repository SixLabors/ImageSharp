// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ico;

/// <summary>
/// Provides Ico specific metadata information for the image.
/// </summary>
public class IcoMetadata : IDeepCloneable<IcoMetadata>, IDeepCloneable
{
    /// <inheritdoc/>
    public IcoMetadata DeepClone() => new();

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();
}
