// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

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
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata();

    /// <summary>
    /// This method is called after a process has been applied to the image frame.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    /// <param name="source">The source image frame.</param>
    /// <param name="destination">The destination image frame.</param>
    public void AfterFrameApply<TPixel>(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>;
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
    public static abstract TSelf FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata);
#pragma warning restore CA1000 // Do not declare static members on generic types
}
