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
    private IMemoryOwner<float>? reconstructionWorkspace;
    private Memory<float> reconstructionMemory;
    private InlineArray4<Size> reconstructedSizes;
    private int reconstructionBlockArea;
    private int inverseScratchLength;
    private Vector4 quantizationBias;
    private Vector3 quantizationScale;

    /// <summary>
    /// Allocates the component planes and one reusable transform workspace after AC metadata establishes the used strategies.
    /// </summary>
    /// <param name="allocator">The tracked frame allocator.</param>
    /// <param name="image">The image's coefficient biases.</param>
    /// <param name="frame">The coding dimensions, sampling shifts and component matrix scales.</param>
    public void PrepareReconstruction(MemoryAllocator allocator, JxlHeader image, JxlFrameHeader frame)
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
    /// Reconstructs the current group's accumulated coefficients before its storage is reused for another group.
    /// </summary>
    /// <param name="group">The validated row-major AC group index.</param>
    /// <param name="cancellationToken">The token checked between block rows and transforms.</param>
    public void ReconstructGroup(int group, CancellationToken cancellationToken)
    {
        Span<float> workspace = this.reconstructionMemory.Span;
        ReadOnlySpan<float> matrices = this.QuantizationMatrices;
        if (this.Uses16BitCoefficients)
        {
            this.ReconstructGroup(
                group, this.GetCoefficients16(0), this.GetCoefficients16(1), this.GetCoefficients16(2), matrices, workspace, cancellationToken);
        }
        else
        {
            this.ReconstructGroup(
                group, this.GetCoefficients32(0), this.GetCoefficients32(1), this.GetCoefficients32(2), matrices, workspace, cancellationToken);
        }
    }

    /// <summary>
    /// Dequantizes and transforms packed short or int coefficients through the same block traversal.
    /// </summary>
    /// <typeparam name="TCoefficient">The frame-selected short or int coefficient storage.</typeparam>
    /// <param name="group">The current group index.</param>
    /// <param name="quantizedX">The complete packed X coefficient plane.</param>
    /// <param name="quantizedY">The complete packed Y coefficient plane.</param>
    /// <param name="quantizedB">The complete packed B coefficient plane.</param>
    /// <param name="matrices">The frame's retained decoder matrices.</param>
    /// <param name="workspace">The frame-owned coefficient, spatial and inverse-transform scratch ranges.</param>
    /// <param name="cancellationToken">The token checked during traversal.</param>
    private void ReconstructGroup<TCoefficient>(
        int group,
        ReadOnlySpan<TCoefficient> quantizedX,
        ReadOnlySpan<TCoefficient> quantizedY,
        ReadOnlySpan<TCoefficient> quantizedB,
        ReadOnlySpan<float> matrices,
        Span<float> workspace,
        CancellationToken cancellationToken)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        Rectangle rectangle = this.GetAcGroupRectangle(group);
        InlineArray4<int> offsetStorage = default;
        Span<int> offsets = offsetStorage;
        InlineArray64<int> zeroStorage = default;
        ReadOnlySpan<int> zeroIntegers = zeroStorage;
        ReadOnlySpan<TCoefficient> zeros = MemoryMarshal.Cast<int, TCoefficient>(zeroIntegers);
        Span<float> spatial = workspace.Slice(3 * this.reconstructionBlockArea, this.reconstructionBlockArea);
        Span<float> scratch = workspace.Slice(4 * this.reconstructionBlockArea, this.inverseScratchLength);
        ReadOnlySpan<int> matrixOffsets = this.QuantizationMatrixOffsets;

        for (int by = rectangle.Y; by < rectangle.Bottom; by++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<byte> strategies = this.Strategies.DangerousGetRowSpan(by);
            ReadOnlySpan<int> quantizers = this.Quantization.DangerousGetRowSpan(by);
            ReadOnlySpan<sbyte> correlationX = this.CorrelationX.DangerousGetRowSpan(by >> ColorTileShift);
            ReadOnlySpan<sbyte> correlationB = this.CorrelationB.DangerousGetRowSpan(by >> ColorTileShift);
            int bx = rectangle.X;
            while (bx < rectangle.Right)
            {
                byte packed = strategies[bx];
                if ((packed & FirstBlockFlag) == 0)
                {
                    bx++;
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                byte strategy = (byte)(packed >> 1);
                int blockWidth = StrategyWidths[strategy];
                int blockHeight = StrategyHeights[strategy];
                int area = CoefficientBlockArea * blockWidth * blockHeight;
                int active = 0;
                for (int component = 0; component < 3; component++)
                {
                    if (((bx >> this.horizontalShifts[component]) << this.horizontalShifts[component]) == bx
                        && ((by >> this.verticalShifts[component]) << this.verticalShifts[component]) == by)
                    {
                        active |= 1 << component;
                    }
                }

                // Subsampled frames permit only single-block transforms. A zero block supplies inactive
                // components without reading a later packed transform. Only active components are written below.
                ReadOnlySpan<TCoefficient> qx = (active & 1) != 0 ? quantizedX.Slice(offsets[0], area) : zeros[..area];
                ReadOnlySpan<TCoefficient> qy = (active & 2) != 0 ? quantizedY.Slice(offsets[1], area) : zeros[..area];
                ReadOnlySpan<TCoefficient> qb = (active & 4) != 0 ? quantizedB.Slice(offsets[2], area) : zeros[..area];
                Span<float> coefficients = workspace[..(3 * area)];
                int matrixOffset = matrixOffsets[GetQuantizationMatrixIndex(strategy, 0)];
                float scale = this.InverseGlobalScale / quantizers[bx];
                Vector2 correlation = new(
                    this.BaseCorrelationX + (correlationX[bx >> ColorTileShift] * this.ColorScale),
                    this.BaseCorrelationB + (correlationB[bx >> ColorTileShift] * this.ColorScale));

                DequantizeAcBlock(
                    qx, qy, qb, matrices.Slice(matrixOffset, 3 * area), coefficients, this.quantizationScale * scale, correlation, this.quantizationBias);

                for (int component = 0; component < 3; component++)
                {
                    if ((active & (1 << component)) == 0)
                    {
                        continue;
                    }

                    int x = bx >> this.horizontalShifts[component];
                    int y = by >> this.verticalShifts[component];
                    Buffer2D<float> output = this.reconstructedChannels[component];
                    Size outputSize = this.reconstructedSizes[component];
                    int left = x * BlockSize;
                    int top = y * BlockSize;
                    offsets[component] += area;

                    // Sampling-factor padding can add whole blocks outside a component's visible dimensions.
                    // Their coefficients have already been decoded, but they supply no samples to reconstruct.
                    if (left >= outputSize.Width || top >= outputSize.Height)
                    {
                        continue;
                    }

                    Buffer2DRegion<float> dc = this.GetDcChannel(component).GetSubRegion(new Rectangle(x, y, blockWidth, blockHeight));
                    JxlInverseTransform.Transform(
                        strategy, coefficients.Slice(component * area, area), dc, spatial[..area], blockWidth * BlockSize, scratch);

                    // The allocator can split a frame plane across backing buffers. Reconstruct into the reusable
                    // contiguous spatial block, then copy each visible row through the existing Buffer2D API.
                    // Clip only storage at partial image edges; transform arithmetic uses the complete padded block.
                    int width = Math.Min(blockWidth * BlockSize, outputSize.Width - left);
                    int height = Math.Min(blockHeight * BlockSize, outputSize.Height - top);
                    for (int row = 0; row < height; row++)
                    {
                        spatial.Slice(row * blockWidth * BlockSize, width).CopyTo(output.DangerousGetRowSpan(top + row).Slice(left, width));
                    }
                }

                bx += blockWidth;
            }
        }
    }

    /// <summary>
    /// Releases the reconstructed component planes and the frame's reusable transform workspace.
    /// </summary>
    private void DisposeReconstruction()
    {
        this.reconstructionWorkspace?.Dispose();
        foreach (Buffer2D<float> channel in this.reconstructedChannels)
        {
            channel.Dispose();
        }
    }
}
