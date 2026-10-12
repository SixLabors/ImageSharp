// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal sealed class GaborishStage : RenderPipelineStageBase
{
    private InlineArray9<float> weights;

    public GaborishStage(Configuration configuration, JxlLoopFilter lf)
        : base(configuration)
    {
        this.Settings = RenderPipelineStageConfiguration.CreateSymmetricBorderOnly(1);

        this.weights[0] = 1;
        this.weights[1] = lf.GaborishXWeight1;
        this.weights[2] = lf.GaborishXWeight2;
        this.weights[3] = 1;
        this.weights[4] = lf.GaborishYWeight1;
        this.weights[5] = lf.GaborishYWeight2;
        this.weights[6] = 1;
        this.weights[7] = lf.GaborishBWeight1;
        this.weights[8] = lf.GaborishBWeight2;

        // Normalization
        for (int c = 0; c < 3; c++)
        {
            int c3 = c * 3; // prevent repeated multiplication

            float div = this.weights[c3] + (4 * (this.weights[c3 + 1] + this.weights[c3 + 2]));
            float mul = 1.0f / div;

            this.weights[c3] *= mul;
            this.weights[c3 + 1] *= mul;
            this.weights[c3 + 2] *= mul;
        }
    }

    /// <inheritdoc />
    public override string Name => "Gab";

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        int xStart = -JxlMath.RoundUpTo(xExtraLeft, Vector<float>.Count);
        int xEnd = width + xExtraRight;

        for (int c = 0; c < 3; c++)
        {
            int c3 = c * 3; // prevent repeated multiplication

            Span<float> rowT = this.GetInputRow(inputRows, c, -1);
            Span<float> rowM = this.GetInputRow(inputRows, c, 0);
            Span<float> rowB = this.GetInputRow(inputRows, c, 1);
            Span<float> rowOut = GetOutputRow(outputRows, c, 0);

            Vector<float> w0 = Vector.Create(this.weights[c3]);
            Vector<float> w1 = Vector.Create(this.weights[c3 + 1]);
            Vector<float> w2 = Vector.Create(this.weights[c3 + 2]);

            // Ref for performance
            ref float refRowT = ref MemoryMarshal.GetReference(rowT);
            ref float refRowM = ref MemoryMarshal.GetReference(rowM);
            ref float refRowB = ref MemoryMarshal.GetReference(rowB);
            ref float refRowOut = ref MemoryMarshal.GetReference(rowOut);

            for (int x = xStart; x < xEnd; x += Vector<float>.Count)
            {
                Vector<float> t = Vector.LoadUnsafe(ref Unsafe.Add(ref refRowT, x));
                Vector<float> tl = Vector.LoadUnsafe(ref Unsafe.Add(ref refRowT, x - 1));
                Vector<float> tr = Vector.LoadUnsafe(ref Unsafe.Add(ref refRowT, x + 1));

                Vector<float> m = Vector.LoadUnsafe(ref Unsafe.Add(ref refRowM, x));
                Vector<float> l = Vector.LoadUnsafe(ref Unsafe.Add(ref refRowM, x - 1));
                Vector<float> r = Vector.LoadUnsafe(ref Unsafe.Add(ref refRowM, x + 1));

                Vector<float> b = Vector.LoadUnsafe(ref Unsafe.Add(ref refRowB, x));
                Vector<float> bl = Vector.LoadUnsafe(ref Unsafe.Add(ref refRowB, x - 1));
                Vector<float> br = Vector.LoadUnsafe(ref Unsafe.Add(ref refRowB, x + 1));

                Vector<float> sum0 = m;
                Vector<float> sum1 = (l + r) + (t + b);
                Vector<float> sum2 = (tl + tr) + (bl + br);

                Vector<float> pixels = (sum2 * w2) + ((sum1 * w1) + (sum0 * w0));
                pixels.StoreUnsafe(ref Unsafe.Add(ref refRowOut, x));
            }
        }
    }

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel) =>
        channel < 3
            ? RenderPipelineChannelMode.InPlace
            : RenderPipelineChannelMode.Ignored;
}
