// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Quantization;

[Trait("Category", "Processors")]
public class PaletteQuantizerTests
{
    private static readonly Color[] Palette = [Color.Red, Color.Green, Color.Blue];

    [Fact]
    public void PaletteQuantizerConstructor()
    {
        QuantizerOptions expected = new() { MaxColors = 128 };
        PaletteQuantizer quantizer = new(Palette, expected);

        Assert.Equal(expected.MaxColors, quantizer.Options.MaxColors);
        Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);

        expected = new QuantizerOptions { Dither = null };
        quantizer = new PaletteQuantizer(Palette, expected);
        Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
        Assert.Null(quantizer.Options.Dither);

        expected = new QuantizerOptions { Dither = KnownDitherings.Atkinson };
        quantizer = new PaletteQuantizer(Palette, expected);
        Assert.Equal(QuantizerConstants.MaxColors, quantizer.Options.MaxColors);
        Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);

        expected = new QuantizerOptions { Dither = KnownDitherings.Atkinson, MaxColors = 0 };
        quantizer = new PaletteQuantizer(Palette, expected);
        Assert.Equal(QuantizerConstants.MinColors, quantizer.Options.MaxColors);
        Assert.Equal(KnownDitherings.Atkinson, quantizer.Options.Dither);
    }

    [Fact]
    public void PaletteQuantizerCanCreateFrameQuantizer()
    {
        PaletteQuantizer quantizer = new(Palette);
        IQuantizer<Rgba32> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

        Assert.NotNull(frameQuantizer);
        Assert.NotNull(frameQuantizer.Options);
        Assert.Equal(QuantizerConstants.DefaultDither, frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();

        quantizer = new PaletteQuantizer(Palette, new QuantizerOptions { Dither = null });
        frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);

        Assert.NotNull(frameQuantizer);
        Assert.Null(frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();

        quantizer = new PaletteQuantizer(Palette, new QuantizerOptions { Dither = KnownDitherings.Atkinson });
        frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(Configuration.Default);
        Assert.NotNull(frameQuantizer);
        Assert.Equal(KnownDitherings.Atkinson, frameQuantizer.Options.Dither);
        frameQuantizer.Dispose();
    }

    [Fact]
    public void KnownQuantizersWebSafeTests()
    {
        IQuantizer quantizer = KnownQuantizers.WebSafe;
        Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);
    }

    [Fact]
    public void KnownQuantizersWernerTests()
    {
        IQuantizer quantizer = KnownQuantizers.Werner;
        Assert.Equal(QuantizerConstants.DefaultDither, quantizer.Options.Dither);
    }

    [Fact]
    public void ExactColorMatchingMatchesUncachedAfterCacheOverflow()
    {
        Rgba32[] palette =
        [
            new Rgba32(0, 0, 0),
            new Rgba32(7, 0, 0)
        ];

        using PixelMap<Rgba32> exact = CreatePixelMap<UncachedCache>(palette);
        using PixelMap<Rgba32> cachedExact = CreatePixelMap<AccurateCache>(palette);

        for (int i = 0; i < AccurateCache.Capacity; i++)
        {
            cachedExact.GetClosestColor(CreateOverflowFillerColor(i), out _);
        }

        Rgba32 first = new(1, 0, 0);
        Rgba32 second = new(6, 0, 0);

        AssertMatchesUncached(exact, cachedExact, first);
        AssertMatchesUncached(exact, cachedExact, second);
    }

    [Fact]
    public void ExactColorMatchingMatchesUncachedAcrossManyProbeBinsAfterRepeatedEviction()
    {
        Rgba32[] palette = CreateGrayscalePalette(256);

        using PixelMap<Rgba32> exact = CreatePixelMap<UncachedCache>(palette);
        using PixelMap<Rgba32> cachedExact = CreatePixelMap<AccurateCache>(palette);

        for (int i = 0; i < AccurateCache.Capacity * 2; i++)
        {
            cachedExact.GetClosestColor(CreateEvictionFillerColor(i), out _);
        }

        for (int i = 0; i < AccurateCache.Capacity; i++)
        {
            AssertMatchesUncached(exact, cachedExact, CreateEvictionProbeColor(i));
        }
    }

    [Fact]
    public void ExactColorMatchingMatchesUncachedForDitherStressColorSequence()
    {
        Rgba32[] palette = CreateGrayscalePalette(16);

        using Image<Rgba32> source = CreateDitherStressImage();
        using PixelMap<Rgba32> exact = CreatePixelMap<UncachedCache>(palette);
        using PixelMap<Rgba32> cachedExact = CreatePixelMap<AccurateCache>(palette);

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                AssertMatchesUncached(exact, cachedExact, source[x, y]);
            }
        }
    }

    // Split the first 512 integers across R and G so the warmup loop produces 512 distinct exact colors:
    // the low 8 bits go into R, and the ninth bit spills into G once R wraps after 255.
    // Keeping B fixed and G offset away from zero also avoids accidentally probing the red-axis test colors below.
    private static Rgba32 CreateOverflowFillerColor(int i)
        => new((byte)i, (byte)(16 + (i >> 8)), 32);

    // Treat i as three packed 5-bit coordinates and expand each coordinate back to an 8-bit channel by
    // shifting left by 3. That lands on the lower edge of each 5-bit coarse bucket, giving the test a
    // deterministic way to fill many distinct coarse buckets before probing nearby exact colors.
    private static Rgba32 CreateEvictionFillerColor(int i)
    {
        byte r = (byte)((i & 31) << 3);
        byte g = (byte)(((i >> 5) & 31) << 3);
        byte b = (byte)(((i >> 10) & 31) << 3);
        return new(r, g, b);
    }

    // Reconstruct the same 5-bit RGB bucket coordinates used by CreateEvictionFillerColor, then set the
    // low 3 bits in each channel to 0b111. That keeps the probe inside the same coarse bucket while making
    // it a different exact color, which is the shape that used to expose coarse-fallback false hits.
    private static Rgba32 CreateEvictionProbeColor(int i)
    {
        byte r = (byte)(((i & 31) << 3) | 0x07);
        byte g = (byte)((((i >> 5) & 31) << 3) | 0x07);
        byte b = (byte)((((i >> 10) & 31) << 3) | 0x07);
        return new(r, g, b);
    }

    private static PixelMap<Rgba32> CreatePixelMap<TCache>(Rgba32[] palette)
        where TCache : struct, IColorIndexCache<TCache>
        => new EuclideanPixelMap<Rgba32, TCache>(Configuration.Default, palette);

    private static void AssertMatchesUncached(PixelMap<Rgba32> exact, PixelMap<Rgba32> cachedExact, Rgba32 color)
    {
        int exactIndex = exact.GetClosestColor(color, out Rgba32 exactMatch);
        int cachedIndex = cachedExact.GetClosestColor(color, out Rgba32 cachedMatch);

        Assert.Equal(exactIndex, cachedIndex);
        Assert.Equal(exactMatch, cachedMatch);
    }

    private static Rgba32[] CreateGrayscalePalette(int count)
    {
        Rgba32[] palette = new Rgba32[count];
        for (int i = 0; i < count; i++)
        {
            byte value = count == 1 ? (byte)0 : (byte)((i * 255) / (count - 1));
            palette[i] = new Rgba32(value, value, value);
        }

        return palette;
    }

    // Generate a deterministic pseudo-image where each channel uses a different x/y slope.
    // Neighboring pixels stay correlated, like real image content, but the combined RGB values
    // churn heavily enough that exact repeats are rare. That makes this a useful stress input
    // for verifying cached exact matching against an uncached baseline under dither-like access.
    private static Image<Rgba32> CreateDitherStressImage()
    {
        Image<Rgba32> image = new(192, 96);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                image[x, y] = new Rgba32(
                    (byte)((x * 17) + (y * 13)),
                    (byte)((x * 29) + (y * 7)),
                    (byte)((x * 11) + (y * 23)));
            }
        }

        return image;
    }

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
