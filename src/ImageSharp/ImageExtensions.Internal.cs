// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <content>
/// Contains internal extensions for <see cref="Image{TPixel}"/>
/// </content>
public static partial class ImageExtensions
{
    /// <summary>
    /// Provides access to the image pixels.
    /// <remarks>
    /// It is imperative that the accessor is correctly disposed of after use.
    /// </remarks>
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="image">The image.</param>
    /// <returns>
    /// The <see cref="Buffer2D{TPixel}" />
    /// </returns>
    internal static Buffer2D<TPixel> GetRootFramePixelBuffer<TPixel>(this Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
        => image.Frames.RootFrame.PixelBuffer;
}
