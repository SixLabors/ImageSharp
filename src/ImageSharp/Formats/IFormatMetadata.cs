// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// An interface that provides metadata for a specific image format.
/// </summary>
public interface IFormatMetadata : IDeepCloneable
{
    /// <summary>
    /// Converts the metadata to a <see cref="PixelTypeInfo"/> instance.
    /// </summary>
    /// <returns>The pixel type info.</returns>
    public PixelTypeInfo GetPixelTypeInfo();

    /// <summary>
    /// Converts the metadata to a <see cref="FormatConnectingMetadata"/> instance.
    /// </summary>
    /// <returns>The <see cref="FormatConnectingMetadata"/>.</returns>
    public FormatConnectingMetadata ToFormatConnectingMetadata();

    /// <summary>
    /// This method is called after a process has been applied to the image.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    /// <param name="destination">The destination image .</param>
    /// <param name="matrix">The transformation matrix applied to the image.</param>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>;
}

/// <summary>
/// An interface that provides metadata for a specific image format.
/// </summary>
/// <typeparam name="TSelf">The metadata type implementing this interface.</typeparam>
public interface IFormatMetadata<TSelf> : IFormatMetadata, IDeepCloneable<TSelf>
    where TSelf : class, IFormatMetadata
{
    /// <summary>
    /// Creates a new instance of the <typeparamref name="TSelf"/> class from the given <see cref="FormatConnectingMetadata"/>.
    /// </summary>
    /// <param name="metadata">The <see cref="FormatConnectingMetadata"/>.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
#pragma warning disable CA1000 // Do not declare static members on generic types
    public static abstract TSelf FromFormatConnectingMetadata(FormatConnectingMetadata metadata);
#pragma warning restore CA1000 // Do not declare static members on generic types
}
