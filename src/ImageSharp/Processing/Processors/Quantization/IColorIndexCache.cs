// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Represents a cache used for efficiently retrieving palette indices for colors.
/// </summary>
internal interface IColorIndexCache : IDisposable
{
    /// <summary>
    /// Adds a color to the cache.
    /// </summary>
    /// <param name="color">The color to add.</param>
    /// <param name="value">The index of the color in the palette.</param>
    /// <returns>
    /// <see langword="true"/> if the color was added; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryAdd(Rgba32 color, short value);

    /// <summary>
    /// Gets the index of the color in the palette.
    /// </summary>
    /// <param name="color">The color to get the index for.</param>
    /// <param name="value">The index of the color in the palette.</param>
    /// <returns>
    /// <see langword="true"/> if the color is in the palette; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetValue(Rgba32 color, out short value);

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void Clear();
}

/// <summary>
/// Represents a cache used for efficiently retrieving palette indices for colors.
/// </summary>
/// <typeparam name="T">The type of the cache.</typeparam>
internal interface IColorIndexCache<T> : IColorIndexCache
   where T : struct, IColorIndexCache
{
    /// <summary>
    /// Creates a new instance of the cache.
    /// </summary>
    /// <param name="allocator">The memory allocator to use.</param>
    /// <returns>
    /// The new instance of the cache.
    /// </returns>
    public static abstract T Create(MemoryAllocator allocator);
}

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
internal unsafe struct HybridCache : IColorIndexCache<HybridCache>
{
    private CoarseCache coarseCache;
    private AccurateCache accurateCache;

    public HybridCache(MemoryAllocator allocator)
    {
        this.accurateCache = AccurateCache.Create(allocator);
        this.coarseCache = CoarseCache.Create(allocator);
    }

    /// <inheritdoc/>
    public static HybridCache Create(MemoryAllocator allocator) => new(allocator);

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public bool TryAdd(Rgba32 color, short index)
    {
        if (this.accurateCache.TryAdd(color, index))
        {
            return true;
        }

        return this.coarseCache.TryAdd(color, index);
    }

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly bool TryGetValue(Rgba32 color, out short value)
    {
        if (this.accurateCache.TryGetValue(color, out value))
        {
            return true;
        }

        return this.coarseCache.TryGetValue(color, out value);
    }

    /// <inheritdoc/>
    public readonly void Clear()
    {
        this.accurateCache.Clear();
        this.coarseCache.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.accurateCache.Dispose();
        this.coarseCache.Dispose();
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
internal unsafe struct CoarseCache : IColorIndexCache<CoarseCache>
{
    // Use 5 bits per channel for R, G, and B: 32 levels each.
    // Total buckets = 32^3 = 32768.
    private const int RgbBits = 5;
    private const int RgbShift = 8 - RgbBits; // 3
    private const int BucketCount = 1 << (RgbBits * 3); // 32768
    private readonly IMemoryOwner<AlphaBucket> bucketsOwner;
    private readonly AlphaBucket* buckets;
    private MemoryHandle bucketHandle;

    private CoarseCache(MemoryAllocator allocator)
    {
        this.bucketsOwner = allocator.Allocate<AlphaBucket>(BucketCount, AllocationOptions.Clean);
        this.bucketHandle = this.bucketsOwner.Memory.Pin();
        this.buckets = (AlphaBucket*)this.bucketHandle.Pointer;
    }

    /// <inheritdoc/>
    public static CoarseCache Create(MemoryAllocator allocator) => new(allocator);

    /// <inheritdoc/>
    public readonly bool TryAdd(Rgba32 color, short paletteIndex)
    {
        int bucketIndex = GetBucketIndex(color.R, color.G, color.B);
        byte quantAlpha = QuantizeAlpha(color.A);
        this.buckets[bucketIndex].Add(quantAlpha, paletteIndex);
        return true;
    }

    /// <inheritdoc/>
    public readonly bool TryGetValue(Rgba32 color, out short paletteIndex)
    {
        int bucketIndex = GetBucketIndex(color.R, color.G, color.B);
        byte quantAlpha = QuantizeAlpha(color.A);
        return this.buckets[bucketIndex].TryGetValue(quantAlpha, out paletteIndex);
    }

    /// <inheritdoc/>
    public readonly void Clear()
    {
        Span<AlphaBucket> bucketsSpan = this.bucketsOwner.GetSpan();
        bucketsSpan.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.bucketHandle.Dispose();
        this.bucketsOwner.Dispose();
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int GetBucketIndex(byte r, byte g, byte b)
    {
        int qr = r >> RgbShift;
        int qg = g >> RgbShift;
        int qb = b >> RgbShift;

        // Combine the quantized channels into a single index.
        return (qr << (RgbBits << 1)) | (qg << RgbBits) | qb;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static byte QuantizeAlpha(byte a)

        // Quantize to 6 bits: shift right by (8 - 6) = 2 bits.
        => (byte)(a >> 2);

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

        [MethodImpl(InliningOptions.ShortMethod)]
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

        [MethodImpl(InliningOptions.ShortMethod)]
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
internal unsafe struct AccurateCache : IColorIndexCache<AccurateCache>
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

    private AccurateCache(MemoryAllocator allocator)
    {
        this.Count = 0;

        // Allocate exactly 512 indexes for buckets.
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

    /// <inheritdoc/>
    public static AccurateCache Create(MemoryAllocator allocator) => new(allocator);

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public bool TryAdd(Rgba32 color, short value)
    {
        if (this.Count == Capacity)
        {
            return false; // Dictionary is full.
        }

        uint key = color.PackedValue;

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

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public bool TryGetValue(Rgba32 color, out short value)
    {
        uint key = color.PackedValue;
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
/// Represents a cache that does not store any values.
/// It allows adding colors, but always returns false when trying to retrieve them.
/// </summary>
internal readonly struct NullCache : IColorIndexCache<NullCache>
{
    /// <inheritdoc/>
    public static NullCache Create(MemoryAllocator allocator) => default;

    /// <inheritdoc/>
    public bool TryAdd(Rgba32 color, short value) => true;

    /// <inheritdoc/>
    public bool TryGetValue(Rgba32 color, out short value)
    {
        value = -1;
        return false;
    }

    /// <inheritdoc/>
    public void Clear()
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
