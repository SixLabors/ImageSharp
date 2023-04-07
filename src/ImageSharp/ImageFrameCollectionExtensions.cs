// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <summary>
/// Extension methods for <see cref="ImageFrameCollection{TPixel}"/>.
/// </summary>
public static class ImageFrameCollectionExtensions
{
    /// <inheritdoc cref="Enumerable.AsEnumerable{TPixel}(IEnumerable{TPixel})"/>
    public static IEnumerable<ImageFrame<TPixel>> AsEnumerable<TPixel>(this ImageFrameCollection<TPixel> source)
        where TPixel : unmanaged, IPixel<TPixel>
        => source;

    /// <inheritdoc cref="Enumerable.Select{TPixel, TResult}(IEnumerable{TPixel}, Func{TPixel, int, TResult})"/>
    public static IEnumerable<TResult> Select<TPixel, TResult>(this ImageFrameCollection<TPixel> source, Func<ImageFrame<TPixel>, TResult> selector)
        where TPixel : unmanaged, IPixel<TPixel> => source.AsEnumerable().Select(selector);
}
