// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    private const int RestorationRadius = 3;

    // Native stores 4 * (sqrt(0.5) - 1) divided by the physical sigma. Magnitudes below sigma 0.3 skip EPF.
    private const float InverseSigmaNumerator = -1.1715728752538099024F;
    private const float MinimumInverseSigma = -3.90524291751269967465540850526868F;
    private const float MinimumSigmaMagnitude = -1e-4F;

    // Native EPF stages multiply inverse sigma by this SAD distance factor and the pass/border scales.
    private const float EpfDistanceScale = 1.65F;

    /// <summary>
    /// Expands chroma and applies the frame's restoration filters to its reconstructed float component planes.
    /// </summary>
    /// <param name="frame">The coding dimensions and parsed filter parameters.</param>
    /// <param name="allocator">The tracked allocator for a reusable row window and sigma metadata.</param>
    /// <param name="cancellationToken">The token checked between rows and filter passes.</param>
    /// <remarks>
    /// Chroma expansion precedes Gaborish and EPF. The filters retain floating-point intermediates and do not apply pixel bounds.
    /// The existing final-capacity component allocations hold every output; temporary storage contains rows, not another image.
    /// </remarks>
    public void ApplyRestoration(JxlFrameHeader frame, MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        Size size = frame.CodingSize;
        for (int component = 0; component < 3; component++)
        {
            Buffer2D<float> channel = this.reconstructedChannels[component];
            Size current = this.reconstructedSizes[component];
            if (this.horizontalShifts[component] != 0)
            {
                for (int y = 0; y < current.Height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ExpandChromaHorizontal(channel.DangerousGetRowSpan(y)[..size.Width], current.Width);
                }

                current = new Size(size.Width, current.Height);
            }

            if (this.verticalShifts[component] != 0)
            {
                // Reverse expansion keeps every as-yet-unread source row below the destination rows.
                // Each kernel loads all three contributing values before either overlapping destination store.
                for (int y = current.Height - 1; y >= 0; y--)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReadOnlySpan<float> top = channel.DangerousGetRowSpan(Math.Max(y - 1, 0))[..current.Width];
                    ReadOnlySpan<float> middle = channel.DangerousGetRowSpan(y)[..current.Width];
                    ReadOnlySpan<float> bottom = channel.DangerousGetRowSpan(Math.Min(y + 1, current.Height - 1))[..current.Width];
                    Span<float> first = channel.DangerousGetRowSpan(2 * y)[..current.Width];
                    Span<float> second = (2 * y) + 1 < size.Height ? channel.DangerousGetRowSpan((2 * y) + 1)[..current.Width] : [];
                    ExpandChromaVertical(top, middle, bottom, first, second);
                }

                current = size;
            }

            this.reconstructedSizes[component] = current;
        }

        JxlRestorationFilter filter = frame.RestorationFilter;
        if (!filter.Gaborish && filter.Iterations == 0)
        {
            return;
        }

        // The largest EPF neighborhood retains at most seven physical rows per component. When Gaborish
        // feeds EPF, append at most three raw rows per component in the same owner. Its filtered rows
        // go straight into the EPF ring. Tiny images retain each physical row once in each disjoint ring.
        int maximumRadius = filter.Iterations == 3 ? 3 : filter.Iterations != 0 ? 2 : 1;
        int ringRows = Math.Min((2 * maximumRadius) + 1, size.Height);
        int gaborishRows = filter.Gaborish && filter.Iterations != 0 ? Math.Min(3, size.Height) : 0;
        long rowStride = (long)size.Width + (2 * RestorationRadius);
        long windowLength = 3 * (ringRows + gaborishRows) * rowStride;
        if (windowLength > int.MaxValue)
        {
            throw new InvalidMemoryOperationException("The JPEG XL restoration row window exceeds the supported buffer length.");
        }

        using IMemoryOwner<float> windowOwner = allocator.Allocate<float>((int)windowLength);
        Span<float> storage = windowOwner.Memory.Span[..(int)windowLength];
        int stride = (int)rowStride;
        int epfWindowLength = 3 * ringRows * stride;
        Span<float> window = storage[..epfWindowLength];
        Span<float> gaborishWindow = storage[epfWindowLength..];
        if (filter.Gaborish && filter.Iterations == 0)
        {
            this.ApplyGaborish(filter, size, window, stride, ringRows, cancellationToken);
        }

        if (filter.Iterations != 0)
        {
            using IMemoryOwner<float> sigmaOwner = allocator.Allocate<float>(2 * this.dcSize.Width);
            Span<float> sigmaStorage = sigmaOwner.Memory.Span;
            Span<float> quantizationSigma = sigmaStorage[..this.dcSize.Width];
            Span<float> sigma = sigmaStorage.Slice(this.dcSize.Width, this.dcSize.Width);
            if (filter.Iterations == 3)
            {
                this.ApplyEpf<Epf0Neighborhood>(
                    filter, size, window, gaborishWindow, stride, ringRows, quantizationSigma, sigma, filter.Pass0SigmaScale, cancellationToken);

                // Only the first EPF consumes Gaborish output. Later passes read their preceding EPF's completed component planes.
                gaborishWindow = [];
            }

            this.ApplyEpf<Epf1Neighborhood>(filter, size, window, gaborishWindow, stride, ringRows, quantizationSigma, sigma, 1F, cancellationToken);
            if (filter.Iterations >= 2)
            {
                this.ApplyEpf<Epf2Neighborhood>(
                    filter, size, window, [], stride, ringRows, quantizationSigma, sigma, filter.Pass2SigmaScale, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Applies the normalized symmetric three-by-three Gaborish kernel with original rows retained until their final use.
    /// </summary>
    /// <param name="filter">The validated per-component kernel weights.</param>
    /// <param name="size">The full coding-resolution dimensions after chroma expansion.</param>
    /// <param name="window">The shared three-component ring storage.</param>
    /// <param name="stride">The padded row stride.</param>
    /// <param name="ringRows">The number of physical rows retained per component.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private void ApplyGaborish(JxlRestorationFilter filter, Size size, Span<float> window, int stride, int ringRows, CancellationToken cancellationToken)
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
            this.LoadRestorationRow(window, stride, ringRows, size.Width, y);
        }

        for (int y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (y + 1 < size.Height && y != 0)
            {
                this.LoadRestorationRow(window, stride, ringRows, size.Width, y + 1);
            }

            PrepareRestorationRows(rows, y, size.Height, stride, ringRows);
            for (int component = 0; component < 3; component++)
            {
                int componentOffset = component * ringRows * stride;
                ReadOnlySpan<float> top = window.Slice(componentOffset + rows[RestorationRadius - 1], stride);
                ReadOnlySpan<float> middle = window.Slice(componentOffset + rows[RestorationRadius], stride);
                ReadOnlySpan<float> bottom = window.Slice(componentOffset + rows[RestorationRadius + 1], stride);
                Span<float> destination = this.reconstructedChannels[component].DangerousGetRowSpan(y)[..size.Width];
                FilterGaborishRow(top, middle, bottom, destination, operations[component]);
            }
        }
    }

    /// <summary>
    /// Applies one EPF neighborhood, reusing a block-row sigma and one ring of original input rows.
    /// </summary>
    /// <typeparam name="TNeighborhood">The fixed neighbor and patch shape selected before traversal.</typeparam>
    /// <param name="filter">The parsed channel, border and sigma parameters.</param>
    /// <param name="size">The full coding-resolution dimensions.</param>
    /// <param name="window">The reusable three-component ring.</param>
    /// <param name="gaborishWindow">The separate raw Gaborish ring for the first EPF, or an empty span to read completed component rows.</param>
    /// <param name="stride">The padded row stride.</param>
    /// <param name="ringRows">The number of physical rows retained per component.</param>
    /// <param name="quantizationSigma">The retained quantizer-dependent sigma term in each block column.</param>
    /// <param name="sigma">One inverse-sigma value per block column.</param>
    /// <param name="sigmaScale">The current pass's dimensionless sigma multiplier.</param>
    /// <param name="cancellationToken">The token checked between rows.</param>
    private void ApplyEpf<TNeighborhood>(
        JxlRestorationFilter filter,
        Size size,
        Span<float> window,
        Span<float> gaborishWindow,
        int stride,
        int ringRows,
        Span<float> quantizationSigma,
        Span<float> sigma,
        float sigmaScale,
        CancellationToken cancellationToken)
        where TNeighborhood : struct, IEpfNeighborhood
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
        if (!gaborishWindow.IsEmpty)
        {
            for (int component = 0; component < 3; component++)
            {
                gaborish[component] = new GaborishOperator(filter.GaborishWeight1[component], filter.GaborishWeight2[component]);
            }
        }

        // Produce input rows in physical order. A mirrored EPF access can refer only to a row already
        // produced here. At the bottom edge the retained physical rows supply every reflected neighbor.
        for (int y = 0; y < Math.Min(radius + 1, size.Height); y++)
        {
            this.LoadEpfRow(window, gaborishWindow, gaborish, size, stride, ringRows, y);
        }

        for (int y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (y != 0 && y < size.Height - radius)
            {
                this.LoadEpfRow(window, gaborishWindow, gaborish, size, stride, ringRows, y + radius);
            }

            if ((y & (BlockSize - 1)) == 0)
            {
                this.PrepareSigmaRow(filter, y / BlockSize, quantizationSigma, sigma);
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
            Span<float> destinationX = this.reconstructedChannels[0].DangerousGetRowSpan(y)[..size.Width];
            Span<float> destinationY = this.reconstructedChannels[1].DangerousGetRowSpan(y)[..size.Width];
            Span<float> destinationB = this.reconstructedChannels[2].DangerousGetRowSpan(y)[..size.Width];

            // With fused Gaborish, input production already passed this row's final raw-sample use.
            // Final EPF stores can overwrite the component planes without changing future Gaborish input.
            FilterEpfRow<TNeighborhood>(operation, destinationX, destinationY, destinationB);
        }
    }

    /// <summary>
    /// Supplies the next physical EPF input row, either from completed planes or directly from Gaborish.
    /// </summary>
    /// <param name="window">The EPF input ring, partitioned into three component ranges.</param>
    /// <param name="gaborishWindow">The disjoint raw Gaborish ring, or an empty span without fused Gaborish.</param>
    /// <param name="gaborish">The prepared Gaborish operators, used only when the raw ring is present.</param>
    /// <param name="size">The full coding-resolution dimensions.</param>
    /// <param name="stride">The padded row stride shared by both rings.</param>
    /// <param name="ringRows">The number of physical rows retained for each EPF component.</param>
    /// <param name="row">The next physical row. Calls start at zero and advance by one.</param>
    private void LoadEpfRow(
        Span<float> window,
        Span<float> gaborishWindow,
        ReadOnlySpan<GaborishOperator> gaborish,
        Size size,
        int stride,
        int ringRows,
        int row)
    {
        if (gaborishWindow.IsEmpty)
        {
            this.LoadRestorationRow(window, stride, ringRows, size.Width, row);
            return;
        }

        int rawRows = Math.Min(3, size.Height);
        if (row == 0)
        {
            // Gaborish needs one original row ahead. Tiny images keep all available rows, so repeated
            // reflection reads their original samples rather than another filtered or overwritten row.
            for (int y = 0; y < Math.Min(2, size.Height); y++)
            {
                this.LoadRestorationRow(gaborishWindow, stride, rawRows, size.Width, y);
            }
        }
        else if (row + 1 < size.Height)
        {
            this.LoadRestorationRow(gaborishWindow, stride, rawRows, size.Width, row + 1);
        }

        InlineArray8<int> rowStorage = default;
        Span<int> rows = rowStorage;
        PrepareRestorationRows(rows, row, size.Height, stride, rawRows);
        for (int component = 0; component < 3; component++)
        {
            int sourceOffset = component * rawRows * stride;
            ReadOnlySpan<float> top = gaborishWindow.Slice(sourceOffset + rows[RestorationRadius - 1], stride);
            ReadOnlySpan<float> middle = gaborishWindow.Slice(sourceOffset + rows[RestorationRadius], stride);
            ReadOnlySpan<float> bottom = gaborishWindow.Slice(sourceOffset + rows[RestorationRadius + 1], stride);
            Span<float> destination = window.Slice(((component * ringRows) + (row % ringRows)) * stride, stride);
            FilterGaborishRow(top, middle, bottom, destination.Slice(RestorationRadius, size.Width), gaborish[component]);
            PadRestorationRow(destination, size.Width);
        }
    }

    /// <summary>
    /// Prepares one row of the native inverse-sigma field without allocating a full block-grid image.
    /// </summary>
    /// <param name="filter">The quantization multiplier and sharpness table.</param>
    /// <param name="row">The block row.</param>
    /// <param name="quantizationSigma">The transform-origin sigma term retained for each covered column.</param>
    /// <param name="sigma">The output inverse-sigma row.</param>
    private void PrepareSigmaRow(JxlRestorationFilter filter, int row, Span<float> quantizationSigma, Span<float> sigma)
    {
        ReadOnlySpan<byte> strategies = this.Strategies.DangerousGetRowSpan(row);
        ReadOnlySpan<int> origins = this.Quantization.DangerousGetRowSpan(row);
        ReadOnlySpan<byte> sharpness = this.Sharpness.DangerousGetRowSpan(row);
        ReadOnlySpan<float> factors = filter.Sharpness;
        float quantizationScale = this.GlobalScale * (1F / GlobalScaleDenominator);
        for (int x = 0; x < this.dcSize.Width; x++)
        {
            byte strategy = strategies[x];
            if ((strategy & FirstBlockFlag) != 0)
            {
                float term = filter.QuantizationMultiplier / (quantizationScale * origins[x] * InverseSigmaNumerator);
                quantizationSigma.Slice(x, StrategyWidths[strategy >> 1]).Fill(term);
            }

            // A continuation keeps the quantizer of its transform origin, while sharpness belongs to each block.
            // The top block row starts at transform origins, so every column is initialized before it is read.
            float value = quantizationSigma[x] * factors[sharpness[x]];

            // The native minimum magnitude keeps this reciprocal finite. Zero sharpness produces a skip-level inverse sigma.
            sigma[x] = 1F / MathF.Min(MinimumSigmaMagnitude, value);
        }
    }

    /// <summary>
    /// Copies one original physical row into its reusable ring slot and fills its mirrored horizontal border.
    /// </summary>
    /// <param name="window">The shared ring storage.</param>
    /// <param name="stride">The padded row stride.</param>
    /// <param name="ringRows">The number of physical rows retained per component.</param>
    /// <param name="width">The current logical component width.</param>
    /// <param name="row">The original physical row to retain before output overwrites it.</param>
    private void LoadRestorationRow(Span<float> window, int stride, int ringRows, int width, int row)
    {
        for (int component = 0; component < 3; component++)
        {
            Span<float> destination = window.Slice(((component * ringRows) + (row % ringRows)) * stride, stride);
            this.reconstructedChannels[component].DangerousGetRowSpan(row)[..width].CopyTo(destination.Slice(RestorationRadius, width));
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
}
