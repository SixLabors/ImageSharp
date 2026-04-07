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
///   - Lookup: O(1) for computing the bucket index from the RGB channels, plus a small constant time (up to 8 iterations)
///     to search through the alpha entries in the bucket.
///   - Insertion: O(1) for bucket index computation and a quick linear search over a very small (fixed) number of entries.
/// </para>
/// <para>
/// Memory Characteristics:
///   - The cache consists of 32,768 buckets.
///   - Each <see cref="AlphaBucket"/> is implemented using an inline array with a capacity of 8 entries.
///   - Each bucket occupies approximately 1 byte (Count) + (8 entries × 3 bytes each) ≈ 25 bytes.
///   - Overall, the buckets occupy roughly 32,768 × 25 bytes = 819,200 bytes (≈ 800 KB).
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
    private static byte QuantizeAlpha(byte a) => (byte)(a >> 2);

    public struct AlphaEntry
    {
        // Store the alpha value quantized to 6 bits (0..63).
        public byte QuantizedAlpha;
        public short PaletteIndex;
    }

    public struct AlphaBucket
    {
        // Fixed capacity for alpha entries in this bucket.
        // We choose a capacity of 8 for several reasons:
        //
        // 1. The alpha channel is quantized to 6 bits, so there are 64 possible distinct values.
        //    In the worst-case, a given RGB bucket might encounter up to 64 different alpha values.
        //
        // 2. However, in practice (based on probability theory and typical image data),
        //    the number of unique alpha values that actually occur for a given quantized RGB
        //    bucket is usually very small. If you randomly sample 8 values out of 64,
        //    the probability that these samples are all unique is high if the distribution
        //    of alpha values is skewed or if only a few alpha values are used.
        //
        // 3. Statistically, for many real-world images, most RGB buckets will have only a couple
        //    of unique alpha values. Allocating 8 slots per bucket provides a good trade-off:
        //    it captures the common-case scenario while keeping overall memory usage low.
        //
        // 4. Even if more than 8 unique alpha values occur in a bucket,
        //    our design overwrites the first entry. This behavior gives us some "wriggle room"
        //    while preserving the most frequently encountered or most recent values.
        public const int Capacity = 8;
        public byte Count;
        private InlineArray8<AlphaEntry> entries;

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
/// A fixed-size exact-match cache that stores packed RGBA keys with 4-way set associativity.
/// </summary>
/// <remarks>
/// The cache holds 512 total entries split across 128 sets. Entries are evicted within a set
/// using round-robin replacement, but cached values are returned only when the full packed RGBA
/// key matches, preserving exact quantization results with predictable memory usage.
/// The overall memory usage is approximately 4–5 KB. Both lookup and insertion operations are,
/// on average, O(1) since each lookup probes at most four candidate entries within the selected set.
/// This guarantees highly efficient and predictable performance for small, fixed-size color palettes.
/// </remarks>
internal unsafe struct AccurateCache : IColorIndexCache<AccurateCache>
{
    public const int Capacity = 512;
    private const int Ways = 4;
    private const int SetCount = Capacity / Ways;
    private const int SetMask = SetCount - 1;

    private readonly IMemoryOwner<uint> keysOwner;
    private MemoryHandle keysHandle;
    private uint* keys;

    private readonly IMemoryOwner<ushort> valuesOwner;
    private MemoryHandle valuesHandle;
    private ushort* values;

    private readonly IMemoryOwner<byte> nextVictimOwner;
    private MemoryHandle nextVictimHandle;
    private byte* nextVictim;

    private AccurateCache(MemoryAllocator allocator)
    {
        this.keysOwner = allocator.Allocate<uint>(Capacity, AllocationOptions.Clean);
        this.keysHandle = this.keysOwner.Memory.Pin();
        this.keys = (uint*)this.keysHandle.Pointer;

        this.valuesOwner = allocator.Allocate<ushort>(Capacity, AllocationOptions.Clean);
        this.valuesHandle = this.valuesOwner.Memory.Pin();
        this.values = (ushort*)this.valuesHandle.Pointer;

        this.nextVictimOwner = allocator.Allocate<byte>(SetCount, AllocationOptions.Clean);
        this.nextVictimHandle = this.nextVictimOwner.Memory.Pin();
        this.nextVictim = (byte*)this.nextVictimHandle.Pointer;
    }

    /// <inheritdoc/>
    public static AccurateCache Create(MemoryAllocator allocator) => new(allocator);

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public bool TryAdd(Rgba32 color, short value)
    {
        uint key = color.PackedValue;
        int set = GetSetIndex(key);
        int start = set * Ways;
        int empty = -1;

        uint* keys = this.keys;
        ushort* values = this.values;
        ushort storedValue = (ushort)(value + 1);

        for (int i = start; i < start + Ways; i++)
        {
            ushort candidate = values[i];
            if (candidate == 0)
            {
                empty = i;
                continue;
            }

            if (keys[i] == key)
            {
                values[i] = storedValue;
                return true;
            }
        }

        int slot = empty >= 0 ? empty : start + this.nextVictim[set];
        keys[slot] = key;
        values[slot] = storedValue;

        if (empty < 0)
        {
            this.nextVictim[set] = (byte)((this.nextVictim[set] + 1) & (Ways - 1));
        }

        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly bool TryGetValue(Rgba32 color, out short value)
    {
        uint key = color.PackedValue;
        int start = GetSetIndex(key) * Ways;

        uint* keys = this.keys;
        ushort* values = this.values;

        for (int i = start; i < start + Ways; i++)
        {
            ushort candidate = values[i];
            if (candidate != 0 && keys[i] == key)
            {
                value = (short)(candidate - 1);
                return true;
            }
        }

        value = -1;
        return false;
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public readonly void Clear()
    {
        this.valuesOwner.GetSpan().Clear();
        this.nextVictimOwner.GetSpan().Clear();
    }

    public void Dispose()
    {
        this.keysHandle.Dispose();
        this.keysOwner.Dispose();
        this.valuesHandle.Dispose();
        this.valuesOwner.Dispose();
        this.nextVictimHandle.Dispose();
        this.nextVictimOwner.Dispose();
        this.keys = null;
        this.values = null;
        this.nextVictim = null;
    }

    /// <summary>
    /// Maps a packed RGBA key to one of the cache sets used by <see cref="AccurateCache"/>.
    /// </summary>
    /// <param name="key">The packed <see cref="Rgba32.PackedValue"/> key.</param>
    /// <returns>The zero-based set index for the key.</returns>
    /// <remarks>
    /// <para>
    /// The cache is 4-way set-associative, so this hash only needs to choose one of
    /// <see cref="SetCount"/> sets before probing up to four candidate entries.
    /// </para>
    /// <para>
    /// <see cref="Rgba32.PackedValue"/> is laid out as <c>R | (G &lt;&lt; 8) | (B &lt;&lt; 16) | (A &lt;&lt; 24)</c>.
    /// The XOR-fold mixes neighboring bytes into the low bits, and the final mask selects the
    /// set. With the current 128-set layout that makes the selected set effectively depend on
    /// the low 7 bits of <c>R ^ G ^ B</c>. Alpha still participates in the later exact key
    /// comparison, but not in set selection.
    /// </para>
    /// <para>
    /// Collisions are expected and acceptable here. Correctness comes from the full packed-key
    /// comparison during probing; this hash only aims to spread keys cheaply enough that each
    /// access touches at most one 4-entry set.
    /// </para>
    /// </remarks>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static int GetSetIndex(uint key)
        => (int)(((key >> 16) ^ (key >> 8) ^ key) & SetMask);
}
