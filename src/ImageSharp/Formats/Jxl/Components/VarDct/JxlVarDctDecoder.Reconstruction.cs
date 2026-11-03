// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    private readonly List<Buffer2D<float>> reconstructedChannels = new(3);

    // Sequential output owns one [X | Y | B] group-row buffer. The region borrows the same allocation;
    // it lets the transform kernels address its rows without a second owner or nullable hot-path access.
    private Buffer2D<float>? reconstructedGroupRow;
    private Buffer2DRegion<float> reconstructedGroupRegion;

    // Restoration retains only its filter neighborhoods. The row processor borrows this memory once
    // before group traversal; the decoder releases it with the other reconstruction storage.
    private IMemoryOwner<float>? restorationWorkspace;
    private Memory<float> restorationMemory;
    private int reconstructedGroupTop;
    private int reconstructedGroupWidth;
    private IMemoryOwner<float>? reconstructionWorkspace;
    private Memory<float> reconstructionMemory;
    private InlineArray4<Size> reconstructedSizes;
    private int reconstructionBlockArea;
    private int inverseScratchLength;
    private Vector4 quantizationBias;
    private Vector3 quantizationScale;

    /// <summary>
    /// Gets the borrowed final-capacity color planes for native frame features.
    /// </summary>
    /// <remarks>
    /// Only the current reconstructed rectangle contains completed samples. Borrow these owners until
    /// the frame is disposed or transfers them to an LF slot. This view does not copy or transfer ownership.
    /// </remarks>
    public ReadOnlySpan<Buffer2D<float>> ReconstructedChannels => CollectionsMarshal.AsSpan(this.reconstructedChannels);

    /// <summary>
    /// Gets a value indicating whether completed group rows supply pixels without retaining full native color planes.
    /// </summary>
    public bool UsesGroupRows => this.reconstructedGroupRow is not null;

    /// <summary>
    /// Gets the padded component width in the combined group-row storage.
    /// </summary>
    public int GroupRowWidth => this.reconstructedGroupWidth;

    /// <summary>
    /// Allocates the component planes and one reusable transform workspace after AC metadata establishes the used strategies.
    /// </summary>
    /// <param name="allocator">The tracked frame allocator.</param>
    /// <param name="image">The image's coefficient biases.</param>
    /// <param name="frame">The coding dimensions, sampling shifts and component matrix scales.</param>
    public void PrepareReconstruction(MemoryAllocator allocator, JxlHeader image, JxlFrameHeader frame)
        => this.PrepareReconstruction(allocator, image, frame, false);

    /// <summary>
    /// Prepares transform scratch and either retained native planes or one reusable horizontal group row.
    /// </summary>
    /// <param name="allocator">The tracked frame allocator.</param>
    /// <param name="image">The image's coefficient biases.</param>
    /// <param name="frame">The coding geometry and reconstruction parameters.</param>
    /// <param name="useGroupRows">Whether the caller consumes restored rows before the next group row is decoded.</param>
    public void PrepareReconstruction(MemoryAllocator allocator, JxlHeader image, JxlFrameHeader frame, bool useGroupRows)
    {
        this.quantizationBias = image.QuantizationBias;

        // Matrix scales apply to X and B residuals before correlation with Y. Prepare the two powers once,
        // independently of the per-block quantizer. A scale field of two leaves that component unchanged.
        this.quantizationScale = new Vector3(
            MathF.Pow(0.8F, frame.XQuantizationScale - 2),
            1F,
            MathF.Pow(0.8F, frame.BQuantizationScale - 2));

        for (byte strategy = 0; strategy < StrategyCount; strategy++)
        {
            if ((this.UsedStrategies & (1U << strategy)) != 0)
            {
                int area = CoefficientBlockArea * StrategyWidths[strategy] * StrategyHeights[strategy];
                this.reconstructionBlockArea = Math.Max(this.reconstructionBlockArea, area);
                this.inverseScratchLength = Math.Max(this.inverseScratchLength, JxlInverseTransform.GetScratchLength(strategy));
            }
        }

        // Retain three dequantized coefficient planes and one spatial block. The inverse worker borrows
        // its own final scratch range. All blocks and components reuse these ranges without another allocation.
        this.reconstructionWorkspace = allocator.Allocate<float>((4 * this.reconstructionBlockArea) + this.inverseScratchLength);
        this.reconstructionMemory = this.reconstructionWorkspace.Memory;
        Size size = frame.CodingSize;
        if (useGroupRows && 3L * this.dcSize.Width * BlockSize <= int.MaxValue)
        {
            // Each physical row contains [X | Y | B]. One allocation avoids rounding three separate planes
            // to pool blocks. Pad to complete transform blocks; only visible samples reach pixel conversion.
            // Eight-row alignment keeps ordinary 8-by-8 transforms within one backing block. Very wide
            // rows use ordinary row alignment; transforms crossing a backing block use the spatial scratch.
            this.reconstructedGroupWidth = this.dcSize.Width * BlockSize;
            int height = Math.Min(frame.GroupSize, this.dcSize.Height * BlockSize);
            int stride = 3 * this.reconstructedGroupWidth;
            int alignment = (long)stride * BlockSize * sizeof(float) <= allocator.GetBufferCapacityInBytes() ? BlockSize : 1;
            this.reconstructedGroupRow = allocator.Allocate2DOveraligned<float>(stride, height, alignment);
            this.reconstructedGroupRegion = this.reconstructedGroupRow.GetRegion();
            if (frame.RestorationFilter.Gaborish || frame.RestorationFilter.Iterations != 0)
            {
                int restorationLength = JxlRestoration.GetWorkspaceLength(size, frame.RestorationFilter, this.dcSize.Width, this.dcSize.Width);
                this.restorationWorkspace = allocator.Allocate<float>(restorationLength);
                this.restorationMemory = this.restorationWorkspace.Memory;
            }

            return;
        }

        for (int component = 0; component < 3; component++)
        {
            int horizontal = this.horizontalShifts[component];
            int vertical = this.verticalShifts[component];
            int width = ((size.Width - 1) >> horizontal) + 1;
            int height = ((size.Height - 1) >> vertical) + 1;

            // Reserve the frame output dimensions once, including LF reduction. Native samples initially occupy this
            // logical rectangle; chroma and frame upsampling reuse the same backing plane before filtering/output.
            this.reconstructedSizes[component] = new Size(width, height);
            this.reconstructedChannels.Add(allocator.Allocate2D<float>(frame.OutputSize));
        }
    }

    /// <summary>
    /// Borrows the current reconstructed component rectangle, before final color conversion.
    /// </summary>
    /// <param name="component">The X, Y or B component index.</param>
    /// <returns>The visible component plane. Reconstruction samples retain their float precision.</returns>
    public Buffer2DRegion<float> GetReconstructedChannel(int component)
        => this.reconstructedChannels[component].GetRegion(new Rectangle(Point.Empty, this.reconstructedSizes[component]));

    /// <summary>
    /// Selects the global top row whose transforms will next populate the reusable group-row storage.
    /// </summary>
    /// <param name="top">The group row's top in full coding-resolution pixels.</param>
    public void BeginGroupRow(int top) => this.reconstructedGroupTop = top;

    /// <summary>
    /// Borrows one completed native row from the current group row.
    /// </summary>
    /// <param name="row">The global coding-resolution row.</param>
    /// <returns>The physical row, laid out as equally sized padded X, Y and B spans.</returns>
    public Span<float> GetGroupRow(int row)
        => this.reconstructedGroupRegion.DangerousGetRowSpan(row - this.reconstructedGroupTop);

    /// <summary>
    /// Transfers the three completed native float planes to the decoder's saved LF slot without copying samples.
    /// </summary>
    /// <returns>The X, Y and B plane owners. The caller must dispose them after their final dependent frame.</returns>
    /// <remarks>
    /// Call this after restoration and frame upsampling. This owner must not reconstruct or read color planes after transfer.
    /// </remarks>
    public Buffer2D<float>[] TakeReconstructedChannels()
    {
        // Build the complete result before removing ownership. If the array allocation fails, Dispose still releases the planes.
        Buffer2D<float>[] result = this.reconstructedChannels.ToArray();
        this.reconstructedChannels.Clear();
        return result;
    }

    /// <summary>
    /// Reconstructs one fully accumulated transform block before the entropy traversal advances to the next block.
    /// </summary>
    /// <typeparam name="TCoefficient">The frame-selected short or int coefficient storage.</typeparam>
    /// <param name="bx">The transform origin in the full horizontal block grid.</param>
    /// <param name="by">The transform origin in the full vertical block grid.</param>
    /// <param name="strategy">The validated serialized transform strategy.</param>
    /// <param name="blockWidth">The transform width in eight-pixel blocks.</param>
    /// <param name="blockHeight">The transform height in eight-pixel blocks.</param>
    /// <param name="active">A bit for each component present at this sampling-aligned origin.</param>
    /// <param name="hasAc">A bit for each component that received AC in any progressive pass.</param>
    /// <param name="quantizedX">The complete accumulated X residual block, or zeros for an inactive component.</param>
    /// <param name="quantizedY">The complete accumulated Y block, or zeros for an inactive component.</param>
    /// <param name="quantizedB">The complete accumulated B residual block, or zeros for an inactive component.</param>
    /// <param name="matrices">The three decoder-matrix planes for this transform.</param>
    /// <param name="workspace">The frame-owned coefficient, spatial and inverse-transform scratch ranges.</param>
    /// <param name="scale">The inverse global scale divided by this block's quantizer.</param>
    /// <param name="correlation">The complete Y-to-X and Y-to-B ratios for the transform's color tile.</param>
    private void ReconstructBlock<TCoefficient>(
        int bx,
        int by,
        byte strategy,
        int blockWidth,
        int blockHeight,
        int active,
        int hasAc,
        ReadOnlySpan<TCoefficient> quantizedX,
        ReadOnlySpan<TCoefficient> quantizedY,
        ReadOnlySpan<TCoefficient> quantizedB,
        ReadOnlySpan<float> matrices,
        Span<float> workspace,
        float scale,
        Vector2 correlation)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        int area = quantizedX.Length;
        Span<float> coefficients = workspace[..(3 * area)];
        Span<float> spatial = workspace.Slice(3 * this.reconstructionBlockArea, this.reconstructionBlockArea);
        Span<float> scratch = workspace.Slice(4 * this.reconstructionBlockArea, this.inverseScratchLength);
        int dcOnly = 0;
        if (area == CoefficientBlockArea)
        {
            // A single-block transform without AC produces one constant DC value. AC presence includes
            // every progressive pass. Y correlation can supply AC to X or B even without its own residual.
            dcOnly = (hasAc & 2) == 0 ? 2 : 0;
            dcOnly |= (hasAc & 1) == 0 && ((hasAc & 2) == 0 || correlation.X == 0F) ? 1 : 0;
            dcOnly |= (hasAc & 4) == 0 && ((hasAc & 2) == 0 || correlation.Y == 0F) ? 4 : 0;
        }

        if ((dcOnly & active) != active)
        {
            DequantizeAcBlock(
                quantizedX, quantizedY, quantizedB, matrices, coefficients, this.quantizationScale * scale, correlation, this.quantizationBias);
        }

        for (int component = 0; component < 3; component++)
        {
            if ((active & (1 << component)) == 0)
            {
                continue;
            }

            int x = bx >> this.horizontalShifts[component];
            int y = by >> this.verticalShifts[component];
            Buffer2DRegion<float> output;
            Size outputSize;
            if (this.UsesGroupRows)
            {
                Rectangle bounds = new(component * this.reconstructedGroupWidth, 0, this.reconstructedGroupWidth, this.reconstructedGroupRegion.Height);
                output = this.reconstructedGroupRegion.GetSubRegion(bounds);
                outputSize = new Size(this.dcSize.Width * BlockSize, this.dcSize.Height * BlockSize);
            }
            else
            {
                output = this.reconstructedChannels[component].GetRegion();
                outputSize = this.reconstructedSizes[component];
            }

            int left = x * BlockSize;
            int top = y * BlockSize;

            // Sampling-factor padding can add whole blocks outside a component's visible dimensions.
            // Their coefficients have already been decoded, but they supply no samples to reconstruct.
            if (left >= outputSize.Width || top >= outputSize.Height)
            {
                continue;
            }

            Buffer2DRegion<float> dc = this.GetDcChannel(component).GetSubRegion(new Rectangle(x, y, blockWidth, blockHeight));
            int outputTop = this.UsesGroupRows ? top - this.reconstructedGroupTop : top;
            if ((dcOnly & (1 << component)) != 0)
            {
                // Fill visible rows through the existing buffer API. Partial edge blocks and split
                // allocations receive the same constant samples as a complete inverse transform.
                int dcWidth = Math.Min(BlockSize, outputSize.Width - left);
                int dcHeight = Math.Min(BlockSize, outputSize.Height - top);
                float value = dc.DangerousGetRowSpan(0)[0];
                for (int row = 0; row < dcHeight; row++)
                {
                    output.DangerousGetRowSpan(outputTop + row).Slice(left, dcWidth).Fill(value);
                }

                continue;
            }

            int width = Math.Min(blockWidth * BlockSize, outputSize.Width - left);
            int height = Math.Min(blockHeight * BlockSize, outputSize.Height - top);
            int outputLeft = output.Bounds.X + left;
            long footprint = ((long)(height - 1) * output.Stride) + outputLeft + width;
            if (width == blockWidth * BlockSize && height == blockHeight * BlockSize && footprint <= int.MaxValue
                && output.Buffer.DangerousTryGetPaddedRowSpan(outputTop + output.Bounds.Y, (int)footprint - output.Buffer.Width, out Span<float> direct))
            {
                // The inverse kernel already accepts a physical row stride. Write a complete block directly
                // when all its rows share one backing allocation. Padding in the group strip includes edge blocks.
                JxlInverseTransform.Transform(strategy, coefficients.Slice(component * area, area), dc, direct[outputLeft..], output.Stride, scratch);
                continue;
            }

            // Partial retained-image edges and blocks crossing allocation boundaries still need contiguous
            // transform scratch. Copy only visible samples; keep the complete padded inverse arithmetic.
            JxlInverseTransform.Transform(
                strategy, coefficients.Slice(component * area, area), dc, spatial[..area], blockWidth * BlockSize, scratch);

            for (int row = 0; row < height; row++)
            {
                spatial.Slice(row * blockWidth * BlockSize, width).CopyTo(output.DangerousGetRowSpan(outputTop + row).Slice(left, width));
            }
        }
    }

    /// <summary>
    /// Releases the reconstructed component planes and the frame's reusable transform workspace.
    /// </summary>
    private void DisposeReconstruction()
    {
        this.reconstructedGroupRow?.Dispose();
        this.restorationWorkspace?.Dispose();
        this.reconstructionWorkspace?.Dispose();
        foreach (Buffer2D<float> channel in this.reconstructedChannels)
        {
            channel.Dispose();
        }
    }
}
