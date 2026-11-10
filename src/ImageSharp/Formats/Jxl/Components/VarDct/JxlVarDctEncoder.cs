// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

/// <summary>
/// Transforms reusable XYB block rows and retains the quantized coefficients needed by the frame sections.
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
    /// Three padded component planes reused after each eight-pixel-high block row has been transformed.
    /// </summary>
    private readonly Buffer2D<float> colors;

    /// <summary>
    /// One 64-value row per component and spatial block, including DC in slot zero. Small rows preserve segmented allocator support.
    /// </summary>
    private readonly Buffer2D<int> coefficients;

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
        this.GroupWidth = Math.Min(JxlEncoderCore.GroupDimension, this.blockColumns * BlockDimension);
        this.GroupHeight = BlockDimension;
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

        // The DCT consumes eight source rows at a time. Reuse one block row per component instead of
        // retaining converted samples for a whole coding group. Coefficients and DC must outlive this
        // input because DC sections precede AC sections, and progressive passes revisit the same blocks.
        // Every coefficient row holds one block, so the allocator never needs a full coefficient scanline.
        Buffer2D<float> colors = allocator.Allocate2D<float>(this.GroupWidth, 3 * this.GroupHeight);
        Buffer2D<int>? coefficients = null;
        Buffer2D<int>? alpha = null;
        try
        {
            coefficients = allocator.Allocate2D<int>(CoefficientsPerBlock, checked(3 * this.blockColumns * this.blockRows));

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
            this.alpha = alpha;
        }
        catch
        {
            alpha?.Dispose();
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
    /// Gets the eight-row height of each reusable input plane.
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
    /// Gets one padded input row for pixel conversion. The caller writes only the visible source width.
    /// </summary>
    /// <param name="component">The X, Y or B component index.</param>
    /// <param name="y">The row relative to the current block row.</param>
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
    /// Gets the retained quantized coefficients of one eight-by-eight block.
    /// </summary>
    /// <param name="component">The X, Y or B component index.</param>
    /// <param name="blockX">The horizontal block coordinate in the frame.</param>
    /// <param name="blockY">The vertical block coordinate in the frame.</param>
    /// <returns>The 64 coefficients in the shared DCT layout. Slot zero holds DC; the AC stream visits only slots one through sixty-three.</returns>
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
    /// Copies the retained DC coefficients of one block row into the caller's reusable row scratch.
    /// </summary>
    /// <param name="component">The X, Y or B component index.</param>
    /// <param name="blockX">The first block column.</param>
    /// <param name="blockY">The vertical block coordinate in the frame.</param>
    /// <param name="destination">The DC row to fill. Its length stays within the selected component's block row.</param>
    public void CopyDcRow(int component, int blockX, int blockY, Span<int> destination)
    {
        long offset = (((((long)component * this.blockRows) + blockY) * this.blockColumns) + blockX) * CoefficientsPerBlock;

        while (!destination.IsEmpty)
        {
            // Every allocator segment contains complete 64-value blocks. Limit this run to its
            // current segment, so no gather lane can read through a segment boundary.
            Span<int> source = this.coefficients.FastMemoryGroup.GetRemainingSliceOfBuffer(offset);
            int count = Math.Min(destination.Length, source.Length / CoefficientsPerBlock);
            ref int input = ref MemoryMarshal.GetReference(source);
            ref int output = ref MemoryMarshal.GetReference(destination);
            int i = 0;

            // Lane k reads slot zero of block i+k, at element offset (i+k)*64. The shared gather
            // helpers handle each supported architecture; stores pack those DC values contiguously.
            // Descending vector widths process complete batches. Scalar loads handle the remainder
            // and the whole run on targets without hardware intrinsics.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectors = (count - i).Vector512Count<int>();
                for (nuint vector = 0; vector < vectors; vector++, i += Vector512<int>.Count)
                {
                    Vector512<int> indices = (Vector512.Create(i) + Vector512<int>.Indices) * Vector512.Create(CoefficientsPerBlock);
                    Vector512_.Gather(ref input, indices).StoreUnsafe(ref output, (nuint)i);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectors = (count - i).Vector256Count<int>();
                for (nuint vector = 0; vector < vectors; vector++, i += Vector256<int>.Count)
                {
                    Vector256<int> indices = (Vector256.Create(i) + Vector256<int>.Indices) * Vector256.Create(CoefficientsPerBlock);
                    Vector256_.Gather(ref input, indices).StoreUnsafe(ref output, (nuint)i);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectors = (count - i).Vector128Count<int>();
                for (nuint vector = 0; vector < vectors; vector++, i += Vector128<int>.Count)
                {
                    Vector128<int> indices = (Vector128.Create(i) + Vector128<int>.Indices) * Vector128.Create(CoefficientsPerBlock);
                    Vector128_.Gather(ref input, indices).StoreUnsafe(ref output, (nuint)i);
                }
            }

            for (; i < count; i++)
            {
                destination[i] = source[i * CoefficientsPerBlock];
            }

            offset += (long)count * CoefficientsPerBlock;
            destination = destination[count..];
        }
    }

    /// <summary>
    /// Pads the visible block row, transforms its blocks and retains their DC and AC quantization results.
    /// </summary>
    /// <param name="bounds">The visible block row in frame coordinates. Pixel conversion has filled its input rows.</param>
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
                int quantizedX = (int)MathF.Round(transformed[0] * inverseDcStep.X, MidpointRounding.AwayFromZero);
                int quantizedB =
                    (int)MathF.Round((transformed[128] - (quantizedY * yDcStep)) * inverseDcStep.Z, MidpointRounding.AwayFromZero);

                Span<int> coefficientsX = this.GetCoefficientBlock(0, blockX, blockY);
                Span<int> coefficientsY = this.GetCoefficientBlock(1, blockX, blockY);
                Span<int> coefficientsB = this.GetCoefficientBlock(2, blockX, blockY);
                JxlQuantization.QuantizeDct8Block(
                    transformed[..192],
                    weights[..192],
                    dequantization[..192],
                    coefficientsX,
                    coefficientsY,
                    coefficientsB,
                    in this.settings,
                    new Vector2(0F, 1F));

                // AC quantization clears slot zero. Store the separately quantized DC values after
                // that call, in the blocks already retained for every pass, without another DC plane.
                coefficientsX[0] = quantizedX;
                coefficientsY[0] = quantizedY;
                coefficientsB[0] = quantizedB;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.alpha?.Dispose();
        this.coefficients.Dispose();
        this.colors.Dispose();
    }
}
