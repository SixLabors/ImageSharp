// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
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
    private readonly HybridColorDistanceCache cache;
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
        this.cache = new HybridColorDistanceCache(configuration.MemoryAllocator);
        PixelOperations<TPixel>.Instance.ToRgba32(configuration, this.Palette.Span, this.rgbaPalette);
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
    /// <param name="transparencyThreshold">The transparency threshold.</param>
    /// <returns>The <see cref="int"/> index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetClosestColor(TPixel color, out TPixel match, short transparencyThreshold = -1)
    {
        ref TPixel paletteRef = ref MemoryMarshal.GetReference(this.Palette.Span);
        Rgba32 rgba = color.ToRgba32();

        if (transparencyThreshold > -1 && rgba.A < transparencyThreshold)
        {
            rgba = default;
        }

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
        this.cache.Clear();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DistanceSquared(Rgba32 a, Rgba32 b)
    {
        Vector4 va = new(a.R, a.G, a.B, a.A);
        Vector4 vb = new(b.R, b.G, b.B, b.A);
        return Vector4.DistanceSquared(va, vb);
    }

    public void Dispose() => this.cache.Dispose();

    /// <summary>
    /// A hybrid color distance cache that combines a small, fixed-capacity exact-match dictionary
    /// (ExactCache, ~4–5 KB for up to 512 entries) with a coarse lookup table (CoarseCache) for 5,5,5,6 precision.
    /// </summary>
    /// <remarks>
    /// ExactCache provides O(1) lookup for common cases using a simple 256-entry hash-based dictionary, while CoarseCache
    /// quantizes RGB channels to 5 bits (yielding 32^3 buckets) and alpha to 6 bits, storing up to 4 alpha entries per bucket
    /// (a design chosen based on probability theory to capture most real-world variations) for a total memory footprint of
    /// roughly 576 KB. Lookups and insertions are performed in constant time, making the overall design both fast and memory-predictable.
    /// </remarks>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    // https://github.com/dotnet/roslyn-analyzers/issues/6151
    private readonly unsafe struct HybridColorDistanceCache : IDisposable
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly CoarseCache coarseCache;
        private readonly ExactCache exactCache;

        public HybridColorDistanceCache(MemoryAllocator allocator)
        {
            this.exactCache = new ExactCache(allocator);
            this.coarseCache = new CoarseCache(allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Add(Rgba32 color, short index)
        {
            if (this.exactCache.TryAdd(color.PackedValue, index))
            {
                return;
            }

            this.coarseCache.Add(color, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryGetValue(Rgba32 color, out short match)
        {
            if (this.exactCache.TryGetValue(color.PackedValue, out match))
            {
                return true; // Exact match found
            }

            if (this.coarseCache.TryGetValue(color, out match))
            {
                return true; // Coarse match found
            }

            match = -1;
            return false;
        }

        public readonly void Clear()
        {
            this.exactCache.Clear();
            this.coarseCache.Clear();
        }

        public void Dispose()
        {
            this.exactCache.Dispose();
            this.coarseCache.Dispose();
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

    /// <summary>
    /// <para>
    /// CoarseCache is a fast, low-memory lookup structure for caching palette indices associated with RGBA values,
    /// using a quantized representation of 5,5,5,6 (RGB: 5 bits each, Alpha: 6 bits).
    /// </para>
    /// <para>
    /// The cache quantizes the RGB channels to 5 bits each, resulting in 32 levels per channel and a total of 32³ = 32,768 buckets.
    /// Each bucket is represented by an <see cref="AlphaBucket"/>, which holds a small, inline array of alpha entries.
    /// Each alpha entry stores the alpha value quantized to 6 bits (0–63) along with a palette index (a 16-bit value).
    /// </para>
    /// <para>
    /// Performance Characteristics:
    ///   - Lookup: O(1) for computing the bucket index from the RGB channels, plus a small constant time (up to 4 iterations)
    ///     to search through the alpha entries in the bucket.
    ///   - Insertion: O(1) for bucket index computation and a quick linear search over a very small (fixed) number of entries.
    /// </para>
    /// <para>
    /// Memory Characteristics:
    ///   - The cache consists of 32,768 buckets.
    ///   - Each <see cref="AlphaBucket"/> is implemented using an inline array with a capacity of 4 entries.
    ///   - Each bucket occupies approximately 18 bytes.
    ///   - Overall, the buckets occupy roughly 32,768 × 18 = 589,824 bytes (576 KB).
    /// </para>
    /// <para>
    /// This design provides nearly constant-time lookup and insertion with minimal memory usage,
    /// making it ideal for applications such as color distance caching in images with a limited palette (up to 256 entries).
    /// </para>
    /// </summary>
    internal sealed unsafe class CoarseCache : IDisposable
    {
        // Use 5 bits per channel for R, G, and B: 32 levels each.
        // Total buckets = 32^3 = 32768.
        private const int RgbBits = 5;
        private const int BucketCount = 1 << (RgbBits * 3); // 32768
        private readonly IMemoryOwner<AlphaBucket> bucketsOwner;
        private readonly AlphaBucket* buckets;
        private MemoryHandle bucketHandle;

        public CoarseCache(MemoryAllocator allocator)
        {
            this.bucketsOwner = allocator.Allocate<AlphaBucket>(BucketCount, AllocationOptions.Clean);
            this.bucketHandle = this.bucketsOwner.Memory.Pin();
            this.buckets = (AlphaBucket*)this.bucketHandle.Pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBucketIndex(byte r, byte g, byte b)
        {
            int qr = r >> (8 - RgbBits);
            int qg = g >> (8 - RgbBits);
            int qb = b >> (8 - RgbBits);

            // Combine the quantized channels into a single index.
            return (qr << (RgbBits * 2)) | (qg << RgbBits) | qb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte QuantizeAlpha(byte a)

            // Quantize to 6 bits: shift right by (8 - 6) = 2 bits.
            => (byte)(a >> 2);

        public void Add(Rgba32 color, short paletteIndex)
        {
            int bucketIndex = GetBucketIndex(color.R, color.G, color.B);
            byte quantAlpha = QuantizeAlpha(color.A);
            this.buckets[bucketIndex].Add(quantAlpha, paletteIndex);
        }

        public void Dispose()
        {
            this.bucketHandle.Dispose();
            this.bucketsOwner.Dispose();
        }

        public bool TryGetValue(Rgba32 color, out short paletteIndex)
        {
            int bucketIndex = GetBucketIndex(color.R, color.G, color.B);
            byte quantAlpha = QuantizeAlpha(color.A);
            return this.buckets[bucketIndex].TryGetValue(quantAlpha, out paletteIndex);
        }

        public void Clear()
        {
            Span<AlphaBucket> bucketsSpan = this.bucketsOwner.GetSpan();
            bucketsSpan.Clear();
        }

        public struct AlphaEntry
        {
            // Store the alpha value quantized to 6 bits (0..63)
            public byte QuantizedAlpha;
            public short PaletteIndex;
        }

        public struct AlphaBucket
        {
            // Fixed capacity for alpha entries in this bucket.
            // We choose a capacity of 4 for several reasons:
            //
            // 1. The alpha channel is quantized to 6 bits, so there are 64 possible distinct values.
            //    In the worst-case, a given RGB bucket might encounter up to 64 different alpha values.
            //
            // 2. However, in practice (based on probability theory and typical image data),
            //    the number of unique alpha values that actually occur for a given quantized RGB
            //    bucket is usually very small. If you randomly sample 4 values out of 64,
            //    the probability that these 4 samples are all unique is high if the distribution
            //    of alpha values is skewed or if only a few alpha values are used.
            //
            // 3. Statistically, for many real-world images, most RGB buckets will have only a couple
            //    of unique alpha values. Allocating 4 slots per bucket provides a good trade-off:
            //    it captures the common-case scenario while keeping overall memory usage low.
            //
            // 4. Even if more than 4 unique alpha values occur in a bucket,
            //    our design overwrites the first entry. This behavior gives us some "wriggle room"
            //    while preserving the most frequently encountered or most recent values.
            public const int Capacity = 4;
            public byte Count;
            private InlineArray4<AlphaEntry> entries;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetValue(byte quantizedAlpha, out short paletteIndex)
            {
                for (int i = 0; i < this.Count; i++)
                {
                    ref AlphaEntry entry = ref this.entries[i];
                    if (entry.QuantizedAlpha == quantizedAlpha)
                    {
                        paletteIndex = entry.PaletteIndex;
                        return true;
                    }
                }

                paletteIndex = -1;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(byte quantizedAlpha, short paletteIndex)
            {
                // Check for an existing entry with the same quantized alpha.
                for (int i = 0; i < this.Count; i++)
                {
                    ref AlphaEntry entry = ref this.entries[i];
                    if (entry.QuantizedAlpha == quantizedAlpha)
                    {
                        // Update palette index if found.
                        entry.PaletteIndex = paletteIndex;
                        return;
                    }
                }

                // If there's room, add a new entry.
                if (this.Count < Capacity)
                {
                    ref AlphaEntry newEntry = ref this.entries[this.Count];
                    newEntry.QuantizedAlpha = quantizedAlpha;
                    newEntry.PaletteIndex = paletteIndex;
                    this.Count++;
                }
                else
                {
                    // Bucket is full. Overwrite the first entry to give us some wriggle room.
                    this.entries[0].QuantizedAlpha = quantizedAlpha;
                    this.entries[0].PaletteIndex = paletteIndex;
                }
            }
        }
    }
}
