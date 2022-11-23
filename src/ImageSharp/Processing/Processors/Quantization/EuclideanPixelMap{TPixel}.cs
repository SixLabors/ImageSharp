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
/// This class is not threadsafe and should not be accessed in parallel.
/// Doing so will result in non-idempotent results.
/// </para>
internal sealed class EuclideanPixelMap<TPixel> : IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private Rgba32[] rgbaPalette;

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
    {
        this.configuration = configuration;
        this.Palette = palette;
        this.rgbaPalette = new Rgba32[palette.Length];
        this.cache = new ColorDistanceCache(configuration.MemoryAllocator);
        PixelOperations<TPixel>.Instance.ToRgba32(configuration, this.Palette.Span, this.rgbaPalette);
    }

    /// <summary>
    /// Gets the color palette of this <see cref="EuclideanPixelMap{TPixel}"/>.
    /// The palette memory is owned by the palette source that created it.
    /// </summary>
    public ReadOnlyMemory<TPixel> Palette
    {
        [MethodImpl(InliningOptions.ShortMethod)]
        get;

        [MethodImpl(InliningOptions.ShortMethod)]
        private set;
    }

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
        Unsafe.SkipInit(out Rgba32 rgba);
        color.ToRgba32(ref rgba);

        // Check if the color is in the lookup table
        if (!this.cache.TryGetValue(rgba, out short index))
        {
            return this.GetClosestColorSlow(rgba, ref paletteRef, out match);
        }

        match = Unsafe.Add(ref paletteRef, index);
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
        this.cache.Clear();
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private int GetClosestColorSlow(Rgba32 rgba, ref TPixel paletteRef, out TPixel match)
    {
        // Loop through the palette and find the nearest match.
        int index = 0;
        float leastDistance = float.MaxValue;
        for (int i = 0; i < this.rgbaPalette.Length; i++)
        {
            Rgba32 candidate = this.rgbaPalette[i];
            int distance = DistanceSquared(rgba, candidate);

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
        match = Unsafe.Add(ref paletteRef, index);
        return index;
    }

    /// <summary>
    /// Returns the Euclidean distance squared between two specified points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The distance squared.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static int DistanceSquared(Rgba32 a, Rgba32 b)
    {
        int deltaR = a.R - b.R;
        int deltaG = a.G - b.G;
        int deltaB = a.B - b.B;
        int deltaA = a.A - b.A;
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
    /// Entry count is currently limited to 1185921 entries (2371842 bytes ~2.26MB).
    /// </para>
    /// </remarks>
    private unsafe struct ColorDistanceCache : IDisposable
    {
        private const int IndexBits = 5;
        private const int IndexAlphaBits = 5;
        private const int IndexCount = (1 << IndexBits) + 1;
        private const int IndexAlphaCount = (1 << IndexAlphaBits) + 1;
        private const int RgbShift = 8 - IndexBits;
        private const int AlphaShift = 8 - IndexAlphaBits;
        private const int Entries = IndexCount * IndexCount * IndexCount * IndexAlphaCount;
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
        public void Add(Rgba32 rgba, byte index)
        {
            int r = rgba.R >> RgbShift;
            int g = rgba.G >> RgbShift;
            int b = rgba.B >> RgbShift;
            int a = rgba.A >> AlphaShift;
            int idx = GetPaletteIndex(r, g, b, a);
            this.tablePointer[idx] = index;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public bool TryGetValue(Rgba32 rgba, out short match)
        {
            int r = rgba.R >> RgbShift;
            int g = rgba.G >> RgbShift;
            int b = rgba.B >> RgbShift;
            int a = rgba.A >> AlphaShift;
            int idx = GetPaletteIndex(r, g, b, a);
            match = this.tablePointer[idx];
            return match > -1;
        }

        /// <summary>
        /// Clears the cache resetting each entry to empty.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Clear() => this.table.GetSpan().Fill(-1);

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int GetPaletteIndex(int r, int g, int b, int a)
            => (r << ((IndexBits << 1) + IndexAlphaBits))
            + (r << (IndexBits + IndexAlphaBits + 1))
            + (g << (IndexBits + IndexAlphaBits))
            + (r << (IndexBits << 1))
            + (r << (IndexBits + 1))
            + (g << IndexBits)
            + ((r + g + b) << IndexAlphaBits)
            + r + g + b + a;

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
