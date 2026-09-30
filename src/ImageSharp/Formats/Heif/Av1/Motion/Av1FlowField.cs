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
/// One entry describes a square of <see cref="DownsampleFactor"/> samples, so the field is that
/// many times smaller than the frame in each direction. The two components are stored apart, each
/// with a border of <see cref="BorderOuter"/> entries on every side, because the upscale filter of
/// one level reads that far outside the area the level filled.
/// </para>
/// <para>
/// Reference: FlowField, alloc_flow_field(), compute_flow_field(), fill_flow_field_borders() and
/// upscale_flow_component().
/// </para>
/// </remarks>
internal sealed class Av1FlowField : IDisposable
{
    /// <summary>
    /// The bits by which the field is smaller than the frame in each direction.
    /// </summary>
    /// <remarks>Reference: DOWNSAMPLE_SHIFT.</remarks>
    public const int DownsampleShift = 3;

    /// <summary>
    /// The samples that one field entry describes in each direction.
    /// </summary>
    /// <remarks>Reference: DOWNSAMPLE_FACTOR.</remarks>
    public const int DownsampleFactor = 1 << DownsampleShift;

    /// <summary>
    /// The offset from the first sample of a block to the sample the entry describes.
    /// </summary>
    /// <remarks>Reference: UPSAMPLE_CENTER_OFFSET.</remarks>
    public const int UpsampleCenterOffset = (DownsampleFactor - 1) / 2;

    /// <summary>
    /// The entries at each edge that the solver leaves alone because a patch centered there would
    /// read outside the level.
    /// </summary>
    /// <remarks>Reference: FLOW_BORDER_INNER.</remarks>
    public const int BorderInner = (Av1DenseFlowSolver.PatchSize >> 1) >> DownsampleShift;

    /// <summary>
    /// The taps of the upscale filter.
    /// </summary>
    /// <remarks>Reference: FLOW_UPSCALE_TAPS.</remarks>
    private const int UpscaleTaps = 4;

    /// <summary>
    /// The entries held outside each edge, so that the upscale filter needs no clamping.
    /// </summary>
    /// <remarks>Reference: FLOW_BORDER_OUTER.</remarks>
    public const int BorderOuter = UpscaleTaps / 2;

    private readonly IMemoryOwner<double> owner;
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

        // The two components share one allocation, and both are cleared, because the coarsest level
        // begins from a flow of zero and the border of every level is filled from entries that the
        // level itself wrote.
        this.owner = allocator.Allocate<double>(2 * this.planeLength, AllocationOptions.Clean);
    }

    /// <summary>
    /// Gets the entries across the finest level.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the entries down the finest level.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the entries between one row of the field and the next, including both borders.
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
    /// <remarks>Reference: flow_upscale_filter, phase 0.75.</remarks>
    private static ReadOnlySpan<double> LowerPhaseFilter => [-3 / 128.0, 29 / 128.0, 111 / 128.0, -9 / 128.0];

    /// <summary>
    /// Gets the cubic taps of the output entry that sits a quarter of an entry after its input.
    /// </summary>
    /// <remarks>Reference: flow_upscale_filter, phase 0.25.</remarks>
    private static ReadOnlySpan<double> UpperPhaseFilter => [-9 / 128.0, 111 / 128.0, 29 / 128.0, -3 / 128.0];

    /// <summary>
    /// Refines the flow of one frame pair from the coarsest level of the pyramid to the finest.
    /// </summary>
    /// <param name="allocator">The allocator of the scratch used between two levels.</param>
    /// <param name="source">The pyramid of the frame the model maps from.</param>
    /// <param name="reference">The pyramid of the frame the model maps to.</param>
    /// <param name="levelCount">The levels that both pyramids hold.</param>
    /// <remarks>
    /// The refinement stops after level one. The field of level zero is the interpolation of level
    /// one and is not refined, because refining the correspondences themselves later is both faster
    /// and more accurate than refining every entry of the finest level.
    /// Reference: compute_flow_field().
    /// </remarks>
    public void Compute(MemoryAllocator allocator, Av1ImagePyramid source, Av1ImagePyramid reference, int levelCount)
    {
        IMemoryOwner<double>? scratchOwner = null;
        Span<double> scratch = default;
        int scratchOrigin = 0;
        if (levelCount >= 2)
        {
            // The scratch holds one horizontally upscaled level, which is as tall as the level that
            // is upscaled and as wide as the field. Its rows above and below the level are written
            // by the upscale itself, so the allocation does not have to be cleared.
            int scratchHeight = source.GetLevel(1).Height >> DownsampleShift;
            scratchOwner = allocator.Allocate<double>((scratchHeight + (2 * BorderOuter)) * this.Stride);
            scratch = scratchOwner.Memory.Span;
            scratchOrigin = BorderOuter * this.Stride;
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
                        // The entry describes the sample at the center of its block, and the patch is
                        // placed so that its own center sample is that sample.
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

                // The edges the solver left alone are filled from the nearest entry it did write, so
                // that the upscale of this level reads a defined value everywhere.
                this.FillBorders(horizontal, levelWidth, levelHeight);
                this.FillBorders(vertical, levelWidth, levelHeight);

                this.Upscale(horizontal, levelWidth, levelHeight, scratch, scratchOrigin);
                this.Upscale(vertical, levelWidth, levelHeight, scratch, scratchOrigin);

                // An odd level width or height gives the next level one more entry than the doubling
                // produced. That entry takes the value of its neighbour, which keeps the ratio of the
                // two levels at exactly two.
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
            scratchOwner?.Dispose();
        }
    }

    /// <summary>
    /// Interpolates one flow component at a fractional position.
    /// </summary>
    /// <param name="component">The component to read.</param>
    /// <param name="offset">The index of the entry at or before the position.</param>
    /// <param name="stride">The entries between one row and the next.</param>
    /// <param name="horizontalKernel">The four horizontal taps.</param>
    /// <param name="verticalKernel">The four vertical taps.</param>
    /// <returns>The interpolated value.</returns>
    /// <remarks>Reference: bicubic_interp_one().</remarks>
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
    /// <param name="kernel">The four taps, in increasing position order.</param>
    /// <remarks>Reference: get_cubic_kernel_dbl().</remarks>
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
    /// <remarks>Reference: get_cubic_value_dbl().</remarks>
    private static double Apply(ReadOnlySpan<double> values, ReadOnlySpan<double> kernel)
        => (values[0] * kernel[0]) + (values[1] * kernel[1]) + (values[2] * kernel[2]) + (values[3] * kernel[3]);

    /// <summary>
    /// Spreads the outermost written entry of one component into the edges around it.
    /// </summary>
    /// <remarks>Reference: fill_flow_field_borders().</remarks>
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

        // Each row is taken from its own left edge, so every index below counts from that edge and
        // none of them is negative. Entry j of the row therefore sits at BorderOuter + j.
        for (int row = BorderInner; row <= lastRow; row++)
        {
            Span<double> entries = component.Slice(start + (row * this.Stride), rowLength);
            entries[..(BorderOuter + BorderInner)].Fill(entries[BorderOuter + BorderInner]);
            entries[(BorderOuter + lastColumn + 1)..].Fill(entries[BorderOuter + lastColumn]);
        }

        // The rows above and below take a complete copy of the nearest written row, including the
        // left and right edges that were just filled.
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
    /// The two output entries of one input entry sit a quarter of an entry on each side of it, so
    /// each takes its own phase of the cubic filter. The magnitude doubles because an entry of the
    /// finer level spans half the samples that an entry of this level spans.
    /// Reference: upscale_flow_component().
    /// </remarks>
    /// <param name="component">The component to double, in place.</param>
    /// <param name="width">The entries across the level.</param>
    /// <param name="height">The entries down the level.</param>
    /// <param name="scratch">The storage of the horizontally doubled level.</param>
    /// <param name="scratchOrigin">The index of the first entry of the scratch that is not border.</param>
    private void Upscale(Span<double> component, int width, int height, Span<double> scratch, int scratchOrigin)
    {
        int upscaledWidth = width * 2;
        for (int row = 0; row < height; row++)
        {
            int input = this.Origin + (row * this.Stride);
            UpscaleRow(
                component.Slice(input - BorderOuter, width + (2 * BorderOuter)),
                scratch.Slice(scratchOrigin + (row * this.Stride), upscaledWidth));
        }

        // The vertical pass reads rows above and below the level, so the scratch keeps a copy of its
        // first and last rows there.
        ReadOnlySpan<double> topRow = scratch.Slice(scratchOrigin, upscaledWidth);
        for (int row = -BorderOuter; row < 0; row++)
        {
            topRow.CopyTo(scratch.Slice(scratchOrigin + (row * this.Stride), upscaledWidth));
        }

        ReadOnlySpan<double> bottomRow = scratch.Slice(scratchOrigin + ((height - 1) * this.Stride), upscaledWidth);
        for (int row = height; row < height + BorderOuter; row++)
        {
            bottomRow.CopyTo(scratch.Slice(scratchOrigin + (row * this.Stride), upscaledWidth));
        }

        for (int row = 0; row < height; row++)
        {
            int output = this.Origin + (2 * row * this.Stride);
            UpscaleColumns(
                scratch[(scratchOrigin + ((row - BorderOuter) * this.Stride))..],
                this.Stride,
                component.Slice(output, upscaledWidth),
                component.Slice(output + this.Stride, upscaledWidth));
        }
    }

    /// <summary>
    /// Doubles one row of a component horizontally, and doubles its magnitude, as the first pass of
    /// <c>upscale_flow_component</c> does.
    /// </summary>
    /// <remarks>
    /// The two outputs of one entry form a lane pair whose inputs are adjacent, so a pair loads two adjacent entries
    /// and multiplies them by the two phases' taps at once. Each lane sums its taps in the reference order with
    /// separate multiplies and adds, so every output is exact.
    /// </remarks>
    /// <param name="input">The row, beginning <see cref="BorderOuter"/> entries before its first entry and ending as
    /// many after its last.</param>
    /// <param name="output">Receives two entries for each entry of the row.</param>
    internal static void UpscaleRow(ReadOnlySpan<double> input, Span<double> output)
    {
        ReadOnlySpan<double> lowerPhase = LowerPhaseFilter;
        ReadOnlySpan<double> upperPhase = UpperPhaseFilter;
        int width = output.Length / 2;
        ref double inputBase = ref MemoryMarshal.GetReference(input);
        ref double outputBase = ref MemoryMarshal.GetReference(output);
        int column = 0;

        // Each stage stops where its last load would pass the right border.
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
    /// Doubles one row of a horizontally doubled component vertically, as the second pass of
    /// <c>upscale_flow_component</c> does.
    /// </summary>
    /// <remarks>
    /// Each lane sums its taps in the reference order with separate multiplies and adds, so every output is exact.
    /// </remarks>
    /// <param name="input">The entries <see cref="BorderOuter"/> rows above the input row, followed by the rest of
    /// the component.</param>
    /// <param name="stride">The entries between one row and the next.</param>
    /// <param name="top">Receives the output row a quarter of an entry above the input row.</param>
    /// <param name="bottom">Receives the output row a quarter of an entry below the input row.</param>
    internal static void UpscaleColumns(ReadOnlySpan<double> input, int stride, Span<double> top, Span<double> bottom)
    {
        ReadOnlySpan<double> lowerPhase = LowerPhaseFilter;
        ReadOnlySpan<double> upperPhase = UpperPhaseFilter;
        int width = top.Length;
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
    /// Copies the neighbouring entry into a column or a row that the doubling did not produce.
    /// </summary>
    /// <remarks>Reference: the rightmost column and bottommost row blocks of compute_flow_field().</remarks>
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
