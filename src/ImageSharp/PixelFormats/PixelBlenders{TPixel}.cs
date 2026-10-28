// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Provides shared pixel blenders for color blending and alpha composition.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
/// <remarks>
/// <para>
/// Each blender uses the alpha representation declared by <typeparamref name="TPixel"/>.
/// Named properties, such as <see cref="NormalSrcOver"/>, select a fixed pair of modes.
/// The <see cref="GetPixelBlender(GraphicsOptions)"/> and
/// <see cref="GetPixelBlender(PixelColorBlendingMode, PixelAlphaCompositionMode)"/> methods select modes at runtime.
/// </para>
/// </remarks>
public static partial class PixelBlenders<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    // This type owns blender selection. PixelBlender owns composition and calls PixelOperations for pixel-buffer conversion.
    // PixelOperations owns conversion rules and bulk optimizations. Pixel formats own rounding and storage layout.
    // Named properties reference only their selected equations. The runtime selector references every supported equation.
    // This separation lets the trimmer remove unused equations without changing pixel conversion or composition arithmetic.

    /// <summary>
    /// Gets the shared blender for the color blending and alpha composition modes in the options.
    /// </summary>
    /// <param name="options">The options that specify the two modes. Other option values do not affect selection.</param>
    /// <returns>The shared pixel blender.</returns>
#pragma warning disable CA1000 // The pixel type determines the shared blender instances.
    public static PixelBlender<TPixel> GetPixelBlender(GraphicsOptions options)
        => GetPixelBlender(options.ColorBlendingMode, options.AlphaCompositionMode);
#pragma warning restore CA1000 // The pixel type determines the shared blender instances.
}
