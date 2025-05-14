// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Gets the closest color to the supplied color based upon the Euclidean distance.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
/// <typeparam name="TCache">The cache type.</typeparam>
/// <para>
/// This class is not thread safe and should not be accessed in parallel.
/// Doing so will result in non-idempotent results.
/// </para>
internal sealed class EuclideanPixelMap<TPixel, TCache> : PixelMap<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
    where TCache : struct, IColorIndexCache<TCache>
{
    private Rgba32[] rgbaPalette;

    // Do not make readonly. It's a mutable struct.
#pragma warning disable IDE0044 // Add readonly modifier
    private TCache cache;
#pragma warning restore IDE0044 // Add readonly modifier

    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="EuclideanPixelMap{TPixel, TCache}"/> class.
    /// </summary>
    /// <param name="configuration">Specifies the settings and resources for the pixel map's operations.</param>
    /// <param name="palette">Defines the color palette used for pixel mapping.</param>
    public EuclideanPixelMap(Configuration configuration, ReadOnlyMemory<TPixel> palette)
    {
        this.configuration = configuration;
        this.Palette = palette;
        this.rgbaPalette = new Rgba32[palette.Length];
        this.cache = TCache.Create(configuration.MemoryAllocator);
        PixelOperations<TPixel>.Instance.ToRgba32(configuration, this.Palette.Span, this.rgbaPalette);
    }

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public override int GetClosestColor(TPixel color, out TPixel match)
    {
        ref TPixel paletteRef = ref MemoryMarshal.GetReference(this.Palette.Span);
        Rgba32 rgba = color.ToRgba32();

        if (this.cache.TryGetValue(rgba, out short index))
        {
            match = Unsafe.Add(ref paletteRef, (ushort)index);
            return index;
        }

        return this.GetClosestColorSlow(rgba, ref paletteRef, out match);
    }

    /// <inheritdoc/>
    public override void Clear(ReadOnlyMemory<TPixel> palette)
    {
        this.Palette = palette;
        this.rgbaPalette = new Rgba32[palette.Length];
        PixelOperations<TPixel>.Instance.ToRgba32(this.configuration, this.Palette.Span, this.rgbaPalette);
        this.cache.Clear();
    }

    [MethodImpl(InliningOptions.ColdPath)]
    private int GetClosestColorSlow(Rgba32 rgba, ref TPixel paletteRef, out TPixel match)
    {
        // Loop through the palette and find the nearest match.
        int index = 0;
        float leastDistance = float.MaxValue;
        for (int i = 0; i < this.rgbaPalette.Length; i++)
        {
            Rgba32 candidate = this.rgbaPalette[i];
            if (candidate.PackedValue == rgba.PackedValue)
            {
                index = i;
                break;
            }

            float distance = DistanceSquared(rgba, candidate);
            if (distance == 0)
            {
                index = i;
                break;
            }

            if (distance < leastDistance)
            {
                index = i;
                leastDistance = distance;
            }
        }

        // Now I have the index, pop it into the cache for next time
        _ = this.cache.TryAdd(rgba, (short)index);
        match = Unsafe.Add(ref paletteRef, (uint)index);

        return index;
    }

    /// <summary>
    /// Returns the Euclidean distance squared between two specified points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The distance squared.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static float DistanceSquared(Rgba32 a, Rgba32 b)
    {
        float deltaR = a.R - b.R;
        float deltaG = a.G - b.G;
        float deltaB = a.B - b.B;
        float deltaA = a.A - b.A;
        return (deltaR * deltaR) + (deltaG * deltaG) + (deltaB * deltaB) + (deltaA * deltaA);
    }

    /// <inheritdoc/>
    public override void Dispose() => this.cache.Dispose();
}

/// <summary>
/// Represents a map of colors to indices.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal abstract class PixelMap<TPixel> : IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the color palette of this <see cref="PixelMap{TPixel}"/>.
    /// </summary>
    public ReadOnlyMemory<TPixel> Palette { get; private protected set; }

    /// <summary>
    /// Returns the closest color in the palette and the index of that pixel.
    /// </summary>
    /// <param name="color">The color to match.</param>
    /// <param name="match">The matched color.</param>
    /// <returns>
    /// The <see cref="int"/> index.
    /// </returns>
    public abstract int GetClosestColor(TPixel color, out TPixel match);

    /// <summary>
    /// Clears the map, resetting it to use the given palette.
    /// </summary>
    /// <param name="palette">The color palette to map from.</param>
    public abstract void Clear(ReadOnlyMemory<TPixel> palette);

    /// <inheritdoc/>
    public abstract void Dispose();
}

/// <summary>
/// A factory for creating <see cref="PixelMap{TPixel}"/> instances.
/// </summary>
internal static class PixelMapFactory
{
    /// <summary>
    /// Creates a new <see cref="PixelMap{TPixel}"/> instance.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="palette">The color palette to map from.</param>
    /// <param name="colorMatchingMode">The color matching mode.</param>
    /// <returns>
    /// The <see cref="PixelMap{TPixel}"/>.
    /// </returns>
    public static PixelMap<TPixel> Create<TPixel>(
        Configuration configuration,
        ReadOnlyMemory<TPixel> palette,
        ColorMatchingMode colorMatchingMode)
        where TPixel : unmanaged, IPixel<TPixel> => colorMatchingMode switch
        {
            ColorMatchingMode.Hybrid => new EuclideanPixelMap<TPixel, HybridCache>(configuration, palette),
            ColorMatchingMode.Exact => new EuclideanPixelMap<TPixel, NullCache>(configuration, palette),
            _ => new EuclideanPixelMap<TPixel, CoarseCache>(configuration, palette),
        };
}
