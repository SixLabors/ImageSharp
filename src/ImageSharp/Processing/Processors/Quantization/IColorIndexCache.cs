// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

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
#pragma warning disable IDE0060 // Remove unused parameter
    [RequiresPreviewFeatures]
    public static abstract T Create(MemoryAllocator allocator);
#pragma warning restore IDE0060 // Remove unused parameter
}

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
/// A hybrid cache for color distance lookups that combines an exact-match dictionary with
/// a fallback coarse lookup table.
/// </summary>
/// <remarks>
/// This cache uses a fallback table with  2,097,152 bins, each storing a 2-byte value
/// (approximately 4 MB total), while the exact-match dictionary is limited to 512 entries
/// and occupies roughly 4 KB. Overall, the worst-case memory usage is about 4 MB.
/// Lookups and insertions are performed in constant time (O(1)) because the fallback table
/// is accessed via direct indexing and the dictionary employs a simple hash-based bucket mechanism.
/// The design achieves extremely fast color distance lookups with a predictable, fixed memory footprint.
/// </remarks>
internal unsafe struct HybridCache : IColorIndexCache<HybridCache>
{
    private AccurateCache accurateCache;
    private CoarseCache coarseCache;

    [RequiresPreviewFeatures]
    private HybridCache(MemoryAllocator allocator)
    {
        this.accurateCache = AccurateCache.Create(allocator);
        this.coarseCache = CoarseCache.Create(allocator);
    }

    [RequiresPreviewFeatures]
    public static HybridCache Create(MemoryAllocator allocator) => new(allocator);

    [MethodImpl(InliningOptions.ShortMethod)]
    public bool TryAdd(Rgba32 color, short index)
    {
        if (this.accurateCache.TryAdd(color, index))
        {
            return true;
        }

        return this.coarseCache.TryAdd(color, index);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public bool TryGetValue(Rgba32 color, out short value)
    {
        if (this.accurateCache.TryGetValue(color, out value))
        {
            return true;
        }

        return this.coarseCache.TryGetValue(color, out value);
    }

    public void Clear()
    {
        this.accurateCache.Clear();
        this.coarseCache.Clear();
    }

    public void Dispose()
    {
        this.accurateCache.Dispose();
        this.coarseCache.Dispose();
    }
}

/// <summary>
/// A coarse cache for color distance lookups that uses a fixed-size lookup table.
/// </summary>
/// <remarks>
/// This cache uses a fixed lookup table with 2,097,152 bins, each storing a 2-byte value,
/// resulting in a worst-case memory usage of approximately 4 MB. Lookups and insertions are
/// performed in constant time (O(1)) via direct table indexing. This design is optimized for
/// speed while maintaining a predictable, fixed memory footprint.
/// </remarks>
internal unsafe struct CoarseCache : IColorIndexCache<CoarseCache>
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

    private readonly IMemoryOwner<short> binsOwner;
    private readonly short* binsPointer;
    private MemoryHandle binsHandle;

    private CoarseCache(MemoryAllocator allocator)
    {
        this.binsOwner = allocator.Allocate<short>(TotalBins);
        this.binsOwner.GetSpan().Fill(-1);
        this.binsHandle = this.binsOwner.Memory.Pin();
        this.binsPointer = (short*)this.binsHandle.Pointer;
    }

    [RequiresPreviewFeatures]
    public static CoarseCache Create(MemoryAllocator allocator) => new(allocator);

    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly bool TryAdd(Rgba32 color, short value)
    {
        this.binsPointer[GetCoarseIndex(color)] = value;
        return true;
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly bool TryGetValue(Rgba32 color, out short value)
    {
        value = this.binsPointer[GetCoarseIndex(color)];
        return value > -1; // Coarse match found
    }

    [MethodImpl(InliningOptions.ShortMethod)]
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
        => this.binsOwner.GetSpan().Fill(-1);

    public void Dispose()
    {
        this.binsHandle.Dispose();
        this.binsOwner.Dispose();
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

    private int count;

    public const int Capacity = 512;

    private AccurateCache(MemoryAllocator allocator)
    {
        this.count = 0;

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

    [RequiresPreviewFeatures]
    public static AccurateCache Create(MemoryAllocator allocator) => new(allocator);

    public bool TryAdd(Rgba32 color, short value)
    {
        if (this.count == Capacity)
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

        short index = (short)this.count;
        this.count++;

        // Insert the new entry:
        entries[index].Key = key;
        entries[index].Value = value;

        // Link this new entry into the bucket chain.
        entries[index].Next = this.buckets[bucket];
        this.buckets[bucket] = index;
        return true;
    }

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
        this.count = 0;
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

internal readonly struct NullCache : IColorIndexCache<NullCache>
{
    [RequiresPreviewFeatures]
    public static NullCache Create(MemoryAllocator allocator) => default;

    public bool TryAdd(Rgba32 color, short value) => true;

    public bool TryGetValue(Rgba32 color, out short value)
    {
        value = -1;
        return false;
    }

    public void Clear()
    {
    }

    public void Dispose()
    {
    }
}
