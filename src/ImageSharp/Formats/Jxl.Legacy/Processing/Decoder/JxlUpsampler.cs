// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Reconstructs two, four or eight samples per input sample with the codestream's five-by-five kernels.
/// </summary>
/// <remarks>
/// One sequential decoder owns this reusable workspace. Source rows borrow their two-sample horizontal halos from the caller.
/// The caller resolves vertical image edges before supplying the five rows. Source and destination storage must not overlap.
/// </remarks>
internal sealed partial class JxlUpsampler : IDisposable
{
    /// <summary>
    /// The number of coefficients in each five-by-five phase kernel.
    /// </summary>
    private const int KernelLength = 25;

    /// <summary>
    /// The number of input samples processed before reusing the extrema workspace.
    /// </summary>
    private const int ChunkSize = 1024;

    /// <summary>
    /// The two input neighbors on either side of each reconstructed sample.
    /// </summary>
    public const int Border = 2;

    /// <summary>
    /// The expanded kernels followed by three chunk-sized extrema buffers.
    /// </summary>
    private readonly IMemoryOwner<float> owner;

    /// <summary>
    /// The logical allocation, without any excess allocator capacity.
    /// </summary>
    private readonly Memory<float> memory;

    /// <summary>
    /// Initializes expanded kernels and reusable extrema storage before processing rows.
    /// </summary>
    /// <param name="configuration">The configuration that owns numeric storage.</param>
    /// <param name="transforms">The parsed default or custom upsampling coefficients.</param>
    /// <param name="factor">The parsed upsampling factor: two, four or eight.</param>
    public JxlUpsampler(Configuration configuration, JxlCustomTransformData transforms, int factor)
    {
        this.Factor = factor;
        int kernelSize = factor * factor * KernelLength;
        int length = kernelSize + (3 * (ChunkSize + (2 * Border)));
        this.owner = configuration.MemoryAllocator.Allocate<float>(length);

        try
        {
            this.memory = this.owner.Memory[..length];
            Span<float> kernels = this.memory.Span[..kernelSize];
            ReadOnlySpan<float> weights = factor == 2 ? transforms.Upsampling2Weights
                : factor == 4 ? transforms.Upsampling4Weights : transforms.Upsampling8Weights;
            int half = factor / 2;

            // The wire stores one triangular quarter of the phase kernels. Swap the two coordinates for triangular lookup,
            // then reflect each five-by-five kernel across its horizontal and vertical axes for the other output phases.
            for (int phaseY = 0; phaseY < half; phaseY++)
            {
                for (int phaseX = 0; phaseX < half; phaseX++)
                {
                    int topLeft = ((phaseY * factor) + phaseX) * KernelLength;
                    int topRight = ((phaseY * factor) + factor - 1 - phaseX) * KernelLength;
                    int bottomLeft = (((factor - 1 - phaseY) * factor) + phaseX) * KernelLength;
                    int bottomRight = (((factor - 1 - phaseY) * factor) + factor - 1 - phaseX) * KernelLength;

                    for (int y = 0; y < 5; y++)
                    {
                        for (int x = 0; x < 5; x++)
                        {
                            int first = (5 * phaseX) + x;
                            int second = (5 * phaseY) + y;
                            int lower = Math.Min(first, second);
                            int upper = Math.Max(first, second);
                            float weight = weights[(5 * half * lower) - (lower * (lower - 1) / 2) + upper - lower];
                            kernels[topLeft + (y * 5) + x] = weight;
                            kernels[topRight + (y * 5) + 4 - x] = weight;
                            kernels[bottomLeft + ((4 - y) * 5) + x] = weight;
                            kernels[bottomRight + ((4 - y) * 5) + 4 - x] = weight;
                        }
                    }
                }
            }
        }
        catch
        {
            this.owner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the number of output samples per input sample on each axis.
    /// </summary>
    public int Factor { get; }

    /// <summary>
    /// Reconstructs a strided input rectangle with a two-sample halo on all four sides.
    /// </summary>
    /// <param name="source">The source from the top-left halo sample. Logical input sample zero is at column two, row two.</param>
    /// <param name="sourceStride">The physical distance between source rows, in floats.</param>
    /// <param name="destination">The destination from its top-left logical output sample.</param>
    /// <param name="destinationStride">The physical distance between destination rows, in floats.</param>
    /// <param name="width">The input width, excluding the two halo samples on either side.</param>
    /// <param name="height">The input height, excluding the two halo rows above and below.</param>
    /// <remarks>
    /// The caller prepares mirrored image edges and neighboring group samples in the halo before this call.
    /// Source contains height plus four rows, each with width plus four samples. Destination contains height times Factor rows,
    /// each with width times Factor samples. Source and destination must not overlap. Row padding remains unchanged.
    /// </remarks>
    public void Upsample(ReadOnlySpan<float> source, int sourceStride, Span<float> destination, int destinationStride, int width, int height)
    {
        int kernelSize = this.Factor * this.Factor * KernelLength;
        Span<float> storage = this.memory.Span;
        ReadOnlySpan<float> kernels = storage[..kernelSize];
        int scratchLength = ChunkSize + (2 * Border);
        Span<float> columnMinimum = storage.Slice(kernelSize, scratchLength);
        Span<float> columnMaximum = storage.Slice(kernelSize + scratchLength, scratchLength);
        Span<float> minimum = storage.Slice(kernelSize + (2 * scratchLength), scratchLength);

        // Borrow allocator storage once for the entire rectangle. A source position names the upper-left neighbor of the
        // five-by-five stencil; its center is two columns and two rows later. All row spans retain the caller's real stride.
        for (int y = 0; y < height; y++)
        {
            int sourceOffset = y * sourceStride;
            ReadOnlySpan<float> top2 = source.Slice(sourceOffset, width + (2 * Border));
            ReadOnlySpan<float> top1 = source.Slice(sourceOffset + sourceStride, width + (2 * Border));
            ReadOnlySpan<float> center = source.Slice(sourceOffset + (2 * sourceStride), width + (2 * Border));
            ReadOnlySpan<float> bottom1 = source.Slice(sourceOffset + (3 * sourceStride), width + (2 * Border));
            ReadOnlySpan<float> bottom2 = source.Slice(sourceOffset + (4 * sourceStride), width + (2 * Border));
            Span<float> outputRows = destination[(y * this.Factor * destinationStride)..];

            // Each chunk includes exactly four horizontal neighbors. No vector rounding extends the input or output.
            // The maximum pass can reuse columnMinimum only after the minimum pass has consumed it completely.
            for (int start = 0; start < width; start += ChunkSize)
            {
                int count = Math.Min(ChunkSize, width - start);
                int haloCount = count + (2 * Border);
                ReadOnlySpan<float> row0 = top2.Slice(start, haloCount);
                ReadOnlySpan<float> row1 = top1.Slice(start, haloCount);
                ReadOnlySpan<float> row2 = center.Slice(start, haloCount);
                ReadOnlySpan<float> row3 = bottom1.Slice(start, haloCount);
                ReadOnlySpan<float> row4 = bottom2.Slice(start, haloCount);
                PrepareColumnExtrema(row0, row1, row2, row3, row4, columnMinimum[..haloCount], columnMaximum[..haloCount]);
                PrepareMinimum(columnMinimum, minimum[..count]);
                PrepareMaximum(columnMaximum, columnMinimum[..count]);
                Span<float> output = outputRows[(start * this.Factor)..];

                // Select the expansion once per chunk. The closed workers keep the phase count and interleave shape constant.
                switch (this.Factor)
                {
                    case 2:
                        Process2(row0, row1, row2, row3, row4, minimum[..count], columnMinimum[..count], output, destinationStride, kernels);
                        break;
                    case 4:
                        Process4(row0, row1, row2, row3, row4, minimum[..count], columnMinimum[..count], output, destinationStride, kernels);
                        break;
                    case 8:
                        Process8(row0, row1, row2, row3, row4, minimum[..count], columnMinimum[..count], output, destinationStride, kernels);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Releases the kernels and extrema workspace owned by this upsampler.
    /// </summary>
    public void Dispose() => this.owner.Dispose();
}
