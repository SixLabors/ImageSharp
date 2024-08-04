// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// An interface that provides metadata for a specific image format frames.
/// </summary>
public interface IFormatFrameMetadata : IDeepCloneable
{
    /// <summary>
    /// Converts the metadata to a <see cref="FormatConnectingFrameMetadata"/> instance.
    /// </summary>
    /// <returns>The <see cref="FormatConnectingFrameMetadata"/>.</returns>
    FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata();
}

/// <summary>
/// An interface that provides metadata for a specific image format frames.
/// </summary>
/// <typeparam name="TSelf">The metadata type implementing this interface.</typeparam>
public interface IFormatFrameMetadata<TSelf> : IFormatFrameMetadata, IDeepCloneable<TSelf>
    where TSelf : class, IFormatFrameMetadata
{
    /// <summary>
    /// Creates a new instance of the <typeparamref name="TSelf"/> class from the given <see cref="FormatConnectingFrameMetadata"/>.
    /// </summary>
    /// <param name="metadata">The <see cref="FormatConnectingFrameMetadata"/>.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types
    static abstract TSelf FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata);
#pragma warning restore CA1000 // Do not declare static members on generic types
}
