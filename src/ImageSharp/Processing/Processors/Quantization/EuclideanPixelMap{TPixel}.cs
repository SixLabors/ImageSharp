// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Gets the closest color to the supplied color based upon the Euclidean distance.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
/// <para>
/// This class is not thread safe and should not be accessed in parallel.
/// Doing so will result in non-idempotent results.
/// </para>
internal sealed class EuclideanPixelMap<TPixel> : IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private Rgba32[] rgbaPalette;
    private int transparentIndex;
    private readonly TPixel transparentMatch;

    /// <summary>
    /// Do not make this readonly! Struct value would be always copied on non-readonly method calls.
    /// </summary>
    private ColorDistanceCache cache;
    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="EuclideanPixelMap{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="palette">The color palette to map from.</param>
    public EuclideanPixelMap(Configuration configuration, ReadOnlyMemory<TPixel> palette)
        : this(configuration, palette, -1)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EuclideanPixelMap{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="palette">The color palette to map from.</param>
    /// <param name="transparentIndex">An explicit index at which to match transparent pixels.</param>
    public EuclideanPixelMap(Configuration configuration, ReadOnlyMemory<TPixel> palette, int transparentIndex = -1)
    {
        this.configuration = configuration;
        this.Palette = palette;
        this.rgbaPalette = new Rgba32[palette.Length];
        this.cache = new(configuration.MemoryAllocator);
        PixelOperations<TPixel>.Instance.ToRgba32(configuration, this.Palette.Span, this.rgbaPalette);

        this.transparentIndex = transparentIndex;
        this.transparentMatch = TPixel.FromRgba32(default);
    }

    /// <summary>
    /// Gets the color palette of this <see cref="EuclideanPixelMap{TPixel}"/>.
    /// The palette memory is owned by the palette source that created it.
    /// </summary>
    public ReadOnlyMemory<TPixel> Palette { get; private set; }

    /// <summary>
    /// Returns the closest color in the palette and the index of that pixel.
    /// The palette contents must match the one used in the constructor.
    /// </summary>
    /// <param name="color">The color to match.</param>
    /// <param name="match">The matched color.</param>
    /// <returns>The <see cref="int"/> index.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public int GetClosestColor(TPixel color, out TPixel match)
    {
        ref TPixel paletteRef = ref MemoryMarshal.GetReference(this.Palette.Span);
        Rgba32 rgba = color.ToRgba32();

        // Check if the color is in the lookup table
        if (!this.cache.TryGetValue(rgba, out short index))
        {
            return this.GetClosestColorSlow(rgba, ref paletteRef, out match);
        }

        match = Unsafe.Add(ref paletteRef, (ushort)index);
        return index;
    }

    /// <summary>
    /// Clears the map, resetting it to use the given palette.
    /// </summary>
    /// <param name="palette">The color palette to map from.</param>
    public void Clear(ReadOnlyMemory<TPixel> palette)
    {
        this.Palette = palette;
        this.rgbaPalette = new Rgba32[palette.Length];
        PixelOperations<TPixel>.Instance.ToRgba32(this.configuration, this.Palette.Span, this.rgbaPalette);
        this.transparentIndex = -1;
        this.cache.Clear();
    }

    /// <summary>
    /// Allows setting the transparent index after construction.
    /// </summary>
    /// <param name="index">An explicit index at which to match transparent pixels.</param>
    public void SetTransparentIndex(int index)
    {
        if (index != this.transparentIndex)
        {
            this.cache.Clear();
        }

        this.transparentIndex = index;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private int GetClosestColorSlow(Rgba32 rgba, ref TPixel paletteRef, out TPixel match)
    {
        // Loop through the palette and find the nearest match.
        int index = 0;

        if (this.transparentIndex >= 0 && rgba == default)
        {
            // We have explicit instructions. No need to search.
            index = this.transparentIndex;
            this.cache.Add(rgba, (byte)index);
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
        this.cache.Add(rgba, (byte)index);
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

    public void Dispose() => this.cache.Dispose();

    /// <summary>
    /// A cache for storing color distance matching results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The granularity of the cache has been determined based upon the current
    /// suite of test images and provides the lowest possible memory usage while
    /// providing enough match accuracy.
    /// Entry count is currently limited to 2335905 entries (4MB).
    /// </para>
    /// </remarks>
    private unsafe struct ColorDistanceCache : IDisposable
    {
        private const int IndexRBits = 5;
        private const int IndexGBits = 5;
        private const int IndexBBits = 5;
        private const int IndexABits = 6;
        private const int IndexRCount = (1 << IndexRBits) + 1;
        private const int IndexGCount = (1 << IndexGBits) + 1;
        private const int IndexBCount = (1 << IndexBBits) + 1;
        private const int IndexACount = (1 << IndexABits) + 1;
        private const int RShift = 8 - IndexRBits;
        private const int GShift = 8 - IndexGBits;
        private const int BShift = 8 - IndexBBits;
        private const int AShift = 8 - IndexABits;
        private const int Entries = IndexRCount * IndexGCount * IndexBCount * IndexACount;
        private MemoryHandle tableHandle;
        private readonly IMemoryOwner<short> table;
        private readonly short* tablePointer;

        public ColorDistanceCache(MemoryAllocator allocator)
        {
            this.table = allocator.Allocate<short>(Entries);
            this.table.GetSpan().Fill(-1);
            this.tableHandle = this.table.Memory.Pin();
            this.tablePointer = (short*)this.tableHandle.Pointer;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly void Add(Rgba32 rgba, byte index)
        {
            int idx = GetPaletteIndex(rgba);
            this.tablePointer[idx] = index;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool TryGetValue(Rgba32 rgba, out short match)
        {
            int idx = GetPaletteIndex(rgba);
            match = this.tablePointer[idx];
            return match > -1;
        }

        /// <summary>
        /// Clears the cache resetting each entry to empty.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly void Clear() => this.table.GetSpan().Fill(-1);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetPaletteIndex(Rgba32 rgba)
        {
            int rIndex = rgba.R >> RShift;
            int gIndex = rgba.G >> GShift;
            int bIndex = rgba.B >> BShift;
            int aIndex = rgba.A >> AShift;

            return (aIndex * (IndexRCount * IndexGCount * IndexBCount)) +
                   (rIndex * (IndexGCount * IndexBCount)) +
                   (gIndex * IndexBCount) + bIndex;
        }

        public void Dispose()
        {
            if (this.table != null)
            {
                this.tableHandle.Dispose();
                this.table.Dispose();
            }
        }
    }
}
