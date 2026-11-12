// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

internal static partial class JxlRestoration
{
    /// <summary>
    /// Gets the float count required to restore sequential rows without retaining complete component planes.
    /// </summary>
    /// <param name="size">The positive coding dimensions, in pixels.</param>
    /// <param name="filter">The parsed Gaborish and edge-preserving filter parameters.</param>
    /// <param name="blockColumns">The number of inverse-sigma entries in one complete block row.</param>
    /// <param name="retainedLength">The sigma source's scratch length retained between block rows.</param>
    /// <returns>The combined component-ring, output-row and sigma workspace length, in floats.</returns>
    public static int GetWorkspaceLength(Size size, JxlRestorationFilter filter, int blockColumns, int retainedLength)
    {
        // Each filter retains only the rows in its input neighborhood. Its output goes directly into
        // the next filter's ring. One final row remains valid while the caller converts and packs it.
        long rows = 1;
        if (filter.Gaborish)
        {
            rows += Math.Min(3, size.Height);
        }

        if (filter.Iterations == 3)
        {
            rows += Math.Min(7, size.Height);
        }

        if (filter.Iterations != 0)
        {
            rows += Math.Min(5, size.Height);
        }

        if (filter.Iterations >= 2)
        {
            rows += Math.Min(3, size.Height);
        }

        // Concurrent filter positions can lie in different eight-row blocks. Each EPF pass therefore
        // keeps its own sigma source state, rather than overwriting a later pass's preceding block row.
        long stride = (long)size.Width + (2 * RestorationRadius);
        long length = (3 * rows * stride) + ((long)filter.Iterations * (blockColumns + (long)retainedLength));
        if (length > int.MaxValue)
        {
            throw new InvalidMemoryOperationException("The JPEG XL restoration row windows exceed the supported buffer length.");
        }

        return (int)length;
    }

    /// <summary>
    /// Restores sequential native component rows with borrowed storage and the existing SIMD filter operators.
    /// </summary>
    /// <typeparam name="TSigmaSource">The constant modular or quantizer-dependent VarDCT sigma preparation.</typeparam>
    /// <remarks>
    /// Append one physical row, then read every available output row before appending again. After the final input,
    /// call <see cref="Complete"/> and drain the remaining output. The caller owns the workspace and all input samples.
    /// </remarks>
    public ref struct RowProcessor<TSigmaSource>
        where TSigmaSource : struct, ISigmaSource
    {
        // The fixed order is Gaborish, EPF0, EPF1, EPF2. EPF0 occurs only for three iterations;
        // EPF1 occurs for every nonzero iteration count; EPF2 occurs for two or three iterations.
        private const int GaborishFilter = 0;
        private const int Epf0Filter = 1;
        private const int Epf1Filter = 2;
        private const int Epf2Filter = 3;

        private readonly Size size;
        private readonly JxlRestorationFilter filter;
        private readonly TSigmaSource sigmaSource;
        private readonly Span<float> workspace;
        private readonly int stride;
        private readonly int sigmaLength;
        private readonly int retainedLength;
        private readonly int filterCount;
        private readonly int finalOffset;

        // Each active filter records its input ring and monotonically increasing output count.
        // Ring entries are addressed by physical image row modulo their retained row count.
        private InlineArray4<int> filters;
        private InlineArray4<int> inputOffsets;
        private InlineArray4<int> inputRows;
        private InlineArray4<int> sigmaOffsets;
        private InlineArray4<int> producedRows;
        private InlineArray4<GaborishOperator> gaborishOperations;
        private int receivedRows;
        private int nextOutputRow;
        private bool complete;

        /// <summary>
        /// Initializes a new instance of the <see cref="RowProcessor{TSigmaSource}"/> struct over borrowed workspace.
        /// </summary>
        /// <param name="size">The positive coding dimensions, in pixels.</param>
        /// <param name="filter">The parsed Gaborish and edge-preserving filter parameters.</param>
        /// <param name="sigmaSource">The ready source of complete global block-row sigma values.</param>
        /// <param name="workspace">At least <see cref="GetWorkspaceLength"/> floats, retained throughout processing.</param>
        public RowProcessor(Size size, JxlRestorationFilter filter, TSigmaSource sigmaSource, Span<float> workspace)
        {
            this = default;
            this.size = size;
            this.filter = filter;
            this.sigmaSource = sigmaSource;
            this.workspace = workspace;
            this.stride = size.Width + (2 * RestorationRadius);
            this.sigmaLength = sigmaSource.BlockColumns;
            this.retainedLength = sigmaSource.RetainedLength;

            int count = 0;
            if (filter.Gaborish)
            {
                this.filters[count++] = GaborishFilter;
                for (int component = 0; component < 3; component++)
                {
                    this.gaborishOperations[component] = new GaborishOperator(filter.GaborishWeight1[component], filter.GaborishWeight2[component]);
                }
            }

            if (filter.Iterations == 3)
            {
                this.filters[count++] = Epf0Filter;
            }

            if (filter.Iterations != 0)
            {
                this.filters[count++] = Epf1Filter;
            }

            if (filter.Iterations >= 2)
            {
                this.filters[count++] = Epf2Filter;
            }

            this.filterCount = count;
            int offset = 0;
            for (int i = 0; i < count; i++)
            {
                int rows = Math.Min((2 * GetFilterRadius(this.filters[i])) + 1, size.Height);
                this.inputOffsets[i] = offset;
                this.inputRows[i] = rows;
                offset += 3 * rows * this.stride;
            }

            this.finalOffset = offset;
            offset += 3 * this.stride;
            for (int i = 0; i < count; i++)
            {
                if (this.filters[i] != GaborishFilter)
                {
                    this.sigmaOffsets[i] = offset;
                    if (TSigmaSource.IsConstant)
                    {
                        Span<float> retained = workspace.Slice(offset, this.retainedLength);
                        Span<float> sigma = workspace.Slice(offset + this.retainedLength, this.sigmaLength);
                        sigmaSource.PrepareRow(0, retained, sigma);
                    }

                    offset += this.retainedLength + this.sigmaLength;
                }
            }
        }

        /// <summary>
        /// Appends the next physical input row and advances the intermediate filters whose input is ready.
        /// </summary>
        /// <param name="sourceX">The next native X or first component row, with at least the coding width.</param>
        /// <param name="sourceY">The next native Y or second component row, with at least the coding width.</param>
        /// <param name="sourceB">The next native B or third component row, with at least the coding width.</param>
        /// <remarks>Drain <see cref="TryReadRow"/> before the next append. Input rows must cover the complete image in order.</remarks>
        public void AppendRows(ReadOnlySpan<float> sourceX, ReadOnlySpan<float> sourceY, ReadOnlySpan<float> sourceB)
        {
            int ringRows = this.filterCount == 0 ? 1 : this.inputRows[0];
            int slot = (this.receivedRows % ringRows) * this.stride;
            for (int component = 0; component < 3; component++)
            {
                ReadOnlySpan<float> source = component == 0 ? sourceX : component == 1 ? sourceY : sourceB;
                Span<float> row = this.workspace.Slice((component * ringRows * this.stride) + slot, this.stride);
                source[..this.size.Width].CopyTo(row.Slice(RestorationRadius, this.size.Width));
                PadRestorationRow(row, this.size.Width);
            }

            this.receivedRows++;

            // Advance intermediate filters even while the last filter still lacks lookahead. Otherwise
            // the raw ring could wrap before the first output requests an older intermediate input.
            // Keep bottom reflection deferred until Complete; advancing an entire bottom tail here could
            // overwrite rows still required by the caller's next final output neighborhood.
            for (int i = 0; i < this.filterCount - 1; i++)
            {
                while (this.CanProduceRow(i, this.producedRows[i]))
                {
                    this.ProduceRow(i);
                }
            }
        }

        /// <summary>
        /// Reads the next completed restored row, or reports that more input is required.
        /// </summary>
        /// <param name="row">The physical image row of the returned components.</param>
        /// <param name="outputX">The borrowed restored first component row.</param>
        /// <param name="outputY">The borrowed restored second component row.</param>
        /// <param name="outputB">The borrowed restored third component row.</param>
        /// <returns>Whether one output row is ready. Returned spans remain valid until the next append or read.</returns>
        public bool TryReadRow(out int row, out ReadOnlySpan<float> outputX, out ReadOnlySpan<float> outputY, out ReadOnlySpan<float> outputB)
        {
            row = this.nextOutputRow;
            outputX = default;
            outputY = default;
            outputB = default;
            if (row == this.size.Height)
            {
                return false;
            }

            int offset = this.finalOffset;
            if (this.filterCount == 0)
            {
                if (row == this.receivedRows)
                {
                    return false;
                }

                offset = 0;
            }
            else
            {
                int last = this.filterCount - 1;
                if (!this.complete && !this.CanProduceRow(last, row))
                {
                    return false;
                }

                // At EOF, request only the preceding rows needed by this output. Flushing every earlier
                // filter first would wrap its ring before the last filter consumed the pending bottom rows.
                this.EnsureRow(last, row);
            }

            outputX = this.workspace.Slice(offset + RestorationRadius, this.size.Width);
            outputY = this.workspace.Slice(offset + this.stride + RestorationRadius, this.size.Width);
            outputB = this.workspace.Slice(offset + (2 * this.stride) + RestorationRadius, this.size.Width);
            this.nextOutputRow++;
            return true;
        }

        /// <summary>
        /// Marks all physical input rows complete so subsequent reads can use mirrored bottom neighbors.
        /// </summary>
        public void Complete() => this.complete = true;

        /// <summary>
        /// Gets the vertical input radius of one filter in the fixed restoration order.
        /// </summary>
        /// <param name="kind">Gaborish, EPF0, EPF1 or EPF2, selected from the validated filter header.</param>
        /// <returns>The input radius in physical rows.</returns>
        private static int GetFilterRadius(int kind) => kind == Epf0Filter ? 3 : kind == Epf1Filter ? 2 : 1;

        /// <summary>
        /// Tests whether the next output has all non-reflected bottom input rows available.
        /// </summary>
        /// <param name="index">The active filter index.</param>
        /// <param name="row">The next physical output row for this filter.</param>
        /// <returns>Whether the current input prefix supplies the complete neighborhood.</returns>
        private readonly bool CanProduceRow(int index, int row)
        {
            int available = index == 0 ? this.receivedRows : this.producedRows[index - 1];
            return row < this.size.Height && (long)row + GetFilterRadius(this.filters[index]) < available;
        }

        /// <summary>
        /// Produces the requested physical filter row and any missing preceding-filter rows in order.
        /// </summary>
        /// <param name="index">The active filter index, with at most four filters in total.</param>
        /// <param name="row">The highest physical output row required by the next filter.</param>
        private void EnsureRow(int index, int row)
        {
            while (this.producedRows[index] <= row)
            {
                this.ProduceRow(index);
            }
        }

        /// <summary>
        /// Applies one existing restoration operator to its retained input rows and advances its physical position.
        /// </summary>
        /// <param name="index">The active filter whose next row is required.</param>
        private void ProduceRow(int index)
        {
            int row = this.producedRows[index];
            int kind = this.filters[index];
            int radius = GetFilterRadius(kind);
            if (index != 0)
            {
                int lastInput = (int)Math.Min(this.size.Height - 1L, (long)row + radius);
                this.EnsureRow(index - 1, lastInput);
            }

            int inputCount = this.inputRows[index];
            int inputOffset = this.inputOffsets[index];
            Span<float> input = this.workspace.Slice(inputOffset, 3 * inputCount * this.stride);
            bool last = index == this.filterCount - 1;
            int outputCount = last ? 1 : this.inputRows[index + 1];
            int outputOffset = last ? this.finalOffset : this.inputOffsets[index + 1];
            outputOffset += (row % outputCount) * this.stride;
            Span<float> destinationX = this.workspace.Slice(outputOffset + RestorationRadius, this.size.Width);
            Span<float> destinationY = this.workspace.Slice(outputOffset + (outputCount * this.stride) + RestorationRadius, this.size.Width);
            Span<float> destinationB = this.workspace.Slice(outputOffset + (2 * outputCount * this.stride) + RestorationRadius, this.size.Width);
            InlineArray8<int> rowStorage = default;
            Span<int> rows = rowStorage;
            PrepareRestorationRows(rows, row, this.size.Height, this.stride, inputCount);

            if (kind == GaborishFilter)
            {
                for (int component = 0; component < 3; component++)
                {
                    int componentOffset = component * inputCount * this.stride;
                    ReadOnlySpan<float> top = input.Slice(componentOffset + rows[RestorationRadius - 1] + RestorationRadius, this.size.Width);
                    ReadOnlySpan<float> middle = input.Slice(componentOffset + rows[RestorationRadius] + RestorationRadius, this.size.Width);
                    ReadOnlySpan<float> bottom = input.Slice(componentOffset + rows[RestorationRadius + 1] + RestorationRadius, this.size.Width);
                    Span<float> destination = component == 0 ? destinationX : component == 1 ? destinationY : destinationB;
                    FilterGaborishRow(top, middle, bottom, destination, this.gaborishOperations[component]);
                }
            }
            else
            {
                int sigmaOffset = this.sigmaOffsets[index];
                Span<float> retained = this.workspace.Slice(sigmaOffset, this.retainedLength);
                Span<float> sigma = this.workspace.Slice(sigmaOffset + this.retainedLength, this.sigmaLength);
                if (!TSigmaSource.IsConstant && (row & (BlockSize - 1)) == 0)
                {
                    this.sigmaSource.PrepareRow(row / BlockSize, retained, sigma);
                }

                float sigmaScale = kind == Epf0Filter ? this.filter.Pass0SigmaScale : kind == Epf2Filter ? this.filter.Pass2SigmaScale : 1F;
                float ordinaryMultiplier = sigmaScale * EpfDistanceScale;
                float borderMultiplier = ordinaryMultiplier * this.filter.BorderSadMultiplier;
                int blockRow = row & (BlockSize - 1);
                bool borderRow = blockRow == 0 || blockRow == BlockSize - 1;
                InlineArray32<float> multiplierStorage = default;
                Span<float> multipliers = multiplierStorage;
                for (int x = 0; x < multipliers.Length; x++)
                {
                    int blockColumn = x & (BlockSize - 1);
                    bool borderColumn = blockColumn == 0 || blockColumn == BlockSize - 1;
                    multipliers[x] = borderRow || borderColumn ? borderMultiplier : ordinaryMultiplier;
                }

                EpfOperator operation = new(input, rows, inputCount * this.stride, sigma, multipliers, this.filter.ChannelScale);
                if (kind == Epf0Filter)
                {
                    FilterEpfRow<Epf0Neighborhood>(operation, destinationX, destinationY, destinationB);
                }
                else if (kind == Epf1Filter)
                {
                    FilterEpfRow<Epf1Neighborhood>(operation, destinationX, destinationY, destinationB);
                }
                else
                {
                    FilterEpfRow<Epf2Neighborhood>(operation, destinationX, destinationY, destinationB);
                }
            }

            // Only a following filter reads horizontal neighbors. Final output needs visible samples only.
            // Padding uses the full image width; strip boundaries never become artificial image borders.
            if (!last)
            {
                for (int component = 0; component < 3; component++)
                {
                    PadRestorationRow(this.workspace.Slice(outputOffset + (component * outputCount * this.stride), this.stride), this.size.Width);
                }
            }

            this.producedRows[index]++;
        }
    }
}
