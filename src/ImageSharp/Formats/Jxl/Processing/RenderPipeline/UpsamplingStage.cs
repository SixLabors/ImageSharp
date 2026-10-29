// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// This render pipeline stage performs upsampling.
/// </summary>
internal class UpsamplingStage : RenderPipelineStageBase
{
    private const int ChunkSize = 1024;

    /// <summary>
    /// The upsampling kernels.
    /// </summary>
    private readonly float[] kernel = new float[64 * 25]; // 1600 floats, 6400 bytes (6.25 KB)

    /// <summary>
    /// The offset of the channel.
    /// </summary>
    private readonly int channel;

    /// <summary>
    /// Temporary scratch-space.
    /// </summary>
    private InlineArray3<Buffer2D<float>> temp;

    public UpsamplingStage(Configuration configuration, JxlCustomTransformData upsFactors, int c, int shift)
        : base(configuration)
    {
        this.channel = c;
        this.Settings = RenderPipelineStageConfiguration.CreateSymmetric(shift, 2);

        Span<float> kernel = this.kernel;

        Span<float> weights = shift == 1 ? upsFactors.Upsampling2Weights
            : shift == 2 ? upsFactors.Upsampling4Weights
            : upsFactors.Upsampling8Weights;

        int n = 1 << shift;
        int h = n / 2;

        // Initialize the upsampling kernels.
        for (int ky = 0; ky < h; ++ky)
        {
            for (int kx = 0; kx < h; ++kx)
            {
                int offset0 = ((ky * n) + kx) * 25;
                int offset1 = ((ky * n) + (n - 1 - kx)) * 25;
                int offset2 = (((n - 1 - ky) * n) + kx) * 25;
                int offset3 = (((n - 1 - ky) * n) + (n - 1 - kx)) * 25;

                for (int py = 0; py < 5; ++py)
                {
                    for (int px = 0; px < 5; ++px)
                    {
                        int j = (5 * ky) + py;
                        int i = (5 * kx) + px;
                        int my = Math.Min(i, j);
                        int mx = Math.Max(i, j);
                        float w = weights[(5 * h * my) - (my * (my - 1) / 2) + mx - my];

                        kernel[offset0 + (py * 5) + px] = w;
                        kernel[offset1 + (py * 5) + (4 - px)] = w;
                        kernel[offset2 + ((4 - py) * 5) + px] = w;
                        kernel[offset3 + ((4 - py) * 5) + (4 - px)] = w;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public override string Name => "Upsample";

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel) =>
        channel == this.channel
            ? RenderPipelineChannelMode.InOut
            : RenderPipelineChannelMode.Ignored;

    /// <inheritdoc />
    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        // The number of iterations. This can only be 2, 4 or 8.
        int n = 1 << this.Settings.ShiftX;

        // Upsampling loop
        for (int x = 0; x < width; x += ChunkSize)
        {
            int xEnd = Math.Min(x + ChunkSize, width);
            int length = xEnd - x;

            // threadId would be equal to the index of multithreading.
            // So for example in Parallel.For(0, 100, (x) => ...), 'x'
            // is equivalent to threadId here.
            this.PreprocessRow(inputRows, x, length, threadId);
            this.ProcessRowInternal(n, inputRows, outputRows, x, length, threadId);
        }
    }

    public void PrepareForThreads(Configuration configuration, int threadCount)
    {
        int allocSize = ChunkSize + Math.Max(4, Vector<float>.Count);
        for (int i = 0; i < 3; i++)
        {
            this.temp[i] = configuration.MemoryAllocator.Allocate2D<float>(threadCount, allocSize);
        }
    }

    private void PreprocessRow(Buffer2D<Memory<float>> inputRows, int x0, int length, int threadId)
    {
        Span<float> colMin = this.temp[0].DangerousGetRowSpan(threadId);
        Span<float> colMax = this.temp[1].DangerousGetRowSpan(threadId);

        InlineArray5<Memory<float>> rows = default;
        rows[0] = this.GetInputRowMemory(inputRows, this.channel, -2)[(x0 - 2)..];
        rows[1] = this.GetInputRowMemory(inputRows, this.channel, -1)[(x0 - 2)..];
        rows[2] = this.GetInputRowMemory(inputRows, this.channel, 0)[(x0 - 2)..];
        rows[3] = this.GetInputRowMemory(inputRows, this.channel, 1)[(x0 - 2)..];
        rows[4] = this.GetInputRowMemory(inputRows, this.channel, 2)[(x0 - 2)..];

        for (int x = 0; x <= length - Vector<float>.Count + 4; x += Vector<float>.Count)
        {
            Vector<float> v0 = Vector.Create<float>(rows[0].Span[x..]);
            Vector<float> v1 = Vector.Create<float>(rows[1].Span[x..]);
            Vector<float> min0 = Vector.Min(v0, v1);
            Vector<float> max0 = Vector.Max(v0, v1);
            Vector<float> v2 = Vector.Create<float>(rows[2].Span[x..]);
            Vector<float> v3 = Vector.Create<float>(rows[3].Span[x..]);
            Vector<float> min1 = Vector.Min(v2, v3);
            Vector<float> max1 = Vector.Max(v2, v3);
            Vector<float> v4 = Vector.Create<float>(rows[4].Span[x..]);
            Vector<float> min2 = Vector.Min(min0, min1);
            Vector<float> max2 = Vector.Max(max0, max1);
            Vector<float> min = Vector.Min(v4, min2);
            Vector<float> max = Vector.Max(v4, max2);
            min.CopyTo(colMin[x..]);
            max.CopyTo(colMax[x..]);
        }

        Span<float> mins = this.temp[2].DangerousGetRowSpan(threadId);
        for (int x = 0; x <= length - Vector<float>.Count; x += Vector<float>.Count)
        {
            Vector<float> v0 = Vector.Create<float>(colMin[x..]);
            Vector<float> v1 = Vector.Create<float>(colMin[(x + 1)..]);
            Vector<float> min0 = Vector.Min(v0, v1);
            Vector<float> v2 = Vector.Create<float>(colMin[(x + 2)..]);
            Vector<float> v3 = Vector.Create<float>(colMin[(x + 3)..]);
            Vector<float> min1 = Vector.Min(v2, v3);
            Vector<float> v4 = Vector.Create<float>(colMin[(x + 4)..]);
            Vector<float> min2 = Vector.Min(min0, min1);
            Vector<float> min = Vector.Min(v4, min2);
            min.CopyTo(mins[x..]);
        }

        Span<float> maxs = this.temp[0].DangerousGetRowSpan(threadId);
        for (int x = 0; x <= length - Vector<float>.Count; x += Vector<float>.Count)
        {
            Vector<float> v0 = Vector.Create<float>(colMax[x..]);
            Vector<float> v1 = Vector.Create<float>(colMax[(x + 1)..]);
            Vector<float> max0 = Vector.Max(v0, v1);
            Vector<float> v2 = Vector.Create<float>(colMax[(x + 2)..]);
            Vector<float> v3 = Vector.Create<float>(colMax[(x + 3)..]);
            Vector<float> max1 = Vector.Max(v2, v3);
            Vector<float> v4 = Vector.Create<float>(colMax[(x + 4)..]);
            Vector<float> max2 = Vector.Max(max0, max1);
            Vector<float> max = Vector.Max(v4, max2);
            max.CopyTo(maxs[x..]);
        }
    }

    private float GetKernel(int k, int i) => this.kernel[(k * 25) + i];

    private void ProcessRowInternal(int n, Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int x0, int length, int threadId)
    {
        InlineArray8<Vector<float>> upsampling = default;
        Span<float> mins = this.temp[2].DangerousGetRowSpan(threadId);
        Span<float> maxs = this.temp[0].DangerousGetRowSpan(threadId);
        InlineArray25<Memory<float>> input = default;

        // Retrieve the input rows into the 'input' variable.
        for (int iy = -2; iy <= 2; ++iy)
        {
            for (int ix = -2; ix <= 2; ++ix)
            {
                input[(5 * (iy + 2)) + (ix + 2)] = this.GetInputRowMemory(inputRows, this.channel, iy)[(x0 + ix)..];
            }
        }

        // Upsampling loop
        for (int x = 0; x <= length - Vector<float>.Count; x += Vector<float>.Count)
        {
            for (int oy = 0; oy < n; oy++)
            {
                Span<float> destRow = GetOutputRow(outputRows, this.channel, oy)[(x0 * n)..];
                for (int ox = 0; ox < n; ox++)
                {
                    // Main upsampling logic here.
                    int k = (n * oy) + ox;

                    // Get input as SIMD vectors and multiply them by the kernel.
                    Vector<float> acc0 = Vector.Create<float>(input[0].Span) * Vector.Create(this.GetKernel(k, 0));
                    Vector<float> acc1 = Vector.Create<float>(input[1].Span) * Vector.Create(this.GetKernel(k, 1));
                    Vector<float> acc2 = Vector.Create<float>(input[2].Span) * Vector.Create(this.GetKernel(k, 2));

                    // Upsample using the kernel.
                    for (int i = 3; i < 24; i += 3)
                    {
                        acc0 = Vector.FusedMultiplyAdd(Vector.Create<float>(input[i].Span), Vector.Create(this.GetKernel(k, i)), acc0);
                        acc1 = Vector.FusedMultiplyAdd(Vector.Create<float>(input[i + 1].Span), Vector.Create(this.GetKernel(k, i + 1)), acc1);
                        acc2 = Vector.FusedMultiplyAdd(Vector.Create<float>(input[i + 2].Span), Vector.Create(this.GetKernel(k, i + 2)), acc2);
                    }

                    acc0 = Vector.FusedMultiplyAdd(Vector.Create<float>(input[24].Span), Vector.Create(this.GetKernel(k, 24)), acc0);

                    // Finalize
                    Vector<float> result = acc0 + acc1 + acc2;
                    upsampling[ox] = Vector.Clamp(result, Vector.Create<float>(mins[x..]), Vector.Create<float>(maxs[x..]));
                }

                // Write back the upsampled vectors to the destination row.
                switch (n)
                {
                    case 2:
                        JxlSimdUtils.StoreInterleaved(
                            upsampling[0],
                            upsampling[1],
                            ref destRow[x * n]);
                        break;
                    case 4:
                        JxlSimdUtils.StoreInterleaved(
                            upsampling[0],
                            upsampling[1],
                            upsampling[2],
                            upsampling[3],
                            ref destRow[x * n]);
                        break;
                    case 8:
                        JxlSimdUtils.StoreInterleaved(
                            upsampling[0],
                            upsampling[1],
                            upsampling[2],
                            upsampling[3],
                            upsampling[4],
                            upsampling[5],
                            upsampling[6],
                            upsampling[7],
                            ref destRow[x * n]);
                        break;
                }
            }

            // Shift them SIMD length forward. This is so the
            // next iteration can start at the shifted offset.
            for (int i = 0; i < 25; i++)
            {
                input[i] = input[i][Vector<float>.Count..];
            }
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        base.Dispose();

        // We only have to dispose scratch-space.
        this.temp[0].Dispose();
        this.temp[1].Dispose();
        this.temp[2].Dispose();
    }
}
