// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// Accepts XYB as input channels. If the output is XYB, this stage
/// scales them. Otherwise, converts them to RGB.
/// </summary>
internal class XybStage : RenderPipelineStageBase
{
    /// <summary>
    /// Parameters for XYB to RGB conversion.
    /// </summary>
    private readonly JxlOpsinParameters opsinParameters;

    /// <summary>
    /// Is the output channel XYB? If so, this stage will
    /// scale the input XYB channels for output. Otherwise,
    /// the input XYB channels are converted to RGB.
    /// </summary>
    private readonly bool outputIsXYB;

    public XybStage(Configuration configuration, JxlOutputEncodingInfo outputEncodingInfo)
        : base(configuration)
    {
        this.opsinParameters = outputEncodingInfo.OpsinParameters;
        this.outputIsXYB = outputEncodingInfo.ColorEncoding.ColorSpace == JxlColorSpace.Xyb;
    }

    /// <inheritdoc />
    public override string Name => "XYB";

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel)
        => channel < 3 ? RenderPipelineChannelMode.InPlace
                       : RenderPipelineChannelMode.Ignored;

    /// <inheritdoc />
    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        if (xExtraLeft == 0 || xExtraRight == 0)
        {
            throw new InvalidOperationException("xExtraLeft/xExtraRight must both be > 0");
        }

        Span<float> row0 = this.GetInputRow(inputRows, 0, 0);
        Span<float> row1 = this.GetInputRow(inputRows, 1, 0);
        Span<float> row2 = this.GetInputRow(inputRows, 2, 0);

        if (this.outputIsXYB)
        {
            // If the output is preferred to be XYB, then
            // we simply scale the input XYB channels.
            Vector<float> scaleX = Vector.Create(JxlOpsinConstants.ScaledXybScale[0]);
            Vector<float> scaleY = Vector.Create(JxlOpsinConstants.ScaledXybScale[1]);
            Vector<float> scaleBMY = Vector.Create(JxlOpsinConstants.ScaledXybScale[2]);
            Vector<float> offsetX = Vector.Create(JxlOpsinConstants.ScaledXybOffset[0]);
            Vector<float> offsetY = Vector.Create(JxlOpsinConstants.ScaledXybOffset[1]);
            Vector<float> offsetBMY = Vector.Create(JxlOpsinConstants.ScaledXybOffset[2]);

            // Main scaling loop
            for (int x = 0; x <= width - Vector<float>.Count; x += Vector<float>.Count)
            {
                // Input XYB.
                Vector<float> inX = Vector.Create<float>(row0[x..]);
                Vector<float> inY = Vector.Create<float>(row1[x..]);
                Vector<float> inB = Vector.Create<float>(row2[x..]);

                // Scaled input XYB.
                Vector<float> outX = (inX + offsetX) * scaleX;
                Vector<float> outY = (inY + offsetY) * scaleY;
                Vector<float> outB = ((inB - inY) + offsetBMY) * scaleBMY;

                // Write scaled XYB in-place.
                outX.CopyTo(row0[x..]);
                outY.CopyTo(row1[x..]);
                outB.CopyTo(row2[x..]);
            }
        }
        else
        {
            // In this case, we convert XYB to RGB in-place.
            for (int x = 0; x <= width - Vector<float>.Count; x += Vector<float>.Count)
            {
                // Input XYB.
                Vector<float> inOpsinX = Vector.Create<float>(row0[x..]);
                Vector<float> inOpsinY = Vector.Create<float>(row1[x..]);
                Vector<float> inOpsinB = Vector.Create<float>(row2[x..]);

                // Convert XYB->RGB
                Unsafe.SkipInit(out Vector<float> r);
                Unsafe.SkipInit(out Vector<float> g);
                Unsafe.SkipInit(out Vector<float> b);
                JxlXybDecoder.ConvertXybToRgb(inOpsinX, inOpsinY, inOpsinB, this.opsinParameters, ref r, ref g, ref b);

                // Write RGB in-place.
                r.CopyTo(row0[x..]);
                g.CopyTo(row1[x..]);
                b.CopyTo(row2[x..]);
            }
        }
    }
}
