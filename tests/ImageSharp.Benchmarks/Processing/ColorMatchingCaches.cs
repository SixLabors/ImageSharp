// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Benchmarks.Processing;

[Config(typeof(Config.Standard))]
public class ColorMatchingCaches
{
    // IterationSetup forces BenchmarkDotNet to use a single benchmark invocation per iteration.
    // Repeated lookups can safely replay a smaller working set because that workload is explicitly
    // meant to model steady-state cache hits after warmup.
    private const int RepeatedLookupCount = 262_144;

    // DitherLike should avoid replaying the same stream across multiple passes because that warms
    // the caches in a way real high-churn input usually does not. Make the single pass larger instead.
    private const int DitherLikeLookupCount = 1_048_576;
    private const int RepeatedPassCount = 16;

    private Rgba32[] palette;
    private Rgba32[] repeatedSeedColors;
    private Rgba32[] repeatedLookups;
    private Rgba32[] ditherLookups;

    private PixelMap<Rgba32> coarse;
    private PixelMap<Rgba32> legacyCoarse;
    private PixelMap<Rgba32> exact;
    private PixelMap<Rgba32> uncached;

    [Params(16, 256)]
    public int PaletteSize { get; set; }

    [Params(CacheWorkload.Repeated, CacheWorkload.DitherLike)]
    public CacheWorkload Workload { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.palette = CreatePalette(this.PaletteSize);
        this.repeatedSeedColors = CreateRepeatedSeedColors(this.palette);
        this.repeatedLookups = CreateRepeatedLookups(this.repeatedSeedColors);
        this.ditherLookups = CreateDitherLikeLookups();

        this.coarse = CreatePixelMap<CoarseCache>(this.palette);
        this.legacyCoarse = CreatePixelMap<LegacyCoarseCache>(this.palette);
        this.exact = CreatePixelMap<AccurateCache>(this.palette);
        this.uncached = CreatePixelMap<UncachedCache>(this.palette);
    }

    [IterationSetup]
    public void ResetCaches()
    {
        // Each benchmark iteration should start from the same cache state so we measure
        // the cache policy itself rather than warm state carried over from a previous iteration.
        this.coarse.Clear(this.palette);
        this.legacyCoarse.Clear(this.palette);
        this.exact.Clear(this.palette);
        this.uncached.Clear(this.palette);

        if (this.Workload == CacheWorkload.Repeated)
        {
            // Prime the repeated workload so the benchmark reflects steady-state hit behavior
            // instead of mostly measuring the first-wave fill cost.
            Prime(this.coarse, this.repeatedSeedColors);
            Prime(this.legacyCoarse, this.repeatedSeedColors);
            Prime(this.exact, this.repeatedSeedColors);
            Prime(this.uncached, this.repeatedSeedColors);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.coarse.Dispose();
        this.legacyCoarse.Dispose();
        this.exact.Dispose();
        this.uncached.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Coarse")]
    public int Coarse() => this.Run(this.coarse);

    [Benchmark(Description = "Legacy Coarse")]
    public int LegacyCoarse() => this.Run(this.legacyCoarse);

    [Benchmark(Description = "Exact Cached")]
    public int Exact() => this.Run(this.exact);

    [Benchmark(Description = "Exact Uncached")]
    public int Uncached() => this.Run(this.uncached);

    public enum CacheWorkload
    {
        // A small working set that is intentionally reused after priming to measure hit-heavy behavior.
        Repeated,

        // A deterministic high-churn stream intended to resemble dithered lookups where exact repeats are rare.
        DitherLike
    }

    private int Run(PixelMap<Rgba32> map)
    {
        Rgba32[] lookups = this.Workload == CacheWorkload.Repeated ? this.repeatedLookups : this.ditherLookups;
        int passCount = this.Workload == CacheWorkload.Repeated ? RepeatedPassCount : 1;
        int checksum = 0;

        // Repeated intentionally replays the same lookup stream to measure steady-state hit behavior.
        // DitherLike runs as a single larger pass so we do not turn a churn-heavy workload into an
        // artificially warmed cache benchmark by replaying the exact same sequence.
        for (int pass = 0; pass < passCount; pass++)
        {
            for (int i = 0; i < lookups.Length; i++)
            {
                checksum = unchecked((checksum * 31) + map.GetClosestColor(lookups[i], out _));
            }
        }

        return checksum;
    }

    private static PixelMap<Rgba32> CreatePixelMap<TCache>(Rgba32[] palette)
        where TCache : struct, IColorIndexCache<TCache>
        => new EuclideanPixelMap<Rgba32, TCache>(Configuration.Default, palette);

    private static void Prime(PixelMap<Rgba32> map, Rgba32[] colors)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            map.GetClosestColor(colors[i], out _);
        }
    }

    private static Rgba32[] CreatePalette(int count)
    {
        Rgba32[] result = new Rgba32[count];

        for (int i = 0; i < result.Length; i++)
        {
            // Use the Knuth/golden-ratio multiplicative hash constant to spread colors across
            // RGBA space without clustering into a gradient. That keeps the benchmark from
            // accidentally favoring any cache because the palette itself is too regular.
            uint value = unchecked((uint)(i + 1) * 2654435761U);
            result[i] = new(
                (byte)value,
                (byte)(value >> 8),
                (byte)(value >> 16),
                (byte)((value >> 24) | 0x80));
        }

        return result;
    }

    private static Rgba32[] CreateRepeatedSeedColors(Rgba32[] palette)
    {
        // Reuse colors derived from the palette but perturb them slightly so the workload still
        // exercises nearest-color matching rather than only exact palette-entry hits.
        int count = Math.Min(64, palette.Length * 2);
        Rgba32[] result = new Rgba32[count];

        for (int i = 0; i < result.Length; i++)
        {
            Rgba32 source = palette[(i * 17) % palette.Length];
            result[i] = new(
                (byte)(source.R + ((i * 3) & 0x07)),
                (byte)(source.G + ((i * 5) & 0x07)),
                (byte)(source.B + ((i * 7) & 0x07)),
                source.A);
        }

        return result;
    }

    private static Rgba32[] CreateRepeatedLookups(Rgba32[] seedColors)
    {
        Rgba32[] result = new Rgba32[RepeatedLookupCount];

        // Cycle a small seed set to produce a stable, hit-heavy stream after priming.
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = seedColors[i % seedColors.Length];
        }

        return result;
    }

    private static Rgba32[] CreateDitherLikeLookups()
    {
        Rgba32[] result = new Rgba32[DitherLikeLookupCount];

        // Generate a deterministic pseudo-image signal with independent channel slopes so nearby
        // samples are correlated but exact repeats are uncommon, which is closer to dithered input.
        for (int i = 0; i < result.Length; i++)
        {
            int x = i & 511;
            int y = i >> 9;

            result[i] = new(
                (byte)((x * 17) + (y * 13)),
                (byte)((x * 29) + (y * 7)),
                (byte)((x * 11) + (y * 23)),
                (byte)(255 - ((x * 3) + (y * 5))));
        }

        return result;
    }

    /// <summary>
    /// Preserves the original direct-mapped coarse cache implementation for side-by-side benchmarks.
    /// </summary>
    private unsafe struct LegacyCoarseCache : IColorIndexCache<LegacyCoarseCache>
    {
        private const int IndexRBits = 5;
        private const int IndexGBits = 5;
        private const int IndexBBits = 5;
        private const int IndexABits = 6;
        private const int IndexRCount = 1 << IndexRBits;
        private const int IndexGCount = 1 << IndexGBits;
        private const int IndexBCount = 1 << IndexBBits;
        private const int IndexACount = 1 << IndexABits;
        private const int TotalBins = IndexRCount * IndexGCount * IndexBCount * IndexACount;

        private readonly IMemoryOwner<short> binsOwner;
        private readonly short* binsPointer;
        private MemoryHandle binsHandle;

        private LegacyCoarseCache(MemoryAllocator allocator)
        {
            this.binsOwner = allocator.Allocate<short>(TotalBins);
            this.binsOwner.GetSpan().Fill(-1);
            this.binsHandle = this.binsOwner.Memory.Pin();
            this.binsPointer = (short*)this.binsHandle.Pointer;
        }

        public static LegacyCoarseCache Create(MemoryAllocator allocator) => new(allocator);

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
            return value > -1;
        }

        public readonly void Clear() => this.binsOwner.GetSpan().Fill(-1);

        public void Dispose()
        {
            this.binsHandle.Dispose();
            this.binsOwner.Dispose();
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
    }

    /// <summary>
    /// Preserves the uncached path for exact-cache comparison benchmarks.
    /// </summary>
    private readonly struct UncachedCache : IColorIndexCache<UncachedCache>
    {
        public static UncachedCache Create(MemoryAllocator allocator) => default;

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
}
