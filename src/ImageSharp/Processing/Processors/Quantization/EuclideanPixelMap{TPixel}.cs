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
    private HybridColorDistanceCache cache;
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
        this.cache = new HybridColorDistanceCache(configuration.MemoryAllocator);
        PixelOperations<TPixel>.Instance.ToRgba32(configuration, this.Palette.Span, this.rgbaPalette);

        this.transparentIndex = transparentIndex;
        Unsafe.SkipInit(out this.transparentMatch);
        this.transparentMatch.FromRgba32(default);
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
    /// A hybrid cache for color distance lookups that combines an exact-match dictionary with
    /// a fallback coarse lookup table.
    /// </summary>
    /// <remarks>
    /// This cache uses a fallback table with  2,097,152 bins, each storing a 2-byte value
    /// (approximately 4 MB total), while the exact-match dictionary is limited to 256 entries
    /// and occupies roughly 4 KB. Overall, the worst-case memory usage is about 4 MB.
    /// Lookups and insertions are performed in constant time (O(1)) because the fallback table
    /// is accessed via direct indexing and the dictionary employs a simple hash-based bucket mechanism.
    /// The design achieves extremely fast color distance lookups with a predictable, fixed memory footprint.
    /// </remarks>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable

    // https://github.com/dotnet/roslyn-analyzers/issues/6151
    private unsafe struct HybridColorDistanceCache : IDisposable
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private const int IndexRBits = 5;
        private const int IndexGBits = 5;
        private const int IndexBBits = 5;
        private const int IndexABits = 6;
        private const int IndexRCount = 1 << IndexRBits; // 32 bins for red
        private const int IndexGCount = 1 << IndexGBits; // 32 bins for green
        private const int IndexBCount = 1 << IndexBBits; // 32 bins for blue
        private const int IndexACount = 1 << IndexABits; // 64 bins for alpha
        private const int TotalBins = IndexRCount * IndexGCount * IndexBCount * IndexACount; // 2,097,152 bins

        private readonly IMemoryOwner<short> fallbackTable;
        private readonly short* fallbackPointer;
        private MemoryHandle fallbackHandle;

        private readonly ExactCache exactCache;

        public HybridColorDistanceCache(MemoryAllocator allocator)
        {
            this.fallbackTable = allocator.Allocate<short>(TotalBins);
            this.fallbackTable.GetSpan().Fill(-1);
            this.fallbackHandle = this.fallbackTable.Memory.Pin();
            this.fallbackPointer = (short*)this.fallbackHandle.Pointer;

            this.exactCache = new ExactCache(allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Add(Rgba32 color, short index)
        {
            if (this.exactCache.TryAdd(color.PackedValue, index))
            {
                return;
            }

            this.fallbackPointer[GetCoarseIndex(color)] = index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryGetValue(Rgba32 color, out short match)
        {
            if (this.exactCache.TryGetValue(color.PackedValue, out match))
            {
                return true; // Exact match found
            }

            match = this.fallbackPointer[GetCoarseIndex(color)];
            return match > -1; // Coarse match found
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetCoarseIndex(Rgba32 color)
        {
            int rIndex = color.R >> (8 - IndexRBits);
            int gIndex = color.G >> (8 - IndexGBits);
            int bIndex = color.B >> (8 - IndexBBits);
            int aIndex = color.A >> (8 - IndexABits);

            return (aIndex * IndexRCount * IndexGCount * IndexBCount) +
                   (rIndex * IndexGCount * IndexBCount) +
                   (gIndex * IndexBCount) +
                   bIndex;
        }

        public readonly void Clear()
        {
            this.exactCache.Clear();
            this.fallbackTable.GetSpan().Fill(-1);
        }

        public void Dispose()
        {
            this.fallbackHandle.Dispose();
            this.fallbackTable.Dispose();
            this.exactCache.Dispose();
        }
    }

    /// <summary>
    /// A fixed-capacity dictionary with exactly 512 entries mapping a <see cref="uint"/> key
    /// to a <see cref="short"/> value.
    /// </summary>
    /// <remarks>
    /// The dictionary is implemented using a fixed array of 512 buckets and an entries array
    /// of the same size. The bucket for a key is computed as (key &amp; 0x1FF), and collisions are
    /// resolved through a linked chain stored in the <see cref="Entry.Next"/> field.
    /// The overall memory usage is approximately 4–5 KB. Both lookup and insertion operations are,
    /// on average, O(1) since the bucket is determined via a simple bitmask and collision chains are
    /// typically very short; in the worst-case, the number of iterations is bounded by 256.
    /// This guarantees highly efficient and predictable performance for small, fixed-size color palettes.
    /// </remarks>
    internal sealed unsafe class ExactCache : IDisposable
    {
        // Buckets array: each bucket holds the index (0-based) into the entries array
        // of the first entry in the chain, or -1 if empty.
        private readonly IMemoryOwner<short> bucketsOwner;
        private MemoryHandle bucketsHandle;
        private short* buckets;

        // Entries array: stores up to 256 entries.
        private readonly IMemoryOwner<Entry> entriesOwner;
        private MemoryHandle entriesHandle;
        private Entry* entries;

        public const int Capacity = 512;

        public ExactCache(MemoryAllocator allocator)
        {
            this.Count = 0;

            // Allocate exactly 512 ints for buckets.
            this.bucketsOwner = allocator.Allocate<short>(Capacity, AllocationOptions.Clean);
            Span<short> bucketSpan = this.bucketsOwner.GetSpan();
            bucketSpan.Fill(-1);
            this.bucketsHandle = this.bucketsOwner.Memory.Pin();
            this.buckets = (short*)this.bucketsHandle.Pointer;

            // Allocate exactly 512 entries.
            this.entriesOwner = allocator.Allocate<Entry>(Capacity, AllocationOptions.Clean);
            this.entriesHandle = this.entriesOwner.Memory.Pin();
            this.entries = (Entry*)this.entriesHandle.Pointer;
        }

        public int Count { get; private set; }

        /// <summary>
        /// Adds a key/value pair to the dictionary.
        /// If the key already exists, the dictionary is left unchanged.
        /// </summary>
        /// <param name="key">The key to add.</param>
        /// <param name="value">The value to add.</param>
        /// <returns><see langword="true"/> if the key was added; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(uint key, short value)
        {
            if (this.Count == Capacity)
            {
                return false; // Dictionary is full.
            }

            // The key is a 32-bit unsigned integer representing an RGBA color, where the bytes are laid out as R|G|B|A
            // (with R in the most significant byte and A in the least significant).
            // To compute the bucket index:
            // 1. (key >> 16) extracts the top 16 bits, effectively giving us the R and G channels.
            // 2. (key >> 8) shifts the key right by 8 bits, bringing R, G, and B into the lower 24 bits (dropping A).
            // 3. XORing these two values with the original key mixes bits from all four channels (R, G, B, and A),
            //    which helps to counteract situations where one or more channels have a limited range.
            // 4. Finally, we apply a bitmask of 0x1FF to keep only the lowest 9 bits, ensuring the result is between 0 and 511,
            //    which corresponds to our fixed bucket count of 512.
            int bucket = (int)(((key >> 16) ^ (key >> 8) ^ key) & 0x1FF);
            int i = this.buckets[bucket];

            // Traverse the collision chain.
            Entry* entries = this.entries;
            while (i != -1)
            {
                Entry e = entries[i];
                if (e.Key == key)
                {
                    // Key already exists; do not overwrite.
                    return false;
                }

                i = e.Next;
            }

            short index = (short)this.Count;
            this.Count++;

            // Insert the new entry:
            entries[index].Key = key;
            entries[index].Value = value;

            // Link this new entry into the bucket chain.
            entries[index].Next = this.buckets[bucket];
            this.buckets[bucket] = index;
            return true;
        }

        /// <summary>
        /// Tries to retrieve the value associated with the specified key.
        /// Returns true if the key is found; otherwise, returns false.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <param name="value">The value associated with the key, if found.</param>
        /// <returns><see langword="true"/> if the key is found; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(uint key, out short value)
        {
            int bucket = (int)(((key >> 16) ^ (key >> 8) ^ key) & 0x1FF);
            int i = this.buckets[bucket];

            // If the bucket is empty, return immediately.
            if (i == -1)
            {
                value = -1;
                return false;
            }

            // Traverse the chain.
            Entry* entries = this.entries;
            do
            {
                Entry e = entries[i];
                if (e.Key == key)
                {
                    value = e.Value;
                    return true;
                }

                i = e.Next;
            }
            while (i != -1);

            value = -1;
            return false;
        }

        /// <summary>
        /// Clears the dictionary.
        /// </summary>
        public void Clear()
        {
            Span<short> bucketSpan = this.bucketsOwner.GetSpan();
            bucketSpan.Fill(-1);
            this.Count = 0;
        }

        public void Dispose()
        {
            this.bucketsHandle.Dispose();
            this.bucketsOwner.Dispose();
            this.entriesHandle.Dispose();
            this.entriesOwner.Dispose();
            this.buckets = null;
            this.entries = null;
        }

        private struct Entry
        {
            public uint Key;     // The key (packed RGBA)
            public short Value;  // The value; -1 means unused.
            public short Next;     // Index of the next entry in the chain, or -1 if none.
        }
    }
}
