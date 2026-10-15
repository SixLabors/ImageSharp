// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Holds the dense flow of one frame pair, one vector for every block of samples.
/// </summary>
/// <remarks>
/// <para>
/// One entry describes a square of <see cref="DownsampleFactor"/> samples, so the field is that many times smaller than the frame in each direction.
/// The two components are stored apart. Each one has a border of <see cref="BorderOuter"/> entries on every side. The upscale filter of one level
/// reads that far outside the area that the level filled.
/// </para>
/// </remarks>
internal sealed class Av1FlowField : IDisposable
{
    /// <summary>
    /// The base-two logarithm of the factor by which the field is smaller than the frame in each direction.
    /// </summary>
    public const int DownsampleShift = 3;

    /// <summary>
    /// The number of samples that one field entry describes in each direction.
    /// </summary>
    public const int DownsampleFactor = 1 << DownsampleShift;

    /// <summary>
    /// The offset from the first sample of a block to the sample the entry describes.
    /// </summary>
    public const int UpsampleCenterOffset = (DownsampleFactor - 1) / 2;

    /// <summary>
    /// The number of entries at each edge that the solver does not write, because a patch centered there reads outside the level.
    /// </summary>
    public const int BorderInner = (Av1DenseFlowSolver.PatchSize >> 1) >> DownsampleShift;

    /// <summary>
    /// The number of taps of the upscale filter.
    /// </summary>
    private const int UpscaleTaps = 4;

    /// <summary>
    /// The number of entries held outside each edge, so that the upscale filter needs no clamping.
    /// </summary>
    public const int BorderOuter = UpscaleTaps / 2;

    /// <summary>
    /// The owner of the storage of both components.
    /// </summary>
    private readonly IMemoryOwner<double> owner;

    /// <summary>
    /// The number of entries in one component, including its border.
    /// </summary>
    private readonly int planeLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1FlowField"/> class.
    /// </summary>
    /// <param name="allocator">The allocator of the field storage.</param>
    /// <param name="frameWidth">The width of the finest level, in samples.</param>
    /// <param name="frameHeight">The height of the finest level, in samples.</param>
    public Av1FlowField(MemoryAllocator allocator, int frameWidth, int frameHeight)
    {
        this.Width = frameWidth >> DownsampleShift;
        this.Height = frameHeight >> DownsampleShift;
        this.Stride = this.Width + (2 * BorderOuter);
        this.planeLength = this.Stride * (this.Height + (2 * BorderOuter));
        this.Origin = (BorderOuter * this.Stride) + BorderOuter;

        // The two components share one allocation. The allocation is cleared, because the coarsest level starts from a flow of zero. The border
        // of every level gets its values from entries that the level itself wrote.
        this.owner = allocator.Allocate<double>(2 * this.planeLength, AllocationOptions.Clean);
    }

    /// <summary>
    /// Gets the number of entries across the finest level.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the number of entries down the finest level.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the number of entries between one row of the field and the next, including both borders.
    /// </summary>
    public int Stride { get; }

    /// <summary>
    /// Gets the index of the first entry of either component that is not border.
    /// </summary>
    public int Origin { get; }

    /// <summary>
    /// Gets the horizontal component, beginning at its border.
    /// </summary>
    public Span<double> Horizontal => this.owner.Memory.Span[..this.planeLength];

    /// <summary>
    /// Gets the vertical component, beginning at its border.
    /// </summary>
    public Span<double> Vertical => this.owner.Memory.Span.Slice(this.planeLength, this.planeLength);

    /// <summary>
    /// Gets the cubic taps of the output entry that sits a quarter of an entry before its input.
    /// </summary>
    /// <remarks>These are the cubic taps of phase 0.75.</remarks>
    private static ReadOnlySpan<double> LowerPhaseFilter => [-3 / 128.0, 29 / 128.0, 111 / 128.0, -9 / 128.0];

    /// <summary>
    /// Gets the cubic taps of the output entry that sits a quarter of an entry after its input.
    /// </summary>
    /// <remarks>These are the cubic taps of phase 0.25.</remarks>
    private static ReadOnlySpan<double> UpperPhaseFilter => [-9 / 128.0, 111 / 128.0, 29 / 128.0, -3 / 128.0];

    /// <summary>
    /// Refines the flow of one frame pair from the coarsest level of the pyramid to the finest.
    /// </summary>
    /// <param name="allocator">The allocator of the buffer that holds one horizontally upscaled level.</param>
    /// <param name="source">The pyramid of the frame the model maps from.</param>
    /// <param name="reference">The pyramid of the frame the model maps to.</param>
    /// <param name="levelCount">The number of levels that both pyramids hold.</param>
    /// <remarks>
    /// The refinement stops after level one. The field of level zero is the interpolation of level one, without refinement. A later refinement of
    /// the correspondences is faster and more accurate than a refinement of every entry of the finest level.
    /// </remarks>
    public void Compute(MemoryAllocator allocator, Av1ImagePyramid source, Av1ImagePyramid reference, int levelCount)
    {
        IMemoryOwner<double>? doubledLevelOwner = null;
        Span<double> doubledLevel = default;
        int doubledLevelOrigin = 0;
        if (levelCount >= 2)
        {
            // This buffer holds one horizontally upscaled level. It is as tall as the source level and as wide as the field. The upscale itself
            // writes the rows above and below the level, so the allocation does not need a clear.
            int doubledLevelHeight = source.GetLevel(1).Height >> DownsampleShift;
            doubledLevelOwner = allocator.Allocate<double>((doubledLevelHeight + (2 * BorderOuter)) * this.Stride);
            doubledLevel = doubledLevelOwner.Memory.Span;
            doubledLevelOrigin = BorderOuter * this.Stride;
        }

        try
        {
            Span<double> horizontal = this.Horizontal;
            Span<double> vertical = this.Vertical;

            for (int level = levelCount - 1; level >= 1; level--)
            {
                Av1ImagePyramid.Level sourceLevel = source.GetLevel(level);
                ReadOnlySpan<byte> sourceSamples = source.GetSamples(level);
                ReadOnlySpan<byte> referenceSamples = reference.GetSamples(level);
                int levelWidth = sourceLevel.Width >> DownsampleShift;
                int levelHeight = sourceLevel.Height >> DownsampleShift;

                for (int row = BorderInner; row < levelHeight - BorderInner; row++)
                {
                    for (int column = BorderInner; column < levelWidth - BorderInner; column++)
                    {
                        // The entry describes the sample at the center of its block. The patch position puts the patch center on that sample.
                        int patchX = (column << DownsampleShift) + UpsampleCenterOffset - Av1DenseFlowSolver.PatchCenter;
                        int patchY = (row << DownsampleShift) + UpsampleCenterOffset - Av1DenseFlowSolver.PatchCenter;
                        int entry = this.Origin + (row * this.Stride) + column;
                        Av1DenseFlowSolver.Solve(
                            sourceSamples,
                            referenceSamples,
                            sourceLevel.Origin,
                            patchX,
                            patchY,
                            sourceLevel.Width,
                            sourceLevel.Height,
                            sourceLevel.Stride,
                            ref horizontal[entry],
                            ref vertical[entry]);
                    }
                }

                // The edges that the solver did not write get the nearest entry that it wrote. Thus the upscale of this level reads a defined value
                // everywhere.
                this.FillBorders(horizontal, levelWidth, levelHeight);
                this.FillBorders(vertical, levelWidth, levelHeight);

                this.Upscale(horizontal, levelWidth, levelHeight, doubledLevel, doubledLevelOrigin);
                this.Upscale(vertical, levelWidth, levelHeight, doubledLevel, doubledLevelOrigin);

                // An odd level width or height gives the next level one more entry than the doubling produced. That entry takes the value of its
                // neighbor, which keeps the ratio of the two levels at exactly two.
                Av1ImagePyramid.Level nextLevel = source.GetLevel(level - 1);
                this.ExtendUpscaledEdges(
                    horizontal,
                    vertical,
                    levelWidth << 1,
                    levelHeight << 1,
                    nextLevel.Width >> DownsampleShift,
                    nextLevel.Height >> DownsampleShift);
            }
        }
        finally
        {
            doubledLevelOwner?.Dispose();
        }
    }

    /// <summary>
    /// Interpolates one flow component at a fractional position.
    /// </summary>
    /// <param name="component">The component to read.</param>
    /// <param name="offset">The index of the entry at or before the position.</param>
    /// <param name="stride">The number of entries between one row and the next.</param>
    /// <param name="horizontalKernel">The four horizontal taps.</param>
    /// <param name="verticalKernel">The four vertical taps.</param>
    /// <returns>The interpolated value.</returns>
    public static double Interpolate(
        ReadOnlySpan<double> component,
        int offset,
        int stride,
        ReadOnlySpan<double> horizontalKernel,
        ReadOnlySpan<double> verticalKernel)
    {
        Span<double> rows = stackalloc double[4];
        for (int row = -1; row < 3; row++)
        {
            rows[row + 1] = Apply(component[(offset + (row * stride) - 1)..], horizontalKernel);
        }

        return Apply(rows, verticalKernel);
    }

    /// <summary>
    /// Builds the four cubic taps of one fractional position.
    /// </summary>
    /// <param name="fraction">The position between two entries, from zero through one.</param>
    /// <param name="kernel">Receives the four taps, in increasing position order.</param>
    public static void GetCubicKernel(double fraction, Span<double> kernel)
    {
        double square = fraction * fraction;
        double cube = square * fraction;
        kernel[0] = (-0.5 * fraction) + square - (0.5 * cube);
        kernel[1] = 1.0 - (2.5 * square) + (1.5 * cube);
        kernel[2] = (0.5 * fraction) + (2.0 * square) - (1.5 * cube);
        kernel[3] = (-0.5 * square) + (0.5 * cube);
    }

    /// <inheritdoc/>
    public void Dispose() => this.owner.Dispose();

    /// <summary>
    /// Applies four taps to four consecutive values.
    /// </summary>
    /// <param name="values">The values, starting at the first of the four.</param>
    /// <param name="kernel">The four taps.</param>
    /// <returns>The weighted sum of the four values.</returns>
    private static double Apply(ReadOnlySpan<double> values, ReadOnlySpan<double> kernel)
        => (values[0] * kernel[0]) + (values[1] * kernel[1]) + (values[2] * kernel[2]) + (values[3] * kernel[3]);

    /// <summary>
    /// Spreads the outermost written entry of one component into the edges around it.
    /// </summary>
    /// <param name="component">The component to fill, in place.</param>
    /// <param name="width">The number of entries across the level.</param>
    /// <param name="height">The number of entries down the level.</param>
    private void FillBorders(Span<double> component, int width, int height)
    {
        if (width <= 2 * BorderInner || height <= 2 * BorderInner)
        {
            return;
        }

        int lastColumn = width - BorderInner - 1;
        int lastRow = height - BorderInner - 1;
        int rowLength = width + (2 * BorderOuter);
        int start = this.Origin - BorderOuter;

        // Each row slice starts at its own left edge, so every index below is not negative. Entry j of the row is at BorderOuter + j.
        for (int row = BorderInner; row <= lastRow; row++)
        {
            Span<double> entries = component.Slice(start + (row * this.Stride), rowLength);
            entries[..(BorderOuter + BorderInner)].Fill(entries[BorderOuter + BorderInner]);
            entries[(BorderOuter + lastColumn + 1)..].Fill(entries[BorderOuter + lastColumn]);
        }

        // The rows above and below get a full copy of the nearest written row, with the left and right edges that the loop above filled.
        ReadOnlySpan<double> firstWritten = component.Slice(start + (BorderInner * this.Stride), rowLength);
        for (int row = -BorderOuter; row < BorderInner; row++)
        {
            firstWritten.CopyTo(component.Slice(start + (row * this.Stride), rowLength));
        }

        ReadOnlySpan<double> lastWritten = component.Slice(start + (lastRow * this.Stride), rowLength);
        for (int row = lastRow + 1; row < height + BorderOuter; row++)
        {
            lastWritten.CopyTo(component.Slice(start + (row * this.Stride), rowLength));
        }
    }

    /// <summary>
    /// Doubles one component in both directions and doubles its magnitude with it.
    /// </summary>
    /// <remarks>
    /// The two output entries of one input entry are a quarter of an entry on each side of it. Thus each one uses its own phase of the cubic filter.
    /// The magnitude doubles, because one sample of this level spans two samples of the finer level.
    /// </remarks>
    /// <param name="component">The component to double, in place.</param>
    /// <param name="width">The number of entries across the level.</param>
    /// <param name="height">The number of entries down the level.</param>
    /// <param name="doubledLevel">The storage of the horizontally doubled level.</param>
    /// <param name="doubledLevelOrigin">The index of the first entry of that storage that is not border.</param>
    private void Upscale(Span<double> component, int width, int height, Span<double> doubledLevel, int doubledLevelOrigin)
    {
        int upscaledWidth = width * 2;
        for (int row = 0; row < height; row++)
        {
            int input = this.Origin + (row * this.Stride);
            UpscaleRow(
                component.Slice(input - BorderOuter, width + (2 * BorderOuter)),
                doubledLevel.Slice(doubledLevelOrigin + (row * this.Stride), upscaledWidth));
        }

        // The vertical pass reads rows above and below the level, so the buffer keeps copies of its first and last rows there.
        ReadOnlySpan<double> topRow = doubledLevel.Slice(doubledLevelOrigin, upscaledWidth);
        for (int row = -BorderOuter; row < 0; row++)
        {
            topRow.CopyTo(doubledLevel.Slice(doubledLevelOrigin + (row * this.Stride), upscaledWidth));
        }

        ReadOnlySpan<double> bottomRow = doubledLevel.Slice(doubledLevelOrigin + ((height - 1) * this.Stride), upscaledWidth);
        for (int row = height; row < height + BorderOuter; row++)
        {
            bottomRow.CopyTo(doubledLevel.Slice(doubledLevelOrigin + (row * this.Stride), upscaledWidth));
        }

        for (int row = 0; row < height; row++)
        {
            int output = this.Origin + (2 * row * this.Stride);
            UpscaleColumns(
                doubledLevel[(doubledLevelOrigin + ((row - BorderOuter) * this.Stride))..],
                this.Stride,
                component.Slice(output, upscaledWidth),
                component.Slice(output + this.Stride, upscaledWidth));
        }
    }

    /// <summary>
    /// Doubles one row of a component horizontally and doubles its magnitude. This is the first pass of the upscale.
    /// </summary>
    /// <remarks>
    /// The two outputs of one entry form a lane pair whose inputs are adjacent. Thus a pair loads two adjacent entries and multiplies them by the taps
    /// of the two phases at once. The wider stages shuffle the entries 0, 1, 1, 2 and more into those pairs. Each lane sums its taps in tap order,
    /// with separate multiplies and adds. Thus every output is bit-exact with the scalar loop.
    /// </remarks>
    /// <param name="input">The row, beginning <see cref="BorderOuter"/> entries before its first entry and ending as many after its last.</param>
    /// <param name="output">Receives two entries for each entry of the row.</param>
    internal static void UpscaleRow(ReadOnlySpan<double> input, Span<double> output)
    {
        ReadOnlySpan<double> lowerPhase = LowerPhaseFilter;
        ReadOnlySpan<double> upperPhase = UpperPhaseFilter;
        int width = output.Length / 2;
        ref double inputBase = ref MemoryMarshal.GetReference(input);
        ref double outputBase = ref MemoryMarshal.GetReference(output);
        int column = 0;

        // Each stage stops before its last load passes the right border.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<long> pairs = Vector512.Create(0L, 1, 1, 2, 2, 3, 3, 4);
            for (; column <= width - 7; column += 4)
            {
                Vector512<double> sum = Vector512<double>.Zero;
                for (int tap = 0; tap < UpscaleTaps; tap++)
                {
                    Vector256<double> taps = Vector256.Create(Vector128.Create(lowerPhase[tap], upperPhase[tap]));
                    Vector512<double> entries = Vector512.Shuffle(Vector512.LoadUnsafe(ref inputBase, (nuint)(column + tap)), pairs);
                    sum += entries * Vector512.Create(taps, taps);
                }

                (sum * 2.0).StoreUnsafe(ref outputBase, (nuint)(2 * column));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<long> pairs = Vector256.Create(0L, 1, 1, 2);
            for (; column <= width - 3; column += 2)
            {
                Vector256<double> sum = Vector256<double>.Zero;
                for (int tap = 0; tap < UpscaleTaps; tap++)
                {
                    Vector128<double> taps = Vector128.Create(lowerPhase[tap], upperPhase[tap]);
                    Vector256<double> entries = Vector256.Shuffle(Vector256.LoadUnsafe(ref inputBase, (nuint)(column + tap)), pairs);
                    sum += entries * Vector256.Create(taps, taps);
                }

                (sum * 2.0).StoreUnsafe(ref outputBase, (nuint)(2 * column));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; column < width; column++)
            {
                Vector128<double> sum = Vector128<double>.Zero;
                for (int tap = 0; tap < UpscaleTaps; tap++)
                {
                    sum += Vector128.LoadUnsafe(ref inputBase, (nuint)(column + tap)) * Vector128.Create(lowerPhase[tap], upperPhase[tap]);
                }

                (sum * 2.0).StoreUnsafe(ref outputBase, (nuint)(2 * column));
            }
        }

        for (; column < width; column++)
        {
            double left = 0;
            double right = 0;
            for (int tap = 0; tap < UpscaleTaps; tap++)
            {
                left += input[column + tap] * lowerPhase[tap];
                right += input[column + tap + 1] * upperPhase[tap];
            }

            output[2 * column] = 2.0 * left;
            output[(2 * column) + 1] = 2.0 * right;
        }
    }

    /// <summary>
    /// Doubles one row of a horizontally doubled component vertically. This is the second pass of the upscale.
    /// </summary>
    /// <remarks>
    /// Each lane sums its taps in tap order, with separate multiplies and adds. Thus every output is bit-exact with the scalar loop.
    /// </remarks>
    /// <param name="input">The entries <see cref="BorderOuter"/> rows above the input row, followed by the rest of the component.</param>
    /// <param name="stride">The number of entries between one row and the next.</param>
    /// <param name="top">Receives the output row a quarter of an entry above the input row.</param>
    /// <param name="bottom">Receives the output row a quarter of an entry below the input row.</param>
    internal static void UpscaleColumns(ReadOnlySpan<double> input, int stride, Span<double> top, Span<double> bottom)
    {
        ReadOnlySpan<double> lowerPhase = LowerPhaseFilter;
        ReadOnlySpan<double> upperPhase = UpperPhaseFilter;
        int width = top.Length;

        // This check covers the last entry that the loops below read, so the unchecked vector loads stay inside the input.
        _ = input[(UpscaleTaps * stride) + width - 1];
        ref double inputBase = ref MemoryMarshal.GetReference(input);
        ref double topBase = ref MemoryMarshal.GetReference(top);
        ref double bottomBase = ref MemoryMarshal.GetReference(bottom);
        int column = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            for (; column <= width - Vector512<double>.Count; column += Vector512<double>.Count)
            {
                Vector512<double> upper = Vector512<double>.Zero;
                Vector512<double> lower = Vector512<double>.Zero;
                for (int tap = 0; tap < UpscaleTaps; tap++)
                {
                    nuint offset = (nuint)((tap * stride) + column);
                    upper += Vector512.LoadUnsafe(ref inputBase, offset) * lowerPhase[tap];
                    lower += Vector512.LoadUnsafe(ref inputBase, offset + (nuint)stride) * upperPhase[tap];
                }

                upper.StoreUnsafe(ref topBase, (nuint)column);
                lower.StoreUnsafe(ref bottomBase, (nuint)column);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; column <= width - Vector256<double>.Count; column += Vector256<double>.Count)
            {
                Vector256<double> upper = Vector256<double>.Zero;
                Vector256<double> lower = Vector256<double>.Zero;
                for (int tap = 0; tap < UpscaleTaps; tap++)
                {
                    nuint offset = (nuint)((tap * stride) + column);
                    upper += Vector256.LoadUnsafe(ref inputBase, offset) * lowerPhase[tap];
                    lower += Vector256.LoadUnsafe(ref inputBase, offset + (nuint)stride) * upperPhase[tap];
                }

                upper.StoreUnsafe(ref topBase, (nuint)column);
                lower.StoreUnsafe(ref bottomBase, (nuint)column);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; column <= width - Vector128<double>.Count; column += Vector128<double>.Count)
            {
                Vector128<double> upper = Vector128<double>.Zero;
                Vector128<double> lower = Vector128<double>.Zero;
                for (int tap = 0; tap < UpscaleTaps; tap++)
                {
                    nuint offset = (nuint)((tap * stride) + column);
                    upper += Vector128.LoadUnsafe(ref inputBase, offset) * lowerPhase[tap];
                    lower += Vector128.LoadUnsafe(ref inputBase, offset + (nuint)stride) * upperPhase[tap];
                }

                upper.StoreUnsafe(ref topBase, (nuint)column);
                lower.StoreUnsafe(ref bottomBase, (nuint)column);
            }
        }

        for (; column < width; column++)
        {
            double upper = 0;
            double lower = 0;
            for (int tap = 0; tap < UpscaleTaps; tap++)
            {
                upper += input[(tap * stride) + column] * lowerPhase[tap];
                lower += input[((tap + 1) * stride) + column] * upperPhase[tap];
            }

            top[column] = upper;
            bottom[column] = lower;
        }
    }

    /// <summary>
    /// Copies the neighboring entry into a column or a row that the doubling did not produce.
    /// </summary>
    /// <param name="horizontal">The horizontal component, in place.</param>
    /// <param name="vertical">The vertical component, in place.</param>
    /// <param name="upscaledWidth">The number of entries across the doubled level.</param>
    /// <param name="upscaledHeight">The number of entries down the doubled level.</param>
    /// <param name="nextWidth">The number of entries across the next finer level.</param>
    /// <param name="nextHeight">The number of entries down the next finer level.</param>
    private void ExtendUpscaledEdges(
        Span<double> horizontal,
        Span<double> vertical,
        int upscaledWidth,
        int upscaledHeight,
        int nextWidth,
        int nextHeight)
    {
        if (nextWidth > upscaledWidth)
        {
            for (int row = 0; row < upscaledHeight; row++)
            {
                int entry = this.Origin + (row * this.Stride) + upscaledWidth;
                horizontal[entry] = horizontal[entry - 1];
                vertical[entry] = vertical[entry - 1];
            }
        }

        if (nextHeight > upscaledHeight)
        {
            for (int column = 0; column < nextWidth; column++)
            {
                int entry = this.Origin + (upscaledHeight * this.Stride) + column;
                horizontal[entry] = horizontal[entry - this.Stride];
                vertical[entry] = vertical[entry - this.Stride];
            }
        }
    }
}
