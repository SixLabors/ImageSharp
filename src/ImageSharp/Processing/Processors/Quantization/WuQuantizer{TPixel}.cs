// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// An implementation of Wu's color quantizer with alpha channel.
/// </summary>
/// <remarks>
/// <para>
/// Based on C Implementation of Xiaolin Wu's Color Quantizer (v. 2)
/// (see Graphics Gems volume II, pages 126-133)
/// (<see href="http://www.ece.mcmaster.ca/~xwu/cq.c"/>).
/// </para>
/// <para>
/// This adaptation is based on the excellent JeremyAnsel.ColorQuant by Jérémy Ansel
/// <see href="https://github.com/JeremyAnsel/JeremyAnsel.ColorQuant"/>
/// </para>
/// <para>
/// Algorithm: Greedy orthogonal bipartition of RGB space for variance minimization aided by inclusion-exclusion tricks.
/// For speed no nearest neighbor search is done. Slightly better performance can be expected by more sophisticated
/// but more expensive versions.
/// </para>
/// </remarks>
/// <typeparam name="TPixel">The pixel format.</typeparam>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "https://github.com/dotnet/roslyn-analyzers/issues/6151")]
internal struct WuQuantizer<TPixel> : IQuantizer<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly MemoryAllocator memoryAllocator;

    // The following two variables determine the amount of bits to preserve when calculating the histogram.
    // Reducing the value of these numbers the granularity of the color maps produced, making it much faster
    // and using much less memory but potentially less accurate. Current results are very good though!
    private const int IndexBits = 5;
    private const int IndexAlphaBits = 5;
    private const int IndexCount = (1 << IndexBits) + 1;
    private const int IndexAlphaCount = (1 << IndexAlphaBits) + 1;
    private const int TableLength = IndexCount * IndexCount * IndexCount * IndexAlphaCount;

    private readonly IMemoryOwner<Moment> momentsOwner;
    private readonly IMemoryOwner<byte> tagsOwner;
    private readonly IMemoryOwner<TPixel> paletteOwner;
    private ReadOnlyMemory<TPixel> palette;
    private int maxColors;
    private readonly Box[] colorCube;
    private PixelMap<TPixel>? pixelMap;
    private readonly bool isDithering;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WuQuantizer{TPixel}"/> struct.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behavior or extending the library.</param>
    /// <param name="options">The quantizer options defining quantization rules.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public WuQuantizer(Configuration configuration, QuantizerOptions options)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(options, nameof(options));

        this.Configuration = configuration;
        this.Options = options;
        this.maxColors = this.Options.MaxColors;
        this.memoryAllocator = this.Configuration.MemoryAllocator;
        this.momentsOwner = this.memoryAllocator.Allocate<Moment>(TableLength, AllocationOptions.Clean);
        this.tagsOwner = this.memoryAllocator.Allocate<byte>(TableLength, AllocationOptions.Clean);
        this.paletteOwner = this.memoryAllocator.Allocate<TPixel>(this.maxColors, AllocationOptions.Clean);
        this.colorCube = new Box[this.maxColors];
        this.isDisposed = false;
        this.pixelMap = default;
        this.palette = default;
        this.isDithering = this.Options.Dither is not null;
    }

    /// <inheritdoc/>
    public Configuration Configuration { get; }

    /// <inheritdoc/>
    public QuantizerOptions Options { get; }

    /// <inheritdoc/>
    public ReadOnlyMemory<TPixel> Palette
    {
        get
        {
            if (this.palette.IsEmpty)
            {
                this.ResolvePalette();
                QuantizerUtilities.CheckPaletteState(in this.palette);
            }

            return this.palette;
        }
    }

    /// <inheritdoc/>
    public readonly void AddPaletteColors(in Buffer2DRegion<TPixel> pixelRegion)
    {
        PixelRowDelegate pixelRowDelegate = new(ref Unsafe.AsRef(in this));
        QuantizerUtilities.AddPaletteColors<WuQuantizer<TPixel>, TPixel, Rgba32, PixelRowDelegate>(
            ref Unsafe.AsRef(in this),
            in pixelRegion,
            in pixelRowDelegate);
    }

    /// <summary>
    /// Once all histogram data has been accumulated, this method computes the moments,
    /// splits the color cube, and resolves the final palette from the accumulated histogram.
    /// </summary>
    private void ResolvePalette()
    {
        // Calculate the cumulative moments from the accumulated histogram.
        this.Get3DMoments(this.memoryAllocator);

        // Partition the histogram into color cubes.
        this.BuildCube();

        // Compute the palette colors from the resolved cubes.
        Span<TPixel> paletteSpan = this.paletteOwner.GetSpan()[..this.maxColors];
        ReadOnlySpan<Moment> momentsSpan = this.momentsOwner.GetSpan();

        float transparencyThreshold = this.Options.TransparencyThreshold;
        for (int k = 0; k < paletteSpan.Length; k++)
        {
            this.Mark(ref this.colorCube[k], (byte)k);
            Moment moment = Volume(ref this.colorCube[k], momentsSpan);
            if (moment.Weight > 0)
            {
                Vector4 normalized = moment.Normalize();
                if (normalized.W < transparencyThreshold)
                {
                    normalized = Vector4.Zero;
                }

                paletteSpan[k] = TPixel.FromScaledVector4(normalized);
            }
        }

        // Update the palette to the new computed colors.
        this.palette = this.paletteOwner.Memory[..paletteSpan.Length];

        // Create the pixel map if dithering is enabled.
        if (this.isDithering && this.pixelMap is null)
        {
            this.pixelMap = PixelMapFactory.Create(this.Configuration, this.palette, this.Options.ColorMatchingMode);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly IndexedImageFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> source, Rectangle bounds)
        => QuantizerUtilities.QuantizeFrame(ref Unsafe.AsRef(in this), source, bounds);

    /// <inheritdoc/>
    public readonly byte GetQuantizedColor(TPixel color, out TPixel match)
    {
        // Due to the addition of new colors by dithering that are not part of the original histogram,
        // the color cube might not match the correct color.
        // In this case, we must use the pixel map to get the closest color.
        if (this.isDithering)
        {
            return (byte)this.pixelMap!.GetClosestColor(color, out match);
        }

        Rgba32 rgba = color.ToRgba32();

        const int shift = 8 - IndexBits;
        int r = rgba.R >> shift;
        int g = rgba.G >> shift;
        int b = rgba.B >> shift;
        int a = rgba.A >> (8 - IndexAlphaBits);

        ReadOnlySpan<byte> tagSpan = this.tagsOwner.GetSpan();
        byte index = tagSpan[GetPaletteIndex(r + 1, g + 1, b + 1, a + 1)];
        ref TPixel paletteRef = ref MemoryMarshal.GetReference(this.palette.Span);
        match = Unsafe.Add(ref paletteRef, (nuint)index);
        return index;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.isDisposed)
        {
            this.isDisposed = true;
            this.momentsOwner?.Dispose();
            this.tagsOwner?.Dispose();
            this.paletteOwner?.Dispose();
            this.pixelMap?.Dispose();
            this.pixelMap = null;
        }
    }

    /// <summary>
    /// Gets the index of the given color in the palette.
    /// </summary>
    /// <param name="r">The red value.</param>
    /// <param name="g">The green value.</param>
    /// <param name="b">The blue value.</param>
    /// <param name="a">The alpha value.</param>
    /// <returns>The index.</returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static int GetPaletteIndex(int r, int g, int b, int a)
        => (r << ((IndexBits * 2) + IndexAlphaBits))
        + (r << (IndexBits + IndexAlphaBits + 1))
        + (g << (IndexBits + IndexAlphaBits))
        + (r << (IndexBits * 2))
        + (r << (IndexBits + 1))
        + (g << IndexBits)
        + ((r + g + b) << IndexAlphaBits)
        + r + g + b + a;

    /// <summary>
    /// Computes sum over a box of any given statistic.
    /// </summary>
    /// <param name="cube">The cube.</param>
    /// <param name="moments">The moment.</param>
    /// <returns>The result.</returns>
    private static Moment Volume(ref Box cube, ReadOnlySpan<Moment> moments)
        => moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMax)]
        - moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMin)]
        - moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMax)]
        + moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
        - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMax)]
        + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
        + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
        - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
        - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMax)]
        + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
        + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
        - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
        + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
        - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
        - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
        + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

    /// <summary>
    /// Computes part of Volume(cube, moment) that doesn't depend on RMax, GMax, BMax, or AMax (depending on direction).
    /// </summary>
    /// <param name="cube">The cube.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="moments">The moment.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Invalid direction.</exception>
    private static Moment Bottom(ref Box cube, int direction, ReadOnlySpan<Moment> moments)
        => direction switch
        {
            // Red
            3 => -moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMax)]
                + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
                + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
                - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
                - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)],

            // Green
            2 => -moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMax)]
                + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
                + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
                - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
                - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)],

            // Blue
            1 => -moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMax)]
                + moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
                + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
                - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
                - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
                + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)],

            // Alpha
            0 => -moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMin)]
                + moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
                + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
                - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
                + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
                - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
                - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
                + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)],
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

    /// <summary>
    /// Computes remainder of Volume(cube, moment), substituting position for RMax, GMax, BMax, or AMax (depending on direction).
    /// </summary>
    /// <param name="cube">The cube.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="position">The position.</param>
    /// <param name="moments">The moment.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Invalid direction.</exception>
    private static Moment Top(ref Box cube, int direction, int position, ReadOnlySpan<Moment> moments)
        => direction switch
        {
            // Red
            3 => moments[GetPaletteIndex(position, cube.GMax, cube.BMax, cube.AMax)]
               - moments[GetPaletteIndex(position, cube.GMax, cube.BMax, cube.AMin)]
               - moments[GetPaletteIndex(position, cube.GMax, cube.BMin, cube.AMax)]
               + moments[GetPaletteIndex(position, cube.GMax, cube.BMin, cube.AMin)]
               - moments[GetPaletteIndex(position, cube.GMin, cube.BMax, cube.AMax)]
               + moments[GetPaletteIndex(position, cube.GMin, cube.BMax, cube.AMin)]
               + moments[GetPaletteIndex(position, cube.GMin, cube.BMin, cube.AMax)]
               - moments[GetPaletteIndex(position, cube.GMin, cube.BMin, cube.AMin)],

            // Green
            2 => moments[GetPaletteIndex(cube.RMax, position, cube.BMax, cube.AMax)]
               - moments[GetPaletteIndex(cube.RMax, position, cube.BMax, cube.AMin)]
               - moments[GetPaletteIndex(cube.RMax, position, cube.BMin, cube.AMax)]
               + moments[GetPaletteIndex(cube.RMax, position, cube.BMin, cube.AMin)]
               - moments[GetPaletteIndex(cube.RMin, position, cube.BMax, cube.AMax)]
               + moments[GetPaletteIndex(cube.RMin, position, cube.BMax, cube.AMin)]
               + moments[GetPaletteIndex(cube.RMin, position, cube.BMin, cube.AMax)]
               - moments[GetPaletteIndex(cube.RMin, position, cube.BMin, cube.AMin)],

            // Blue
            1 => moments[GetPaletteIndex(cube.RMax, cube.GMax, position, cube.AMax)]
               - moments[GetPaletteIndex(cube.RMax, cube.GMax, position, cube.AMin)]
               - moments[GetPaletteIndex(cube.RMax, cube.GMin, position, cube.AMax)]
               + moments[GetPaletteIndex(cube.RMax, cube.GMin, position, cube.AMin)]
               - moments[GetPaletteIndex(cube.RMin, cube.GMax, position, cube.AMax)]
               + moments[GetPaletteIndex(cube.RMin, cube.GMax, position, cube.AMin)]
               + moments[GetPaletteIndex(cube.RMin, cube.GMin, position, cube.AMax)]
               - moments[GetPaletteIndex(cube.RMin, cube.GMin, position, cube.AMin)],

            // Alpha
            0 => moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, position)]
               - moments[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, position)]
               - moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, position)]
               + moments[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, position)]
               - moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, position)]
               + moments[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, position)]
               + moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, position)]
               - moments[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, position)],
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

    /// <summary>
    /// Builds a 3-D color histogram of <c>counts, r/g/b, c^2</c>.
    /// </summary>
    /// <param name="pixels">The source pixel data.</param>
    private readonly void Build3DHistogram(ReadOnlySpan<Rgba32> pixels)
    {
        Span<Moment> moments = this.momentsOwner.GetSpan();
        for (int x = 0; x < pixels.Length; x++)
        {
            Rgba32 rgba = pixels[x];
            int r = (rgba.R >> (8 - IndexBits)) + 1;
            int g = (rgba.G >> (8 - IndexBits)) + 1;
            int b = (rgba.B >> (8 - IndexBits)) + 1;
            int a = (rgba.A >> (8 - IndexAlphaBits)) + 1;

            moments[GetPaletteIndex(r, g, b, a)] += rgba;
        }
    }

    /// <summary>
    /// Converts the histogram into moments so that we can rapidly calculate the sums of the above quantities over any desired box.
    /// </summary>
    /// <param name="allocator">The memory allocator used for allocating buffers.</param>
    private readonly void Get3DMoments(MemoryAllocator allocator)
    {
        using IMemoryOwner<Moment> volume = allocator.Allocate<Moment>(IndexCount * IndexAlphaCount);
        using IMemoryOwner<Moment> area = allocator.Allocate<Moment>(IndexAlphaCount);

        Span<Moment> momentSpan = this.momentsOwner.GetSpan();
        Span<Moment> volumeSpan = volume.GetSpan();
        Span<Moment> areaSpan = area.GetSpan();
        const int indexBits2 = IndexBits * 2;
        const int indexAndAlphaBits = IndexBits + IndexAlphaBits;
        const int indexBitsAndAlphaBits1 = IndexBits + IndexAlphaBits + 1;
        int baseIndex = GetPaletteIndex(1, 0, 0, 0);

        for (int r = 1; r < IndexCount; r++)
        {
            // Currently, RyuJIT hoists the invariants of multi-level nested loop only to the
            // immediate outer loop. See https://github.com/dotnet/runtime/issues/61420
            // To ensure the calculation doesn't happen repeatedly, hoist some of the calculations
            // in the form of ind1* manually.
            int ind1R = (r << (indexBits2 + IndexAlphaBits)) +
                (r << indexBitsAndAlphaBits1) +
                (r << indexBits2) +
                (r << (IndexBits + 1)) +
                r;

            volumeSpan.Clear();

            for (int g = 1; g < IndexCount; g++)
            {
                int ind1G = ind1R +
                    (g << indexAndAlphaBits) +
                    (g << IndexBits) +
                    g;
                int r_g = r + g;

                areaSpan.Clear();

                for (int b = 1; b < IndexCount; b++)
                {
                    int ind1B = ind1G +
                        ((r_g + b) << IndexAlphaBits) +
                        b;

                    Moment line = default;
                    int bIndexAlphaOffset = b * IndexAlphaCount;
                    for (int a = 1; a < IndexAlphaCount; a++)
                    {
                        int ind1 = ind1B + a;

                        line += momentSpan[ind1];

                        areaSpan[a] += line;

                        int inv = bIndexAlphaOffset + a;
                        volumeSpan[inv] += areaSpan[a];

                        int ind2 = ind1 - baseIndex;
                        momentSpan[ind1] = momentSpan[ind2] + volumeSpan[inv];
                    }
                }
            }
        }
    }

    /// <summary>
    /// Computes the weighted variance of a box cube.
    /// </summary>
    /// <param name="cube">The cube.</param>
    /// <returns>The <see cref="float"/>.</returns>
    private readonly double Variance(ref Box cube)
    {
        ReadOnlySpan<Moment> momentSpan = this.momentsOwner.GetSpan();

        Moment volume = Volume(ref cube, momentSpan);
        Moment variance =
              momentSpan[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMax)]
            - momentSpan[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMax, cube.AMin)]
            - momentSpan[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMax)]
            + momentSpan[GetPaletteIndex(cube.RMax, cube.GMax, cube.BMin, cube.AMin)]
            - momentSpan[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMax)]
            + momentSpan[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMax, cube.AMin)]
            + momentSpan[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMax)]
            - momentSpan[GetPaletteIndex(cube.RMax, cube.GMin, cube.BMin, cube.AMin)]
            - momentSpan[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMax)]
            + momentSpan[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMax, cube.AMin)]
            + momentSpan[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMax)]
            - momentSpan[GetPaletteIndex(cube.RMin, cube.GMax, cube.BMin, cube.AMin)]
            + momentSpan[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMax)]
            - momentSpan[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMax, cube.AMin)]
            - momentSpan[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMax)]
            + momentSpan[GetPaletteIndex(cube.RMin, cube.GMin, cube.BMin, cube.AMin)];

        Vector4 vector = new(volume.R, volume.G, volume.B, volume.A);
        return variance.Moment2 - (Vector4.Dot(vector, vector) / volume.Weight);
    }

    /// <summary>
    /// We want to minimize the sum of the variances of two sub-boxes.
    /// The sum(c^2) terms can be ignored since their sum over both sub-boxes
    /// is the same (the sum for the whole box) no matter where we split.
    /// The remaining terms have a minus sign in the variance formula,
    /// so we drop the minus sign and maximize the sum of the two terms.
    /// </summary>
    /// <param name="cube">The cube.</param>
    /// <param name="direction">The direction.</param>
    /// <param name="first">The first position.</param>
    /// <param name="last">The last position.</param>
    /// <param name="cut">The cutting point.</param>
    /// <param name="whole">The whole moment.</param>
    /// <returns>The <see cref="float"/>.</returns>
    private readonly float Maximize(ref Box cube, int direction, int first, int last, out int cut, Moment whole)
    {
        ReadOnlySpan<Moment> momentSpan = this.momentsOwner.GetSpan();
        Moment bottom = Bottom(ref cube, direction, momentSpan);

        float max = 0F;
        cut = -1;

        for (int i = first; i < last; i++)
        {
            Moment half = bottom + Top(ref cube, direction, i, momentSpan);

            if (half.Weight == 0)
            {
                continue;
            }

            Vector4 vector = new(half.R, half.G, half.B, half.A);
            float temp = Vector4.Dot(vector, vector) / half.Weight;

            half = whole - half;

            if (half.Weight == 0)
            {
                continue;
            }

            vector = new(half.R, half.G, half.B, half.A);
            temp += Vector4.Dot(vector, vector) / half.Weight;

            if (temp > max)
            {
                max = temp;
                cut = i;
            }
        }

        return max;
    }

    /// <summary>
    /// Cuts a box.
    /// </summary>
    /// <param name="set1">The first set.</param>
    /// <param name="set2">The second set.</param>
    /// <returns>Returns a value indicating whether the box has been split.</returns>
    private readonly bool Cut(ref Box set1, ref Box set2)
    {
        ReadOnlySpan<Moment> momentSpan = this.momentsOwner.GetSpan();
        Moment whole = Volume(ref set1, momentSpan);

        float maxR = this.Maximize(ref set1, 3, set1.RMin + 1, set1.RMax, out int cutR, whole);
        float maxG = this.Maximize(ref set1, 2, set1.GMin + 1, set1.GMax, out int cutG, whole);
        float maxB = this.Maximize(ref set1, 1, set1.BMin + 1, set1.BMax, out int cutB, whole);
        float maxA = this.Maximize(ref set1, 0, set1.AMin + 1, set1.AMax, out int cutA, whole);

        int dir;

        if ((maxR >= maxG) && (maxR >= maxB) && (maxR >= maxA))
        {
            dir = 3;

            if (cutR < 0)
            {
                return false;
            }
        }
        else if ((maxG >= maxR) && (maxG >= maxB) && (maxG >= maxA))
        {
            dir = 2;
        }
        else if ((maxB >= maxR) && (maxB >= maxG) && (maxB >= maxA))
        {
            dir = 1;
        }
        else
        {
            dir = 0;
        }

        set2.RMax = set1.RMax;
        set2.GMax = set1.GMax;
        set2.BMax = set1.BMax;
        set2.AMax = set1.AMax;

        switch (dir)
        {
            // Red
            case 3:
                set2.RMin = set1.RMax = cutR;
                set2.GMin = set1.GMin;
                set2.BMin = set1.BMin;
                set2.AMin = set1.AMin;
                break;

            // Green
            case 2:
                set2.GMin = set1.GMax = cutG;
                set2.RMin = set1.RMin;
                set2.BMin = set1.BMin;
                set2.AMin = set1.AMin;
                break;

            // Blue
            case 1:
                set2.BMin = set1.BMax = cutB;
                set2.RMin = set1.RMin;
                set2.GMin = set1.GMin;
                set2.AMin = set1.AMin;
                break;

            // Alpha
            case 0:
                set2.AMin = set1.AMax = cutA;
                set2.RMin = set1.RMin;
                set2.GMin = set1.GMin;
                set2.BMin = set1.BMin;
                break;
        }

        set1.Volume = (set1.RMax - set1.RMin) * (set1.GMax - set1.GMin) * (set1.BMax - set1.BMin) * (set1.AMax - set1.AMin);
        set2.Volume = (set2.RMax - set2.RMin) * (set2.GMax - set2.GMin) * (set2.BMax - set2.BMin) * (set2.AMax - set2.AMin);

        return true;
    }

    /// <summary>
    /// Marks a color space tag.
    /// </summary>
    /// <param name="cube">The cube.</param>
    /// <param name="label">A label.</param>
    private readonly void Mark(ref Box cube, byte label)
    {
        Span<byte> tagSpan = this.tagsOwner.GetSpan();

        for (int r = cube.RMin + 1; r <= cube.RMax; r++)
        {
            // Currently, RyuJIT hoists the invariants of multi-level nested loop only to the
            // immediate outer loop. See https://github.com/dotnet/runtime/issues/61420
            // To ensure the calculation doesn't happen repeatedly, hoist some of the calculations
            // in the form of ind1* manually.
            int ind1R = (r << ((IndexBits * 2) + IndexAlphaBits)) +
                (r << (IndexBits + IndexAlphaBits + 1)) +
                (r << (IndexBits * 2)) +
                (r << (IndexBits + 1)) +
                r;

            for (int g = cube.GMin + 1; g <= cube.GMax; g++)
            {
                int ind1G = ind1R +
                    (g << (IndexBits + IndexAlphaBits)) +
                    (g << IndexBits) +
                    g;
                int r_g = r + g;

                for (int b = cube.BMin + 1; b <= cube.BMax; b++)
                {
                    int ind1B = ind1G +
                        ((r_g + b) << IndexAlphaBits) +
                        b;

                    for (int a = cube.AMin + 1; a <= cube.AMax; a++)
                    {
                        int index = ind1B + a;

                        tagSpan[index] = label;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Builds the cube.
    /// </summary>
    private void BuildCube()
    {
        // Store the volume variance.
        using IMemoryOwner<double> vvOwner = this.Configuration.MemoryAllocator.Allocate<double>(this.maxColors);
        Span<double> vv = vvOwner.GetSpan();

        ref Box cube = ref MemoryMarshal.GetArrayDataReference(this.colorCube);
        cube.RMin = cube.GMin = cube.BMin = cube.AMin = 0;
        cube.RMax = cube.GMax = cube.BMax = IndexCount - 1;
        cube.AMax = IndexAlphaCount - 1;

        int next = 0;

        for (int i = 1; i < this.maxColors; i++)
        {
            ref Box nextCube = ref this.colorCube[next];
            ref Box currentCube = ref this.colorCube[i];
            if (this.Cut(ref nextCube, ref currentCube))
            {
                vv[next] = nextCube.Volume > 1 ? this.Variance(ref nextCube) : 0D;
                vv[i] = currentCube.Volume > 1 ? this.Variance(ref currentCube) : 0D;
            }
            else
            {
                vv[next] = 0D;
                i--;
            }

            next = 0;

            double temp = vv[0];
            for (int k = 1; k <= i; k++)
            {
                if (vv[k] > temp)
                {
                    temp = vv[k];
                    next = k;
                }
            }

            if (temp <= 0D)
            {
                this.maxColors = i + 1;
                break;
            }
        }
    }

    private struct Moment
    {
        /// <summary>
        /// Moment of <c>r*P(c)</c>.
        /// </summary>
        public long R;

        /// <summary>
        /// Moment of <c>g*P(c)</c>.
        /// </summary>
        public long G;

        /// <summary>
        /// Moment of <c>b*P(c)</c>.
        /// </summary>
        public long B;

        /// <summary>
        /// Moment of <c>a*P(c)</c>.
        /// </summary>
        public long A;

        /// <summary>
        /// Moment of <c>P(c)</c>.
        /// </summary>
        public long Weight;

        /// <summary>
        /// Moment of <c>c^2*P(c)</c>.
        /// </summary>
        public double Moment2;

        [MethodImpl(InliningOptions.ShortMethod)]
        public static Moment operator +(Moment x, Moment y)
        {
            x.R += y.R;
            x.G += y.G;
            x.B += y.B;
            x.A += y.A;
            x.Weight += y.Weight;
            x.Moment2 += y.Moment2;
            return x;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static Moment operator -(Moment x, Moment y)
        {
            x.R -= y.R;
            x.G -= y.G;
            x.B -= y.B;
            x.A -= y.A;
            x.Weight -= y.Weight;
            x.Moment2 -= y.Moment2;
            return x;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static Moment operator -(Moment x)
        {
            x.R = -x.R;
            x.G = -x.G;
            x.B = -x.B;
            x.A = -x.A;
            x.Weight = -x.Weight;
            x.Moment2 = -x.Moment2;
            return x;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static Moment operator +(Moment x, Rgba32 y)
        {
            x.R += y.R;
            x.G += y.G;
            x.B += y.B;
            x.A += y.A;
            x.Weight++;

            Vector4 vector = new(y.R, y.G, y.B, y.A);
            x.Moment2 += Vector4.Dot(vector, vector);

            return x;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 Normalize()
            => new Vector4(this.R, this.G, this.B, this.A) / this.Weight / 255F;
    }

    /// <summary>
    /// Represents a box color cube.
    /// </summary>
    private struct Box : IEquatable<Box>
    {
        /// <summary>
        /// Gets or sets the min red value, exclusive.
        /// </summary>
        public int RMin;

        /// <summary>
        /// Gets or sets the max red value, inclusive.
        /// </summary>
        public int RMax;

        /// <summary>
        /// Gets or sets the min green value, exclusive.
        /// </summary>
        public int GMin;

        /// <summary>
        /// Gets or sets the max green value, inclusive.
        /// </summary>
        public int GMax;

        /// <summary>
        /// Gets or sets the min blue value, exclusive.
        /// </summary>
        public int BMin;

        /// <summary>
        /// Gets or sets the max blue value, inclusive.
        /// </summary>
        public int BMax;

        /// <summary>
        /// Gets or sets the min alpha value, exclusive.
        /// </summary>
        public int AMin;

        /// <summary>
        /// Gets or sets the max alpha value, inclusive.
        /// </summary>
        public int AMax;

        /// <summary>
        /// Gets or sets the volume.
        /// </summary>
        public int Volume;

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj)
            => obj is Box box
            && this.Equals(box);

        /// <inheritdoc/>
        public readonly bool Equals(Box other) =>
            this.RMin == other.RMin
            && this.RMax == other.RMax
            && this.GMin == other.GMin
            && this.GMax == other.GMax
            && this.BMin == other.BMin
            && this.BMax == other.BMax
            && this.AMin == other.AMin
            && this.AMax == other.AMax
            && this.Volume == other.Volume;

        /// <inheritdoc/>
        public override readonly int GetHashCode()
        {
            HashCode hash = default;
            hash.Add(this.RMin);
            hash.Add(this.RMax);
            hash.Add(this.GMin);
            hash.Add(this.GMax);
            hash.Add(this.BMin);
            hash.Add(this.BMax);
            hash.Add(this.AMin);
            hash.Add(this.AMax);
            hash.Add(this.Volume);
            return hash.ToHashCode();
        }
    }

    private readonly struct PixelRowDelegate : IQuantizingPixelRowDelegate<Rgba32>
    {
        private readonly WuQuantizer<TPixel> quantizer;

        public PixelRowDelegate(ref WuQuantizer<TPixel> quantizer) => this.quantizer = quantizer;

        public void Invoke(ReadOnlySpan<Rgba32> row, int rowIndex) => this.quantizer.Build3DHistogram(row);
    }
}
