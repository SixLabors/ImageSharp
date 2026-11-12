// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Retains JPEG XL noise strengths and adds the format's correlated random noise to native component planes.
/// </summary>
/// <remarks>
/// The arithmetic follows libjxl v0.13.0, revision 91837342b24034078780b687f57f670a7def8153:
/// dec_noise.cc, xorshift128plus-inl.h and render_pipeline/stage_noise.cc.
/// Random samples retain all 23 mantissa bits. No pixel bounds apply before the final pixel conversion.
/// </remarks>
internal readonly partial struct JxlNoise
{
    /// <summary>
    /// The two-pixel radius of the five-by-five Laplacian noise kernel in libjxl render_pipeline/stage_noise.cc.
    /// </summary>
    private const int Radius = 2;

    /// <summary>
    /// The five taps on each axis of the native noise kernel.
    /// </summary>
    private const int Diameter = (2 * Radius) + 1;

    /// <summary>
    /// The four raw rows retained at a tile boundary: two rows on each side of the boundary.
    /// </summary>
    private const int BorderRows = 2 * Radius;

    /// <summary>
    /// The three native X, Y and B noise planes generated in that order for each group.
    /// </summary>
    private const int ColorChannels = 3;

    /// <summary>
    /// The eight encoded intensity-strength samples in libjxl noise.h NoiseParams::Lut.
    /// </summary>
    private const int StrengthCount = 8;

    /// <summary>
    /// The sixteen float samples supplied by one advance of the native eight 64-bit generators.
    /// Each generator supplies its low and high 32-bit words, in that order.
    /// </summary>
    private const int RandomBatchLength = 16;

    /// <summary>
    /// The maximum current, left and right groups retained for a kernel neighborhood.
    /// The separate border strips retain vertical neighbors, so storage does not grow with image height.
    /// </summary>
    private const int MaximumRetainedGroups = 3;

    /// <summary>
    /// The weight of each of the 24 noncentral taps in the native five-by-five noise convolution.
    /// </summary>
    private const float NeighborWeight = 0.16F;

    /// <summary>
    /// The native central tap. Together with 24 neighbor weights it gives zero total kernel weight.
    /// </summary>
    private const float CenterWeight = -3.84F;

    /// <summary>
    /// The strength denominator from libjxl noise.h kNoisePrecision. Each ten-bit integer represents units of 1/1024.
    /// </summary>
    private const float NoisePrecision = 1024F;

    /// <summary>
    /// The strict activation threshold from libjxl NoiseParams::HasAny. Tables at or below it add no noise.
    /// </summary>
    private const float MinimumNoiseStrength = 1e-3F;

    private readonly InlineArray8<float> strengths;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlNoise"/> struct with the complete native strength table.
    /// </summary>
    /// <param name="strengths">The eight nonnegative strengths in intensity order.</param>
    /// <param name="hasAny">Whether a strength exceeds the native activation threshold.</param>
    private JxlNoise(InlineArray8<float> strengths, bool hasAny)
    {
        this.strengths = strengths;
        this.HasAny = hasAny;
    }

    /// <summary>
    /// Gets a value indicating whether the table contains a strength above the native activation threshold.
    /// </summary>
    public bool HasAny { get; }

    /// <summary>
    /// Reads the eight ten-bit strengths before the frame's DC factors.
    /// </summary>
    /// <param name="reader">The reader at the first noise strength.</param>
    /// <returns>The complete noise parameters. An inactive table still consumes all 80 encoded bits.</returns>
    public static JxlNoise Read(ref JxlBitReader reader)
    {
        InlineArray8<float> strengths = default;
        bool hasAny = false;
        for (int i = 0; i < StrengthCount; i++)
        {
            float value = reader.ReadBits(10) * (1F / NoisePrecision);
            strengths[i] = value;
            hasAny |= value > MinimumNoiseStrength;
        }

        return new JxlNoise(strengths, hasAny);
    }

    /// <summary>
    /// Adds noise after frame upsampling and before LF storage or color conversion.
    /// </summary>
    /// <param name="colors">The three distinct native X, Y and B plane owners. Only their visible rectangles are changed.</param>
    /// <param name="size">The positive visible dimensions after frame upsampling and LF reduction.</param>
    /// <param name="groupSize">The validated frame group size in output pixels.</param>
    /// <param name="correlation">The frame's base Y-to-X and Y-to-B correlation ratios.</param>
    /// <param name="visibleFrame">The visible-frame seed counter, advanced by the decoder before this frame.</param>
    /// <param name="nonvisibleFrame">The nonvisible-frame seed counter, advanced or reset before this frame.</param>
    /// <param name="allocator">The tracked allocator for one bounded group and border workspace.</param>
    /// <param name="cancellationToken">The token checked between groups and output rows.</param>
    /// <remarks>
    /// The caller supplies complete native float planes. An inactive table changes no samples and rents no storage.
    /// The workspace retains at most three groups and two four-row border strips. It never retains whole-frame noise planes.
    /// </remarks>
    public void Apply(
        ReadOnlySpan<Buffer2D<float>> colors,
        Size size,
        int groupSize,
        Vector2 correlation,
        uint visibleFrame,
        uint nonvisibleFrame,
        MemoryAllocator allocator,
        CancellationToken cancellationToken)
    {
        if (!this.HasAny)
        {
            return;
        }

        int groupColumns = ((size.Width - 1) / groupSize) + 1;
        int retainedGroups = Math.Min(MaximumRetainedGroups, groupColumns);
        int tileStride = Math.Min(groupSize, size.Width) + (2 * Radius);
        int tileHeight = Math.Min(groupSize, size.Height);
        int tilePlaneLength = tileStride * tileHeight;
        int tileLength = ColorChannels * tilePlaneLength;
        int tileStorageLength = retainedGroups * tileLength;
        long borderStrideLong = (long)size.Width + (2 * Radius);
        long borderLengthLong = ColorChannels * BorderRows * borderStrideLong;
        long workspaceLength = tileStorageLength + (2 * borderLengthLong);
        if (workspaceLength > int.MaxValue)
        {
            throw new InvalidMemoryOperationException("The JPEG XL noise workspace exceeds the supported buffer length.");
        }

        int borderStride = (int)borderStrideLong;
        int borderLength = (int)borderLengthLong;
        using IMemoryOwner<float> owner = allocator.Allocate<float>((int)workspaceLength);
        Span<float> workspace = owner.Memory.Span[..(int)workspaceLength];
        NoiseOperator operation = new(this.strengths, correlation);
        int previousBorder = tileStorageLength;
        int nextBorder = tileStorageLength + borderLength;

        // Native Random3Planes advances one RNG through all of plane zero, then one, then two for each group.
        // Retain those complete groups. A three-slot horizontal ring keeps both neighbors until the center is consumed.
        // Two border strips keep the previous group's last four rows separate from the next strip under construction.
        for (int bandY = 0; bandY < size.Height;)
        {
            int height = Math.Min(groupSize, size.Height - bandY);
            for (int generated = 0; generated < groupColumns; generated++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int left = generated * groupSize;
                int width = Math.Min(groupSize, size.Width - left);
                int slot = generated % retainedGroups;
                GenerateGroup(
                    workspace.Slice(slot * tileLength, tileLength),
                    tileStride,
                    tilePlaneLength,
                    width,
                    height,
                    visibleFrame,
                    nonvisibleFrame,
                    (uint)left,
                    (uint)bandY);

                if (generated > 0)
                {
                    ApplyGroup(
                        workspace,
                        colors,
                        size,
                        groupSize,
                        generated - 1,
                        bandY,
                        height,
                        retainedGroups,
                        tileStride,
                        tilePlaneLength,
                        tileLength,
                        previousBorder,
                        nextBorder,
                        borderStride,
                        operation,
                        cancellationToken);
                }
            }

            // The last group needs no future horizontal input. Its right halo mirrors the global image edge.
            ApplyGroup(
                workspace,
                colors,
                size,
                groupSize,
                groupColumns - 1,
                bandY,
                height,
                retainedGroups,
                tileStride,
                tilePlaneLength,
                tileLength,
                previousBorder,
                nextBorder,
                borderStride,
                operation,
                cancellationToken);

            if (bandY + height < size.Height)
            {
                // Every group has now supplied its bottom four original rows. Complete the global horizontal
                // reflection before publishing the new strip; no pooled padding remains observable to a later band.
                for (int channel = 0; channel < ColorChannels; channel++)
                {
                    for (int row = 0; row < BorderRows; row++)
                    {
                        Span<float> border = workspace.Slice(nextBorder + (((channel * BorderRows) + row) * borderStride), borderStride);
                        for (int halo = 0; halo < Radius; halo++)
                        {
                            border[halo] = border[Radius + JxlUpsampler.MirrorIndex(halo - Radius, size.Width)];
                            border[Radius + size.Width + halo] = border[Radius + JxlUpsampler.MirrorIndex((long)size.Width + halo, size.Width)];
                        }
                    }
                }

                (previousBorder, nextBorder) = (nextBorder, previousBorder);
            }

            bandY += height;
        }
    }

    /// <summary>
    /// Generates a complete group's three raw planes in the native channel, row and batch order.
    /// </summary>
    /// <param name="tile">The borrowed three-plane tile. Each row reserves two unused halo columns on each side.</param>
    /// <param name="stride">The physical tile row length in floats.</param>
    /// <param name="planeLength">The reserved storage for one component.</param>
    /// <param name="width">The positive visible group width.</param>
    /// <param name="height">The positive visible group height.</param>
    /// <param name="visibleFrame">The first frame seed.</param>
    /// <param name="nonvisibleFrame">The second frame seed.</param>
    /// <param name="left">The group's global output column, used as the third seed.</param>
    /// <param name="top">The group's global output row, used as the fourth seed.</param>
    private static void GenerateGroup(
        Span<float> tile,
        int stride,
        int planeLength,
        int width,
        int height,
        uint visibleFrame,
        uint nonvisibleFrame,
        uint left,
        uint top)
    {
        Xorshift128Plus random = new(visibleFrame, nonvisibleFrame, left, top);
        InlineArray16<float> tailStorage = default;
        Span<float> tail = tailStorage;
        for (int channel = 0; channel < ColorChannels; channel++)
        {
            for (int y = 0; y < height; y++)
            {
                Span<float> row = tile.Slice((channel * planeLength) + (y * stride) + Radius, width);
                int x = 0;
                for (; x <= width - RandomBatchLength; x += RandomBatchLength)
                {
                    random.Fill(row.Slice(x, RandomBatchLength));
                }

                if (x < width)
                {
                    // Each row consumes complete sixteen-float batches. Discard unused words instead of carrying
                    // them into the next row: that would change all later rows and the following channel's RNG state.
                    random.Fill(tail);
                    tail[..(width - x)].CopyTo(row[x..]);
                }
            }
        }
    }

    /// <summary>
    /// Adds one horizontal group's ready rows and preserves its raw bottom border for the following group row.
    /// </summary>
    /// <param name="workspace">The initialized group ring and border strips.</param>
    /// <param name="colors">The borrowed native output plane owners.</param>
    /// <param name="size">The full visible output dimensions.</param>
    /// <param name="groupSize">The frame group extent.</param>
    /// <param name="group">The group column whose left and right neighbors are now available.</param>
    /// <param name="bandY">The current group's global top row.</param>
    /// <param name="height">The current group row's visible height.</param>
    /// <param name="retainedGroups">The number of physical horizontal ring slots.</param>
    /// <param name="tileStride">The padded row length in each tile.</param>
    /// <param name="tilePlaneLength">The reserved floats for one tile component.</param>
    /// <param name="tileLength">The reserved floats for one three-component tile.</param>
    /// <param name="previousBorder">The first float of the previous group row's four-row strip.</param>
    /// <param name="nextBorder">The first float of the separate strip under construction.</param>
    /// <param name="borderStride">The global strip row length, including horizontal halos.</param>
    /// <param name="operation">The frame's prepared strength lookup and correlation ratios.</param>
    /// <param name="cancellationToken">The token checked between output rows.</param>
    private static void ApplyGroup(
        Span<float> workspace,
        ReadOnlySpan<Buffer2D<float>> colors,
        Size size,
        int groupSize,
        int group,
        int bandY,
        int height,
        int retainedGroups,
        int tileStride,
        int tilePlaneLength,
        int tileLength,
        int previousBorder,
        int nextBorder,
        int borderStride,
        NoiseOperator operation,
        CancellationToken cancellationToken)
    {
        int left = group * groupSize;
        int width = Math.Min(groupSize, size.Width - left);
        int tileOffset = (group % retainedGroups) * tileLength;
        bool lastBand = bandY + height == size.Height;
        for (int channel = 0; channel < ColorChannels; channel++)
        {
            for (int row = 0; row < height; row++)
            {
                int rowOffset = tileOffset + (channel * tilePlaneLength) + (row * tileStride);
                for (int halo = 0; halo < Radius; halo++)
                {
                    int leftX = JxlUpsampler.MirrorIndex((long)left + halo - Radius, size.Width);
                    int rightX = JxlUpsampler.MirrorIndex((long)left + width + halo, size.Width);
                    int leftSource = (((leftX / groupSize) % retainedGroups) * tileLength)
                        + (channel * tilePlaneLength) + (row * tileStride) + Radius + (leftX % groupSize);

                    int rightSource = (((rightX / groupSize) % retainedGroups) * tileLength)
                        + (channel * tilePlaneLength) + (row * tileStride) + Radius + (rightX % groupSize);

                    workspace[rowOffset + halo] = workspace[leftSource];
                    workspace[rowOffset + Radius + width + halo] = workspace[rightSource];
                }
            }

            if (!lastBand)
            {
                // A nonfinal band has the full format group height (at least 128), so its last four rows exist.
                // Copy raw samples before this tile slot can be reused. Output writes never alter the raw group ring.
                for (int row = 0; row < BorderRows; row++)
                {
                    workspace.Slice(tileOffset + (channel * tilePlaneLength) + ((height - BorderRows + row) * tileStride) + Radius, width)
                        .CopyTo(workspace.Slice(nextBorder + (((channel * BorderRows) + row) * borderStride) + Radius + left, width));
                }
            }
        }

        InlineArray16<int> rowStorage = default;
        int firstY = Math.Max(0, bandY - Radius);
        int endY = lastBand ? size.Height : bandY + height - Radius;
        for (int y = firstY; y < endY; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int channel = 0; channel < ColorChannels; channel++)
            {
                for (int tap = 0; tap < Diameter; tap++)
                {
                    int sourceY = JxlUpsampler.MirrorIndex((long)y + tap - Radius, size.Height);
                    rowStorage[(channel * Diameter) + tap] = sourceY < bandY
                        ? previousBorder + (((channel * BorderRows) + sourceY - bandY + BorderRows) * borderStride) + left
                        : tileOffset + (channel * tilePlaneLength) + ((sourceY - bandY) * tileStride);
                }
            }

            // The preceding band's bottom two output rows waited for these new raw rows. Every other output
            // row now has all five source rows as well. Color samples are independent between pixels, so group
            // traversal can update each pixel once without retaining a second color image or filtered-noise planes.
            FilterNoiseRow(
                workspace,
                rowStorage,
                colors[0].DangerousGetRowSpan(y).Slice(left, width),
                colors[1].DangerousGetRowSpan(y).Slice(left, width),
                colors[2].DangerousGetRowSpan(y).Slice(left, width),
                operation);
        }
    }
}
