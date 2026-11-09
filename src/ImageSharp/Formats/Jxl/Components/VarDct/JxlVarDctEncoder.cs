// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

/// <summary>
/// Transforms reusable XYB groups and retains the quantized coefficients needed by the frame sections.
/// </summary>
internal sealed class JxlVarDctEncoder : IDisposable
{
    /// <summary>
    /// The spatial width and height of the fast encoder's transform.
    /// </summary>
    private const int BlockDimension = 8;

    /// <summary>
    /// The coefficient count of one eight-by-eight transform.
    /// </summary>
    private const int CoefficientsPerBlock = BlockDimension * BlockDimension;

    /// <summary>
    /// The maximum DC samples across a default DC group: eight 256-pixel groups divided into eight-pixel blocks.
    /// </summary>
    private const int DcGroupBlockWidth = 256;

    /// <summary>
    /// Three padded component planes reused after each group has been transformed.
    /// </summary>
    private readonly Buffer2D<float> colors;

    /// <summary>
    /// One 64-value row per component and spatial block. Small rows preserve segmented allocator support.
    /// </summary>
    private readonly Buffer2D<int> coefficients;

    /// <summary>
    /// Quantized DC residuals in X, Y and B plane order, retained until all DC sections have been written.
    /// </summary>
    private readonly Buffer2D<int> dc;

    /// <summary>
    /// The optional derived alpha samples. Supplied extra channels retain their existing owners.
    /// </summary>
    private readonly Buffer2D<int>? alpha;

    /// <summary>
    /// The borrowed alpha region prepared only when pixel conversion supplies a derived alpha channel.
    /// </summary>
    private readonly Buffer2DRegion<int> alphaRegion;

    /// <summary>
    /// The number of bounded alpha tile rows stored for each full-resolution image row.
    /// </summary>
    private readonly int alphaGroupColumns;

    /// <summary>
    /// The frame width in eight-sample blocks.
    /// </summary>
    private readonly int blockColumns;

    /// <summary>
    /// The frame height in eight-sample blocks.
    /// </summary>
    private readonly int blockRows;

    /// <summary>
    /// The horizontal DC tiles retained for each component and full-frame block row.
    /// </summary>
    private readonly int dcTileColumns;

    /// <summary>
    /// The quantizers and component multipliers prepared once for the frame.
    /// </summary>
    private readonly JxlQuantization.EncoderSettings settings;

    /// <summary>
    /// The default DCT8 quantization weights in three contiguous component planes.
    /// </summary>
    private InlineArray256<float> weights;

    /// <summary>
    /// Reciprocal matrix factors used to reconstruct quantized Y before B correlation removal.
    /// </summary>
    private InlineArray256<float> dequantization;

    /// <summary>
    /// Three 64-value coefficient planes reused by the forward transforms and AC quantizer.
    /// </summary>
    private InlineArray256<float> transformed;

    /// <summary>
    /// Three eight-value batches of at most eight lanes for the shared forward transform.
    /// </summary>
    private InlineArray256<float> axisScratch;

    /// <summary>
    /// The forward transform's transpose plane, reused across all components and blocks.
    /// </summary>
    private InlineArray64<float> temporary;

    /// <summary>
    /// A spatial tile used only when an allocator segment divides the eight source rows.
    /// </summary>
    private InlineArray64<float> sourceTile;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlVarDctEncoder"/> class.
    /// </summary>
    /// <param name="allocator">The operation's allocator. All retained frame and group planes use this owner.</param>
    /// <param name="size">The validated frame dimensions.</param>
    /// <param name="distance">The positive perceptual distance selected at the encoder boundary.</param>
    /// <param name="retainAlpha">Whether the pixel conversion supplies a derived alpha plane.</param>
    public JxlVarDctEncoder(MemoryAllocator allocator, Size size, float distance, bool retainAlpha)
    {
        this.blockColumns = ((size.Width - 1) / BlockDimension) + 1;
        this.blockRows = ((size.Height - 1) / BlockDimension) + 1;
        this.dcTileColumns = ((this.blockColumns - 1) / DcGroupBlockWidth) + 1;
        this.GroupWidth = Math.Min(JxlEncoderCore.GroupDimension, this.blockColumns * BlockDimension);
        this.GroupHeight = Math.Min(JxlEncoderCore.GroupDimension, this.blockRows * BlockDimension);
        this.settings = JxlQuantization.CreateEncoderSettings(distance);

        // Matrix generation and reconstruction share the decoder's weight and bias owner. Retain both
        // directions because AC quantization uses weights, but the Y round trip needs their reciprocals.
        // Prepare fixed inline storage before renting planes, so matrix setup cannot retain a partial allocation.
        InlineArray128<float> parametersStorage = default;
        Span<float> parameters = parametersStorage;
        JxlQuantization.MatrixDescription description = JxlQuantization.GetLibraryMatrix(0, parameters);
        Span<float> weights = this.weights;
        JxlQuantization.GenerateMatrix(description, parameters, BlockDimension, BlockDimension, weights[..192], CancellationToken.None);
        Span<float> dequantization = this.dequantization;
        weights[..192].CopyTo(dequantization);
        JxlQuantization.InvertQuantizationWeights(dequantization[..192]);

        // The three input planes are group-sized. Coefficients and DC must outlive that input because
        // DC sections precede AC sections, and progressive passes revisit the same quantized blocks.
        // Every coefficient row holds one block, so the allocator never needs a full coefficient scanline.
        Buffer2D<float> colors = allocator.Allocate2D<float>(this.GroupWidth, 3 * this.GroupHeight);
        Buffer2D<int>? coefficients = null;
        Buffer2D<int>? dc = null;
        Buffer2D<int>? alpha = null;
        try
        {
            coefficients = allocator.Allocate2D<int>(CoefficientsPerBlock, checked(3 * this.blockColumns * this.blockRows));

            // DC sections consume at most 256 block samples across. Store that same bounded tile row
            // instead of requiring the allocator to align a full image-width DC scanline.
            // Only the final horizontal tile has unused columns, which are excluded from serialization.
            dc = allocator.Allocate2D<int>(Math.Min(this.blockColumns, DcGroupBlockWidth), checked(3 * this.blockRows * this.dcTileColumns));

            if (retainAlpha)
            {
                // Each row covers one horizontal coding group. This bounds its contiguous allocation
                // to 256 integers even when a source pixel row uses a smaller sample carrier.
                // The last tile reserves unused columns up to that width; callers never read or write them.
                this.alphaGroupColumns = ((size.Width - 1) / JxlEncoderCore.GroupDimension) + 1;
                int alphaWidth = Math.Min(size.Width, JxlEncoderCore.GroupDimension);
                alpha = allocator.Allocate2D<int>(alphaWidth, checked(this.alphaGroupColumns * size.Height));
                this.alphaRegion = alpha.GetRegion();
            }

            this.colors = colors;
            this.coefficients = coefficients;
            this.dc = dc;
            this.alpha = alpha;
        }
        catch
        {
            alpha?.Dispose();
            dc?.Dispose();
            coefficients?.Dispose();
            colors.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the padded width of each reusable input plane.
    /// </summary>
    public int GroupWidth { get; }

    /// <summary>
    /// Gets the padded height of each reusable input plane.
    /// </summary>
    public int GroupHeight { get; }

    /// <summary>
    /// Gets the frame's encoded global quantization scale.
    /// </summary>
    public int GlobalScale => this.settings.GlobalScale;

    /// <summary>
    /// Gets the frame's encoded DC quantizer.
    /// </summary>
    public int DcQuantizer => this.settings.DcQuantizer;

    /// <summary>
    /// Gets the uniform encoded AC quantizer.
    /// </summary>
    public int AcQuantizer => this.settings.AcQuantizer;

    /// <summary>
    /// Gets the encoded X matrix scale.
    /// </summary>
    public int XQuantizationScale => this.settings.XQuantizationScale;

    /// <summary>
    /// Gets the encoded B matrix scale.
    /// </summary>
    public int BQuantizationScale => this.settings.BQuantizationScale;

    /// <summary>
    /// Gets the number of edge-preserving filter iterations selected for the distance.
    /// </summary>
    public int EpfIterations => this.settings.EpfIterations;

    /// <summary>
    /// Gets one padded group row for pixel conversion. The caller writes only the visible source width.
    /// </summary>
    /// <param name="component">The X, Y or B component index.</param>
    /// <param name="y">The row relative to the current coding group.</param>
    /// <returns>The borrowed row. It remains valid until this owner is disposed.</returns>
    public Span<float> GetColorRow(int component, int y) => this.colors.DangerousGetRowSpan((component * this.GroupHeight) + y);

    /// <summary>
    /// Gets one coding group's derived alpha samples on an absolute image row.
    /// </summary>
    /// <param name="groupX">The horizontal group origin in pixels, aligned to 256. The caller selects a single coding group.</param>
    /// <param name="y">The absolute image row.</param>
    /// <returns>The borrowed tile row. The caller uses only the visible group width and has requested alpha storage.</returns>
    public Span<int> GetAlphaRow(int groupX, int y)
        => this.alphaRegion.DangerousGetRowSpan((y * this.alphaGroupColumns) + (groupX / JxlEncoderCore.GroupDimension));

    /// <summary>
    /// Gets the retained quantized AC coefficients of one eight-by-eight block.
    /// </summary>
    /// <param name="component">The X, Y or B component index.</param>
    /// <param name="blockX">The horizontal block coordinate in the frame.</param>
    /// <param name="blockY">The vertical block coordinate in the frame.</param>
    /// <returns>The 64 coefficients in the shared DCT layout. Slot zero belongs to the separate DC plane and remains zero here.</returns>
    public Span<int> GetCoefficientBlock(int component, int blockX, int blockY)
        => this.coefficients.DangerousGetRowSpan((((component * this.blockRows) + blockY) * this.blockColumns) + blockX);

    /// <summary>
    /// Borrows consecutive coefficient blocks when they share one allocator segment.
    /// </summary>
    /// <param name="component">The X, Y or B component index.</param>
    /// <param name="blockX">The first horizontal block coordinate in the frame.</param>
    /// <param name="blockY">The vertical block coordinate in the frame.</param>
    /// <param name="blockCount">The positive number of blocks within that spatial block row.</param>
    /// <param name="blocks">The borrowed blocks, each with 64 coefficients, or an empty span when a segment divides them.</param>
    /// <returns>Whether all requested blocks share one contiguous span.</returns>
    public bool TryGetCoefficientRow(int component, int blockX, int blockY, int blockCount, out Span<int> blocks)
    {
        // Each retained row holds one block. Borrow the first row and the following block rows together
        // only when their full footprint fits one segment. The caller uses GetCoefficientBlock otherwise.
        int row = (((component * this.blockRows) + blockY) * this.blockColumns) + blockX;

        return this.coefficients.DangerousTryGetPaddedRowSpan(row, (blockCount - 1) * CoefficientsPerBlock, out blocks);
    }

    /// <summary>
    /// Gets one retained quantized DC tile row, starting at the requested block column.
    /// </summary>
    /// <param name="component">The X, Y or B component index.</param>
    /// <param name="blockX">The first block column. The caller stays within its 256-block DC tile.</param>
    /// <param name="blockY">The vertical block coordinate in the frame.</param>
    /// <returns>The borrowed remainder of that tile row. Serialization excludes edge padding and writes Y before X and B.</returns>
    public Span<int> GetDcRow(int component, int blockX, int blockY)
    {
        // Rows are ordered by component, image block row, then horizontal DC tile.
        // The slice places the caller's first block at index zero without copying samples.
        int tile = blockX / DcGroupBlockWidth;
        int row = (((component * this.blockRows) + blockY) * this.dcTileColumns) + tile;

        return this.dc.DangerousGetRowSpan(row)[(blockX % DcGroupBlockWidth)..];
    }

    /// <summary>
    /// Pads the visible group edges, transforms its blocks and retains their DC and AC quantization results.
    /// </summary>
    /// <param name="bounds">The visible group rectangle in frame coordinates. Pixel conversion has filled its input rows.</param>
    /// <param name="cancellationToken">The token checked between block rows.</param>
    public void ProcessGroup(Rectangle bounds, CancellationToken cancellationToken)
    {
        int width = ((bounds.Width + BlockDimension - 1) / BlockDimension) * BlockDimension;
        int height = ((bounds.Height + BlockDimension - 1) / BlockDimension) * BlockDimension;

        // VarDCT extends the last visible sample, then the last already-extended row. This matches
        // image-edge padding without retaining or copying a full floating-point frame.
        for (int component = 0; component < 3; component++)
        {
            for (int y = 0; y < bounds.Height; y++)
            {
                Span<float> row = this.GetColorRow(component, y);
                row.Slice(bounds.Width, width - bounds.Width).Fill(row[bounds.Width - 1]);
            }

            ReadOnlySpan<float> last = this.GetColorRow(component, bounds.Height - 1)[..width];

            for (int y = bounds.Height; y < height; y++)
            {
                last.CopyTo(this.GetColorRow(component, y));
            }
        }

        Span<float> transformed = this.transformed;
        Span<float> temporary = this.temporary;
        Span<float> axisScratch = this.axisScratch;
        Span<float> sourceTile = this.sourceTile;
        ReadOnlySpan<float> weights = this.weights;
        ReadOnlySpan<float> dequantization = this.dequantization;
        float dcScale = this.settings.Scale * this.DcQuantizer;
        Vector3 dcFactors = JxlQuantization.DefaultDcFactors;
        Vector3 inverseDcStep = (Vector3.One / dcFactors) * dcScale;
        float yDcStep = ((65536F / this.GlobalScale) / this.DcQuantizer) * dcFactors.Y;
        int firstBlock = bounds.X / BlockDimension;

        for (int y = 0; y < height; y += BlockDimension)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int blockY = (bounds.Y + y) / BlockDimension;
            Span<int> dcX = this.GetDcRow(0, firstBlock, blockY);
            Span<int> dcY = this.GetDcRow(1, firstBlock, blockY);
            Span<int> dcB = this.GetDcRow(2, firstBlock, blockY);

            // The eight-row footprint stays the same across this block row. Resolve its allocator
            // segment once per component. A failed query returns an empty span and uses the retained tile below.
            _ = this.colors.DangerousTryGetPaddedRowSpan(y, (BlockDimension - 1) * this.GroupWidth, out Span<float> sourceX);
            _ = this.colors.DangerousTryGetPaddedRowSpan(
                this.GroupHeight + y,
                (BlockDimension - 1) * this.GroupWidth,
                out Span<float> sourceY);

            _ = this.colors.DangerousTryGetPaddedRowSpan(
                (2 * this.GroupHeight) + y,
                (BlockDimension - 1) * this.GroupWidth,
                out Span<float> sourceB);

            for (int x = 0; x < width; x += BlockDimension)
            {
                for (int component = 0; component < 3; component++)
                {
                    int row = (component * this.GroupHeight) + y;
                    ReadOnlySpan<float> rows = component switch
                    {
                        0 => sourceX,
                        1 => sourceY,
                        _ => sourceB
                    };

                    ReadOnlySpan<float> source;
                    int stride;

                    // Borrow all eight rows when this tile stays in one allocator segment. Only a
                    // split tile needs staging; its single retained buffer also serves every component.
                    if (!rows.IsEmpty)
                    {
                        source = rows[x..];
                        stride = this.GroupWidth;
                    }
                    else
                    {
                        for (int dy = 0; dy < BlockDimension; dy++)
                        {
                            this.colors.DangerousGetRowSpan(row + dy).Slice(x, BlockDimension)
                                .CopyTo(sourceTile.Slice(dy * BlockDimension, BlockDimension));
                        }

                        source = sourceTile;
                        stride = BlockDimension;
                    }

                    JxlDct.ForwardDct(
                        source,
                        stride,
                        BlockDimension,
                        BlockDimension,
                        transformed.Slice(component * CoefficientsPerBlock, CoefficientsPerBlock),
                        temporary,
                        axisScratch);
                }

                int localBlockX = x / BlockDimension;
                int blockX = firstBlock + localBlockX;

                // The DC path rounds halves away from zero. Its B residual uses rounded Y, while
                // AC uses bias-adjusted Y reconstruction inside the shared quantizer. Keep those stages distinct.
                int quantizedY = (int)MathF.Round(transformed[64] * inverseDcStep.Y, MidpointRounding.AwayFromZero);
                dcY[localBlockX] = quantizedY;
                dcX[localBlockX] = (int)MathF.Round(transformed[0] * inverseDcStep.X, MidpointRounding.AwayFromZero);
                dcB[localBlockX] =
                    (int)MathF.Round((transformed[128] - (quantizedY * yDcStep)) * inverseDcStep.Z, MidpointRounding.AwayFromZero);

                JxlQuantization.QuantizeDct8Block(
                    transformed[..192],
                    weights[..192],
                    dequantization[..192],
                    this.GetCoefficientBlock(0, blockX, blockY),
                    this.GetCoefficientBlock(1, blockX, blockY),
                    this.GetCoefficientBlock(2, blockX, blockY),
                    in this.settings,
                    new Vector2(0F, 1F));
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.alpha?.Dispose();
        this.dc.Dispose();
        this.coefficients.Dispose();
        this.colors.Dispose();
    }
}
