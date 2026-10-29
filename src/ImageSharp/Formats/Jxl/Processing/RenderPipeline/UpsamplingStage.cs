// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal class UpsamplingStage : RenderPipelineStageBase
{
    private const int ChunkSize = 1024;

    private readonly float[] kernel = new float[64 * 25]; // 1600 floats, 6400 bytes (6.25 KB)
    private readonly int channelIndex;
    private readonly InlineArray3<List<Memory<byte>>> temp = default;

    /// <summary>
    /// Initializes a new instance of the UpsamplingStage class.
    /// </summary>
    /// <param name="configuration">The configuration used by the render stage.</param>
    /// <param name="transformData">The custom upsampling filter weights.</param>
    /// <param name="channelIndex">The channel processed by this stage.</param>
    /// <param name="upsamplingShift">The base-two logarithm of the upsampling factor.</param>
    public UpsamplingStage(Configuration configuration, JxlCustomTransformData transformData, int channelIndex, int upsamplingShift)
        : base(configuration)
    {
        this.channelIndex = channelIndex;
        this.Settings = RenderPipelineStageConfiguration.CreateSymmetric(upsamplingShift, 2);

        Span<float> kernel = this.kernel;

        Span<float> weights = upsamplingShift == 1 ? transformData.Upsampling2Weights
            : upsamplingShift == 2 ? transformData.Upsampling4Weights
            : transformData.Upsampling8Weights;

        int n = 1 << upsamplingShift;
        int h = n / 2;

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

    public override string Name => "Upsample";

    public override RenderPipelineChannelMode GetChannelMode(int channel) =>
        channel == this.channelIndex
            ? RenderPipelineChannelMode.InOut
            : RenderPipelineChannelMode.Ignored;
}
