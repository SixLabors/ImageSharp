// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Applies JPEG XL restoration to three borrowed float planes with one reusable row window.
/// </summary>
internal static partial class JxlRestoration
{
    /// <summary>
    /// The native numerator 4 * (sqrt(0.5) - 1) used to convert physical sigma to inverse sigma.
    /// </summary>
    public const float InverseSigmaNumerator = -1.1715728752538099024F;

    /// <summary>
    /// The maximum three-pixel EPF radius. Native stage_epf.cc EPF0 combines a five-by-five cross with three-by-three cross SADs.
    /// </summary>
    private const int RestorationRadius = 3;

    /// <summary>
    /// The eight-pixel axis length of a coefficient block. One inverse-sigma value applies to its eight-by-eight pixels.
    /// </summary>
    private const int BlockSize = 8;

    // Inverse magnitudes beyond this boundary correspond to physical sigma below 0.3 and skip EPF.
    private const float MinimumInverseSigma = -3.90524291751269967465540850526868F;

    // Native EPF stages multiply inverse sigma by this SAD distance factor and the pass/border scales.
    private const float EpfDistanceScale = 1.65F;

    /// <summary>
    /// Prepares inverse sigma before a complete eight-row filter block. The filter never calls this per pixel.
    /// </summary>
    public interface ISigmaSource
    {
        /// <summary>
        /// Gets a value indicating whether one prepared row remains valid for every image row and filter pass.
        /// </summary>
        static abstract bool IsConstant { get; }

        /// <summary>
        /// Gets the number of inverse-sigma entries in one complete block row.
        /// </summary>
        int BlockColumns { get; }

        /// <summary>
        /// Gets the scratch length retained between block rows, or zero for a constant source.
        /// </summary>
        int RetainedLength { get; }

        /// <summary>
        /// Prepares one complete inverse-sigma row using the source's existing metadata.
        /// </summary>
        /// <param name="row">The validated block-row index, or zero for a constant source.</param>
        /// <param name="retained">The source's retained block-row scratch.</param>
        /// <param name="sigma">The inverse-sigma destination, with <see cref="BlockColumns"/> entries.</param>
        void PrepareRow(int row, Span<float> retained, Span<float> sigma);
    }

    /// <summary>
    /// Applies Gaborish and edge-preserving filters in place with the selected sigma source.
    /// </summary>
    /// <typeparam name="TSigmaSource">The constant modular or quantizer-dependent VarDCT sigma preparation.</typeparam>
    /// <param name="channels">Three distinct final-capacity float planes containing completed coding-resolution color samples.</param>
    /// <param name="size">The positive coding dimensions after chroma expansion.</param>
    /// <param name="filter">The parsed Gaborish and edge-preserving filter parameters.</param>
    /// <param name="sigmaSource">The ready sigma preparation, borrowing any required frame metadata for this call.</param>
    /// <param name="allocator">The tracked allocator for one combined row-window and sigma allocation.</param>
    /// <param name="cancellationToken">The token checked between rows and filter passes.</param>
    /// <remarks>
    /// The caller owns every plane. A modular gray channel must initialize three independent planes before filtering,
    /// because component-specific Gaborish weights can differ. All samples remain floats without pixel bounds.
    /// Sigma preparation runs only at block-row boundaries; the SIMD filter workers do not dispatch through the source.
    /// </remarks>
    public static void Apply<TSigmaSource>(
        ReadOnlySpan<Buffer2D<float>> channels,
        Size size,
        JxlRestorationFilter filter,
        TSigmaSource sigmaSource,
        MemoryAllocator allocator,
        CancellationToken cancellationToken)
        where TSigmaSource : struct, ISigmaSource
    {
        if (!filter.Gaborish && filter.Iterations == 0)
        {
            return;
        }

        // The largest EPF neighborhood retains at most seven physical rows per component.
        // Gaborish supplies the first EPF's ring directly; standalone Gaborish uses this ring for original rows.
        // Tiny images retain each physical row once, and reflected accesses reuse its ring slot.
        int maximumRadius = filter.Iterations == 3 ? 3 : filter.Iterations != 0 ? 2 : 1;
        int ringRows = Math.Min((2 * maximumRadius) + 1, size.Height);
        long rowStride = (long)size.Width + (2 * RestorationRadius);
        long windowLength = 3 * ringRows * rowStride;
        int sigmaLength = filter.Iterations == 0 ? 0 : sigmaSource.BlockColumns;
        int retainedLength = filter.Iterations == 0 ? 0 : sigmaSource.RetainedLength;
        long workspaceLength = windowLength + sigmaLength + retainedLength;
        if (workspaceLength > int.MaxValue)
        {
            throw new InvalidMemoryOperationException("The JPEG XL restoration row window exceeds the supported buffer length.");
        }

        // The same owner supplies all filter rows and sigma metadata. Modular sigma has no retained
        // quantizer row, while VarDCT keeps its original transform-origin terms between block rows.
        using IMemoryOwner<float> workspaceOwner = allocator.Allocate<float>((int)workspaceLength);
        Span<float> workspace = workspaceOwner.Memory.Span[..(int)workspaceLength];
        Span<float> window = workspace[..(int)windowLength];
        int stride = (int)rowStride;

        if (filter.Gaborish && filter.Iterations == 0)
        {
            ApplyGaborish(channels, filter, size, window, stride, ringRows, cancellationToken);
        }

        if (filter.Iterations != 0)
        {
            Span<float> quantizationSigma = workspace.Slice((int)windowLength, retainedLength);
            Span<float> sigma = workspace.Slice((int)windowLength + retainedLength, sigmaLength);
            if (TSigmaSource.IsConstant)
            {
                // A modular frame uses one constant at every block and pass. Fill its single row once.
                sigmaSource.PrepareRow(0, quantizationSigma, sigma);
            }

            bool firstPassGaborish = filter.Gaborish;

            if (filter.Iterations == 3)
            {
                ApplyEpf<Epf0Neighborhood, TSigmaSource>(
                    channels,
                    filter,
                    size,
                    window,
                    firstPassGaborish,
                    stride,
                    ringRows,
                    quantizationSigma,
                    sigma,
                    filter.Pass0SigmaScale,
                    sigmaSource,
                    cancellationToken);

                // Only the first EPF consumes Gaborish output. Later passes read their preceding EPF's completed component planes.
                firstPassGaborish = false;
            }

            ApplyEpf<Epf1Neighborhood, TSigmaSource>(
                channels,
                filter,
                size,
                window,
                firstPassGaborish,
                stride,
                ringRows,
                quantizationSigma,
                sigma,
                1F,
                sigmaSource,
                cancellationToken);

            if (filter.Iterations >= 2)
            {
                ApplyEpf<Epf2Neighborhood, TSigmaSource>(
                    channels,
                    filter,
                    size,
                    window,
                    false,
                    stride,
                    ringRows,
                    quantizationSigma,
                    sigma,
                    filter.Pass2SigmaScale,
                    sigmaSource,
                    cancellationToken);
            }
        }
    }

    /// <summary>
    /// Applies the normalized symmetric three-by-three Gaborish kernel with original rows retained until their final use.
    /// </summary>
    /// <param name="channels">The three borrowed color planes.</param>
    /// <param name="filter">The validated per-component kernel weights.</param>
    /// <param name="size">The full coding-resolution dimensions after chroma expansion.</param>
    /// <param name="window">The shared three-component ring storage.</param>
    /// <param name="stride">The padded row stride.</param>
    /// <param name="ringRows">The number of physical rows retained per component.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private static void ApplyGaborish(
        ReadOnlySpan<Buffer2D<float>> channels,
        JxlRestorationFilter filter,
        Size size,
        Span<float> window,
        int stride,
        int ringRows,
        CancellationToken cancellationToken)
    {
        InlineArray8<int> rowStorage = default;
        Span<int> rows = rowStorage;
        InlineArray4<GaborishOperator> operationStorage = default;
        Span<GaborishOperator> operations = operationStorage;
        for (int component = 0; component < 3; component++)
        {
            operations[component] = new GaborishOperator(filter.GaborishWeight1[component], filter.GaborishWeight2[component]);
        }

        for (int y = 0; y < Math.Min(2, size.Height); y++)
        {
            LoadRestorationRow(channels, window, stride, ringRows, size.Width, y);
        }

        for (int y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (y + 1 < size.Height && y != 0)
            {
                LoadRestorationRow(channels, window, stride, ringRows, size.Width, y + 1);
            }

            PrepareRestorationRows(rows, y, size.Height, stride, ringRows);
            for (int component = 0; component < 3; component++)
            {
                int componentOffset = component * ringRows * stride;
                ReadOnlySpan<float> top = window.Slice(componentOffset + rows[RestorationRadius - 1] + RestorationRadius, size.Width);
                ReadOnlySpan<float> middle = window.Slice(componentOffset + rows[RestorationRadius] + RestorationRadius, size.Width);
                ReadOnlySpan<float> bottom = window.Slice(componentOffset + rows[RestorationRadius + 1] + RestorationRadius, size.Width);
                Span<float> destination = channels[component].DangerousGetRowSpan(y)[..size.Width];
                FilterGaborishRow(top, middle, bottom, destination, operations[component]);
            }
        }
    }

    /// <summary>
    /// Applies one EPF neighborhood, reusing a block-row sigma and one ring of original input rows.
    /// </summary>
    /// <typeparam name="TNeighborhood">The fixed neighbor and patch shape selected before traversal.</typeparam>
    /// <typeparam name="TSigmaSource">The block-row preparation selected before filtering.</typeparam>
    /// <param name="channels">The three borrowed color planes.</param>
    /// <param name="filter">The parsed channel, border and sigma parameters.</param>
    /// <param name="size">The full coding-resolution dimensions.</param>
    /// <param name="window">The reusable three-component ring.</param>
    /// <param name="firstPassGaborish">Whether this first EPF pass reads Gaborish output directly from the original planes.</param>
    /// <param name="stride">The padded row stride.</param>
    /// <param name="ringRows">The number of physical rows retained per component.</param>
    /// <param name="quantizationSigma">The retained quantizer-dependent sigma term in each block column.</param>
    /// <param name="sigma">One inverse-sigma value per block column.</param>
    /// <param name="sigmaScale">The current pass's dimensionless sigma multiplier.</param>
    /// <param name="sigmaSource">The prepared constant or quantizer-dependent sigma source.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private static void ApplyEpf<TNeighborhood, TSigmaSource>(
        ReadOnlySpan<Buffer2D<float>> channels,
        JxlRestorationFilter filter,
        Size size,
        Span<float> window,
        bool firstPassGaborish,
        int stride,
        int ringRows,
        Span<float> quantizationSigma,
        Span<float> sigma,
        float sigmaScale,
        TSigmaSource sigmaSource,
        CancellationToken cancellationToken)
        where TNeighborhood : struct, IEpfNeighborhood
        where TSigmaSource : struct, ISigmaSource
    {
        int radius = TNeighborhood.Radius;
        InlineArray8<int> rowStorage = default;
        Span<int> rows = rowStorage;
        InlineArray32<float> multiplierStorage = default;
        Span<float> multipliers = multiplierStorage;
        float ordinaryMultiplier = sigmaScale * EpfDistanceScale;
        float borderMultiplier = ordinaryMultiplier * filter.BorderSadMultiplier;
        InlineArray4<GaborishOperator> gaborishStorage = default;
        Span<GaborishOperator> gaborish = gaborishStorage;
        if (firstPassGaborish)
        {
            for (int component = 0; component < 3; component++)
            {
                gaborish[component] = new GaborishOperator(filter.GaborishWeight1[component], filter.GaborishWeight2[component]);
            }
        }

        // Produce input rows in physical order. The first EPF has radius two or three, so its Gaborish
        // input stays ahead of output stores. Startup produces every required row before the first store,
        // including all physical rows when the image is smaller than the neighborhood.
        for (int y = 0; y < Math.Min(radius + 1, size.Height); y++)
        {
            LoadEpfRow(channels, window, firstPassGaborish, gaborish, size, stride, ringRows, y);
        }

        for (int y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (y != 0 && y < size.Height - radius)
            {
                LoadEpfRow(channels, window, firstPassGaborish, gaborish, size, stride, ringRows, y + radius);
            }

            if (!TSigmaSource.IsConstant && (y & (BlockSize - 1)) == 0)
            {
                sigmaSource.PrepareRow(y / BlockSize, quantizationSigma, sigma);
            }

            int blockRow = y & (BlockSize - 1);
            bool borderRow = blockRow == 0 || blockRow == BlockSize - 1;
            for (int x = 0; x < multipliers.Length; x++)
            {
                int blockColumn = x & (BlockSize - 1);
                bool borderColumn = blockColumn == 0 || blockColumn == BlockSize - 1;
                multipliers[x] = borderRow || borderColumn ? borderMultiplier : ordinaryMultiplier;
            }

            PrepareRestorationRows(rows, y, size.Height, stride, ringRows);
            EpfOperator operation = new(window, rows, ringRows * stride, sigma, multipliers, filter.ChannelScale);
            Span<float> destinationX = channels[0].DangerousGetRowSpan(y)[..size.Width];
            Span<float> destinationY = channels[1].DangerousGetRowSpan(y)[..size.Width];
            Span<float> destinationB = channels[2].DangerousGetRowSpan(y)[..size.Width];

            // Gaborish needs only one preceding row. Its next input is at least y+radius-1 >= y+1,
            // so storing EPF row y cannot overwrite any original sample needed by a later Gaborish row.
            // At the bottom, all Gaborish rows are already retained in the EPF ring before their originals change.
            FilterEpfRow<TNeighborhood>(operation, destinationX, destinationY, destinationB);
        }
    }

    /// <summary>
    /// Supplies the next physical EPF input row, either from completed planes or directly from Gaborish.
    /// </summary>
    /// <param name="channels">The three borrowed color planes.</param>
    /// <param name="window">The EPF input ring, partitioned into three component ranges.</param>
    /// <param name="firstPassGaborish">Whether to filter original component rows directly into the first EPF input ring.</param>
    /// <param name="gaborish">The prepared Gaborish operators, used only by the first pass when Gaborish is enabled.</param>
    /// <param name="size">The full coding-resolution dimensions.</param>
    /// <param name="stride">The padded EPF input row stride.</param>
    /// <param name="ringRows">The number of physical rows retained for each EPF component.</param>
    /// <param name="row">The next physical row. Calls start at zero and advance by one.</param>
    private static void LoadEpfRow(
        ReadOnlySpan<Buffer2D<float>> channels,
        Span<float> window,
        bool firstPassGaborish,
        ReadOnlySpan<GaborishOperator> gaborish,
        Size size,
        int stride,
        int ringRows,
        int row)
    {
        if (!firstPassGaborish)
        {
            LoadRestorationRow(channels, window, stride, ringRows, size.Width, row);
            return;
        }

        // These original rows remain ahead of the first EPF's stores. Resolve the one-row vertical
        // neighborhood with the shared half-sample reflection, including a one-row image.
        int topRow = JxlUpsampler.MirrorIndex((long)row - 1, size.Height);
        int bottomRow = JxlUpsampler.MirrorIndex((long)row + 1, size.Height);

        for (int component = 0; component < 3; component++)
        {
            Buffer2D<float> channel = channels[component];
            ReadOnlySpan<float> top = channel.DangerousGetRowSpan(topRow)[..size.Width];
            ReadOnlySpan<float> middle = channel.DangerousGetRowSpan(row)[..size.Width];
            ReadOnlySpan<float> bottom = channel.DangerousGetRowSpan(bottomRow)[..size.Width];
            Span<float> destination = window.Slice(((component * ringRows) + (row % ringRows)) * stride, stride);
            FilterGaborishRow(top, middle, bottom, destination.Slice(RestorationRadius, size.Width), gaborish[component]);
            PadRestorationRow(destination, size.Width);
        }
    }

    /// <summary>
    /// Copies one original physical row into its reusable ring slot and fills its mirrored horizontal border.
    /// </summary>
    /// <param name="channels">The three borrowed color planes.</param>
    /// <param name="window">The shared ring storage.</param>
    /// <param name="stride">The padded row stride.</param>
    /// <param name="ringRows">The number of physical rows retained per component.</param>
    /// <param name="width">The current logical component width.</param>
    /// <param name="row">The original physical row to retain before output overwrites it.</param>
    private static void LoadRestorationRow(ReadOnlySpan<Buffer2D<float>> channels, Span<float> window, int stride, int ringRows, int width, int row)
    {
        for (int component = 0; component < 3; component++)
        {
            Span<float> destination = window.Slice(((component * ringRows) + (row % ringRows)) * stride, stride);
            channels[component].DangerousGetRowSpan(row)[..width].CopyTo(destination.Slice(RestorationRadius, width));
            PadRestorationRow(destination, width);
        }
    }

    /// <summary>
    /// Fills both horizontal margins from a completed physical row using the format's repeated reflection.
    /// </summary>
    /// <param name="row">The padded row, with its logical samples already stored after the leading margin.</param>
    /// <param name="width">The logical row width, which can be smaller than the three-sample margin.</param>
    private static void PadRestorationRow(Span<float> row, int width)
    {
        // Mirror each logical index independently. Repeating edge samples handles widths one and two
        // without reading an uninitialized margin or changing the filter's source sample order.
        for (int i = 0; i < RestorationRadius; i++)
        {
            row[i] = row[RestorationRadius + JxlUpsampler.MirrorIndex(i - RestorationRadius, width)];
            row[RestorationRadius + width + i] = row[RestorationRadius + JxlUpsampler.MirrorIndex((long)width + i, width)];
        }
    }

    /// <summary>
    /// Resolves each vertical neighbor to a cached physical row, including repeated reflection in very small images.
    /// </summary>
    /// <param name="rows">Seven row offsets relative to a component ring.</param>
    /// <param name="row">The current output row.</param>
    /// <param name="height">The logical image height.</param>
    /// <param name="stride">The padded row stride.</param>
    /// <param name="ringRows">The number of cached physical rows per component.</param>
    private static void PrepareRestorationRows(Span<int> rows, int row, int height, int stride, int ringRows)
    {
        for (int offset = -RestorationRadius; offset <= RestorationRadius; offset++)
        {
            int physical = JxlUpsampler.MirrorIndex((long)row + offset, height);
            rows[offset + RestorationRadius] = (physical % ringRows) * stride;
        }
    }

    /// <summary>
    /// Supplies the format's constant inverse sigma for modular frames without quantizer or sharpness metadata.
    /// </summary>
    public readonly struct ModularSigmaSource : ISigmaSource
    {
        private readonly float inverseSigma;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModularSigmaSource"/> struct for a validated modular frame.
        /// </summary>
        /// <param name="width">The positive coding width, in pixels.</param>
        /// <param name="sigma">The positive physical sigma from the parsed restoration header.</param>
        public ModularSigmaSource(int width, float sigma)
        {
            this.BlockColumns = ((width - 1) / BlockSize) + 1;

            // libjxl dec_frame.cc ProcessDCGroup and jxl-rs features/epf.rs prepare this constant directly.
            // The VarDCT quantizer-dependent minimum and reciprocal sequence do not apply to modular samples.
            this.inverseSigma = InverseSigmaNumerator / sigma;
        }

        /// <inheritdoc />
        public static bool IsConstant => true;

        /// <inheritdoc />
        public int BlockColumns { get; }

        /// <inheritdoc />
        public int RetainedLength => 0;

        /// <inheritdoc />
        public void PrepareRow(int row, Span<float> retained, Span<float> sigma) => sigma.Fill(this.inverseSigma);
    }
}
