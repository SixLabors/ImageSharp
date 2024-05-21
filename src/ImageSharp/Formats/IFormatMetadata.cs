// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// An interface that provides metadata for a specific image format.
/// </summary>
public interface IFormatMetadata : IDeepCloneable
{
    /// <summary>
    /// Converts the metadata to a <see cref="FormatConnectingMetadata"/> instance.
    /// </summary>
    /// <returns>The <see cref="FormatConnectingMetadata"/>.</returns>
    FormatConnectingMetadata ToFormatConnectingMetadata();
}

/// <summary>
/// An interface that provides metadata for a specific image format.
/// </summary>
/// <typeparam name="TSelf">The metadata type implementing this interface.</typeparam>
public interface IFormatMetadata<TSelf> : IFormatMetadata, IDeepCloneable<TSelf>
    where TSelf : class, IFormatMetadata, new()
{
    /// <summary>
    /// Creates a new instance of the <typeparamref name="TSelf"/> class from the given <see cref="FormatConnectingMetadata"/>.
    /// </summary>
    /// <param name="metadata">The <see cref="FormatConnectingMetadata"/>.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types
    static abstract TSelf FromFormatConnectingMetadata(FormatConnectingMetadata metadata);
#pragma warning restore CA1000 // Do not declare static members on generic types
}
