// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
    private int transparentIndex;
    private readonly TPixel transparentMatch;

    // Do not make readonly. It's a mutable struct.
#pragma warning disable IDE0044 // Add readonly modifier
    private TCache cache;
#pragma warning restore IDE0044 // Add readonly modifier
    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="EuclideanPixelMap{TPixel, TCache}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="palette">The color palette to map from.</param>
    /// <param name="transparentIndex">An explicit index at which to match transparent pixels.</param>
    [RequiresPreviewFeatures]
    public EuclideanPixelMap(
        Configuration configuration,
        ReadOnlyMemory<TPixel> palette,
        int transparentIndex = -1)
    {
        this.configuration = configuration;
        this.cache = TCache.Create(configuration.MemoryAllocator);

        this.Palette = palette;
        this.rgbaPalette = new Rgba32[palette.Length];
        PixelOperations<TPixel>.Instance.ToRgba32(configuration, this.Palette.Span, this.rgbaPalette);

        this.transparentIndex = transparentIndex;
        Unsafe.SkipInit(out this.transparentMatch);
        this.transparentMatch.FromRgba32(default);
    }

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public override int GetClosestColor(TPixel color, out TPixel match)
    {
        ref TPixel paletteRef = ref MemoryMarshal.GetReference(this.Palette.Span);
        Unsafe.SkipInit(out Rgba32 rgba);
        color.ToRgba32(ref rgba);

        // Check if the color is in the lookup table
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
        this.transparentIndex = -1;
        this.cache.Clear();
    }

    /// <inheritdoc/>
    public override void SetTransparentIndex(int index)
    {
        if (index != this.transparentIndex)
        {
            this.cache.Clear();
        }

        this.transparentIndex = index;
    }

    [MethodImpl(InliningOptions.ColdPath)]
    private int GetClosestColorSlow(Rgba32 rgba, ref TPixel paletteRef, out TPixel match)
    {
        // Loop through the palette and find the nearest match.
        int index = 0;

        if (this.transparentIndex >= 0 && rgba == default)
        {
            // We have explicit instructions. No need to search.
            index = this.transparentIndex;
            _ = this.cache.TryAdd(rgba, (short)index);
            match = this.transparentMatch;
            return index;
        }

        float leastDistance = float.MaxValue;
        for (int i = 0; i < this.rgbaPalette.Length; i++)
        {
            Rgba32 candidate = this.rgbaPalette[i];
            float distance = DistanceSquared(rgba, candidate);

            // If it's an exact match, exit the loop
            if (distance == 0)
            {
                index = i;
                break;
            }

            if (distance < leastDistance)
            {
                // Less than... assign.
                index = i;
                leastDistance = distance;
            }
        }

        // Now I have the index, pop it into the cache for next time
        _ = this.cache.TryAdd(rgba, (short)index);
        match = Unsafe.Add(ref paletteRef, (uint)index);

        return index;
    }

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

    /// <summary>
    /// Allows setting the transparent index after construction.
    /// </summary>
    /// <param name="index">An explicit index at which to match transparent pixels.</param>
    public abstract void SetTransparentIndex(int index);

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
    /// <param name="transparentIndex">An explicit index at which to match transparent pixels.</param>
    /// <returns>
    /// The <see cref="PixelMap{TPixel}"/>.
    /// </returns>
    public static PixelMap<TPixel> Create<TPixel>(
        Configuration configuration,
        ReadOnlyMemory<TPixel> palette,
        ColorMatchingMode colorMatchingMode,
        int transparentIndex = -1)
        where TPixel : unmanaged, IPixel<TPixel> => colorMatchingMode switch
        {
            ColorMatchingMode.Hybrid => new EuclideanPixelMap<TPixel, HybridCache>(configuration, palette, transparentIndex),
            ColorMatchingMode.Exact => new EuclideanPixelMap<TPixel, NullCache>(configuration, palette, transparentIndex),
            _ => new EuclideanPixelMap<TPixel, CoarseCache>(configuration, palette, transparentIndex),
        };
}
